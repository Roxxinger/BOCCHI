using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.Hunt;
using BOCCHI.Treasure.Services;
using ECommons.Throttlers;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;

namespace BOCCHI.Automator.Services;

/// <summary>Illegal Mode post-activity treasure filler.</summary>
public class IllegalModeTreasureFillerService
(
    IAutomator automator,
    IAutomatorContext context,
    IAutomatorMemory memory,
    ITreasureHunter hunter,
    ITreasureTracker tracker,
    ISupportJobFactory supportJobs,
    IZoneProvider zones,
    IIllegalModeStartableActivityProbe startableActivities,
    AutomatorConfig automatorConfig,
    TreasureConfig treasureConfig,
    ILogger<IllegalModeTreasureFillerService> logger
) : IOnUpdate
{
    // Default Order (0). TriageLatchService is Order 10 so PendingTriage is set before Sight latches.
    public int Order => 0;

    private bool hadActivity;

    private bool hadFillerHunt;

    private bool loggedSightUnavailable;

    private bool HasTreasureSight => SupportJobTreasureSight.CanCast(supportJobs);

    public void Update()
    {
        if ((!context.IsIllegalMode && !context.IsCompletionist) || context.IsPotsAndTreasure)
        {
            ResetSession();
            return;
        }

        if (automator.SuspendedForShopping)
        {
            return;
        }

        if (!automatorConfig.EnableAutomaticTreasureHuntDuringIllegalMode)
        {
            ResetSession();
            return;
        }

        if (!zones.GetZone().IsOccultCrescentZone())
        {
            ResetSession();
            return;
        }

        EnsureSurveyMemory(out AutomaticTreasureSurveyMemory survey);
        ClearSurveyLatchIfSightUnavailable(survey);

        bool activityNow = IllegalModeActivityWork.HasFillerBlockingActivity(memory);
        if (hadActivity && !activityNow)
        {
            OnActivityCompleted(survey);
        }

        hadActivity = activityNow;

        if (hunter.ManagedByIllegalModeFiller && hunter.Running)
        {
            hadFillerHunt = true;
            UpdateRunningFillerHunt(activityNow);
            return;
        }

        if (hadFillerHunt && (!hunter.Running || !hunter.ManagedByIllegalModeFiller))
        {
            OnFillerHuntEnded(survey);
            hadFillerHunt = false;
        }

        if (activityNow)
        {
            PauseFillerHuntForActivity();
            return;
        }

        if (survey.WaitingForSurveyResult)
        {
            TryApplySurveyResult(survey);
            return;
        }

        if (survey.PendingSurvey)
        {
            // CastingTreasureSightHandler casts at camp; ReturningHandler gets us there.
            return;
        }

        if (survey.PendingMapHunt)
        {
            TryStartPendingMapHunt(survey);
            return;
        }

        if (ShouldStartHunt(survey))
        {
            EnterHuntPhase(fromSurvey: true);
        }
    }

    /// <summary>
    ///     Sight hunts stay exclusive. Map hunts (no Sight) pause when a FATE/CE is available so
    ///     Illegal Mode can take it, then resume the same route afterward.
    /// </summary>
    private void UpdateRunningFillerHunt(bool activityNow)
    {
        if (HasTreasureSight)
        {
            return;
        }

        if (activityNow)
        {
            if (!hunter.Paused)
            {
                hunter.Pause();
                logger.Debug("Illegal Mode: paused map treasure hunt for CE/FATE activity");
            }

            automator.SetSuspendedForTreasure(false);
            return;
        }

        bool startable = startableActivities.HasStartableFateOrCriticalEncounter();
        if (!hunter.Paused && startable)
        {
            hunter.Pause();
            automator.SetSuspendedForTreasure(false);
            if (EzThrottler.Throttle("IllegalModeMapHuntYield", 5000))
            {
                logger.Info("Illegal Mode: pausing map treasure hunt — FATE/CE available");
            }

            return;
        }

        if (hunter.Paused && !startable)
        {
            // Activity cancelled / nothing to do — keep filling the map.
            EnterHuntPhase(fromSurvey: false);
        }
    }

    private void EnsureSurveyMemory(out AutomaticTreasureSurveyMemory survey)
    {
        if (memory.TryRemember(out survey))
        {
            return;
        }

        survey = new AutomaticTreasureSurveyMemory();
        memory.TryAdd(survey);
    }

    private void OnActivityCompleted(AutomaticTreasureSurveyMemory survey)
    {
        if (survey.IsBusy)
        {
            return;
        }

        // TriageLatchService owns raise latch; wait until it finishes before Sight / map hunt.
        if (TriageSession.IsActive(memory))
        {
            return;
        }

        // Same map-hunt session was paused for this FATE/CE — continue remaining pads.
        if (hunter.ManagedByIllegalModeFiller && hunter.Running && hunter.Paused)
        {
            logger.Info("Illegal Mode: resuming map treasure hunt after FATE/CE");
            EnterHuntPhase(fromSurvey: false);
            return;
        }

        LatchPostActivityHunt(survey, "activity completed");
    }

    private void LatchPostActivityHunt(AutomaticTreasureSurveyMemory survey, string reason)
    {
        if (!HasTreasureSight)
        {
            survey.PendingSurvey = false;
            survey.WaitingForSurveyResult = false;
            survey.SurveyWaitDeadlineUtc = DateTime.MinValue;
            survey.PendingMapHunt = true;
            // Hunt owns travel/Return — drop any Automator Return already queued after the FATE/CE.
            memory.Forget<ReturningStateMemory>();
            LogSightUnavailableOnce();
            logger.Debug("Illegal Mode: latched map treasure hunt without Treasure Sight ({Reason})", reason);
            return;
        }

        survey.PendingMapHunt = false;
        survey.PendingSurvey = true;
        survey.WaitingForSurveyResult = false;
        survey.MinAcceptedRevision = tracker.SurveyRevision;
        survey.SurveyWaitDeadlineUtc = DateTime.MinValue;
        logger.Debug("Illegal Mode: latched Treasure Sight survey ({Reason})", reason);
    }

    private void ClearSurveyLatchIfSightUnavailable(AutomaticTreasureSurveyMemory survey)
    {
        if (HasTreasureSight)
        {
            loggedSightUnavailable = false;
            return;
        }

        if (survey.PendingSurvey || survey.WaitingForSurveyResult)
        {
            survey.PendingSurvey = false;
            survey.WaitingForSurveyResult = false;
            survey.SurveyWaitDeadlineUtc = DateTime.MinValue;
            survey.PendingMapHunt = true;
            memory.Forget<ReturningStateMemory>();
            LogSightUnavailableOnce();
            logger.Debug("Illegal Mode: Treasure Sight became unavailable — falling back to map hunt");
        }
    }

    private void LogSightUnavailableOnce()
    {
        if (loggedSightUnavailable)
        {
            return;
        }

        loggedSightUnavailable = true;
        logger.Info(
            "Illegal Mode: Treasure Sight unavailable (Freelancer below level {Level}) — using built-in coffer map (yields to FATE/CE)",
            SupportJobTreasureSight.RequiredFreelancerLevel);
    }

    private void TryStartPendingMapHunt(AutomaticTreasureSurveyMemory survey)
    {
        if (!hunter.IsVnavAvailable || TriageSession.IsActive(memory))
        {
            return;
        }

        // Prefer a live FATE/CE before burning a full map pass.
        if (startableActivities.HasStartableFateOrCriticalEncounter())
        {
            return;
        }

        if (automator.CurrentState is not (AutomatorState.Idle or null))
        {
            return;
        }

        // No Sight → no live fill counts. Always run the built-in map; thresholds only apply after a survey.
        survey.PendingMapHunt = false;
        EnterHuntPhase(fromSurvey: false);
    }

    private void TryApplySurveyResult(AutomaticTreasureSurveyMemory survey)
    {
        if (tracker.SurveyRevision > survey.MinAcceptedRevision && tracker.CountInitialised)
        {
            ApplySurveyResult(survey);
            return;
        }

        if (survey.SurveyWaitDeadlineUtc != DateTime.MinValue
            && DateTime.UtcNow >= survey.SurveyWaitDeadlineUtc)
        {
            survey.WaitingForSurveyResult = false;
            survey.PendingSurvey = false;
            logger.Debug("Illegal Mode: Treasure Sight survey timed out — retry after next activity");
        }
    }

    private void ApplySurveyResult(AutomaticTreasureSurveyMemory survey)
    {
        survey.WaitingForSurveyResult = false;
        survey.PendingSurvey = false;

        int silver = tracker.SilverChests;
        int bronze = tracker.BronzeChests;
        if (silver + bronze <= 0)
        {
            logger.Info("Illegal Mode: survey found no coffers — continuing CE/FATE farming");
            return;
        }

        if (!TreasureHuntFillGate.MeetsMinimumFill(tracker, treasureConfig))
        {
            logger.Info(
                "Illegal Mode: survey fill below threshold ({Silver} silver, {Bronze} bronze) — continuing CE/FATE farming",
                silver,
                bronze);
            return;
        }

        logger.Info(
            "Illegal Mode: survey found {Silver} silver, {Bronze} bronze — starting hunt",
            silver,
            bronze);
        EnterHuntPhase(fromSurvey: true);
    }

    private void OnFillerHuntEnded(AutomaticTreasureSurveyMemory survey)
    {
        // After a route, wait for the next activity before surveying / hunting again.
        survey.PendingSurvey = false;
        survey.WaitingForSurveyResult = false;
        survey.PendingMapHunt = false;
        survey.MinAcceptedRevision = tracker.SurveyRevision;
        automator.SetSuspendedForTreasure(false);
        logger.Info("Illegal Mode: treasure hunt ended — will fill again after next activity");
    }

    private bool ShouldStartHunt(AutomaticTreasureSurveyMemory survey)
    {
        if (!hunter.IsVnavAvailable || survey.IsBusy)
        {
            return false;
        }

        if (!tracker.CountInitialised || tracker.SurveyRevision <= survey.MinAcceptedRevision)
        {
            return false;
        }

        if (tracker.SilverChests + tracker.BronzeChests <= 0)
        {
            return false;
        }

        if (!TreasureHuntFillGate.MeetsMinimumFill(tracker, treasureConfig))
        {
            return false;
        }

        return automator.CurrentState is AutomatorState.Idle or null;
    }

    private void EnterHuntPhase(bool fromSurvey)
    {
        // Sight surveys: suspend Illegal Mode for the short revealed route.
        // Map hunts (no Sight): keep Automator awake so a spawned FATE/CE can interrupt.
        if (HasTreasureSight)
        {
            automator.SetSuspendedForTreasure(true);
        }
        else
        {
            automator.SetSuspendedForTreasure(false);
            memory.Forget<ReturningStateMemory>();
        }

        if (!hunter.IsVnavReady)
        {
            if (!fromSurvey)
            {
                // Keep retrying the map hunt once navmesh is ready.
                if (memory.TryRemember(out AutomaticTreasureSurveyMemory survey))
                {
                    survey.PendingMapHunt = true;
                }
            }

            return;
        }

        if (!hunter.Running)
        {
            hunter.ManagedByIllegalModeFiller = true;
            hunter.StartManaged();
            hadFillerHunt = true;
            if (fromSurvey && tracker.CountInitialised)
            {
                logger.Info(
                    "Illegal Mode: started automatic treasure hunt (survey {Silver} silver, {Bronze} bronze)",
                    tracker.SilverChests,
                    tracker.BronzeChests);
            }
            else
            {
                logger.Info("Illegal Mode: started automatic treasure hunt from built-in map (no Treasure Sight)");
            }

            return;
        }

        if (hunter.Paused)
        {
            // Map hunt (no Sight): after a distant FATE/CE, continue from nearby remaining pads
            // instead of walking back to where the route was paused.
            if (HasTreasureSight)
            {
                hunter.Resume();
            }
            else
            {
                hunter.ResumeNearPlayer();
            }

            hadFillerHunt = true;
            logger.Debug("Illegal Mode: resumed automatic treasure hunt");
        }
    }

    private void PauseFillerHuntForActivity()
    {
        automator.SetSuspendedForTreasure(false);

        if (!hunter.ManagedByIllegalModeFiller)
        {
            return;
        }

        if (hunter.Running && !hunter.Paused)
        {
            hunter.Pause();
            logger.Debug("Illegal Mode: paused treasure hunt for CE/FATE activity");
        }
    }

    private void ResetSession()
    {
        hadActivity = false;
        hadFillerHunt = false;
        loggedSightUnavailable = false;
        memory.Forget<AutomaticTreasureSurveyMemory>();

        if (hunter.ManagedByIllegalModeFiller)
        {
            automator.SetSuspendedForTreasure(false);
            hunter.ManagedByIllegalModeFiller = false;
            if (hunter.Running)
            {
                hunter.Toggle();
            }
        }
    }
}
