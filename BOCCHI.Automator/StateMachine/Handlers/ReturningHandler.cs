using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.Services;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Services.Gate;
using Ocelot.Services.PlayerState;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class ReturningHandler
(
    IAutomatorMemory memory,
    IAutomatorContext automator,
    IZoneProvider zones,
    ICondition conditions,
    IAddonLifecycle addons,
    IFateRepository fates,
    ICriticalEncounterRepository criticalEncounters,
    IPlayer player,
    IGateService gate,
    AutoRotationController autoRotation,
    ITreasureHunter hunter
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Returning)
{
    public override StatePriority GetScore()
    {
        if (!automator.Enabled || !zones.GetZone().IsOccultCrescentZone())
        {
            return StatePriority.Never;
        }

        // Treasure hunt is the idle filler in Pots & Treasure — never Return-to-camp.
        if (automator.IsPotsAndTreasure)
        {
            return StatePriority.Never;
        }

        // Return while dead accepts the death prompt and force-respawns.
        if (conditions[ConditionFlag.Unconscious])
        {
            return StatePriority.Never;
        }

        // Pathfinding already dequeued Return — this latch must win even if a map hunt was
        // just latched, or Teleport starts from the field and Lifestream fires short of camp.
        if (memory.TryRemember<ReturningStateMemory>(out ReturningStateMemory _))
        {
            return StatePriority.VeryHigh;
        }

        // Map-hunt filler (no Treasure Sight): hunt owns opportunistic Return / routing while
        // actively moving. When paused for a FATE/CE, allow Automator Return (e.g. camp for buffs).
        if (IsIllegalModeMapHuntFillerActive())
        {
            return StatePriority.Never;
        }

        // Pot chest farm / deferred handoff — open the reveal before Sight Return.
        if (memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _)
            || memory.TryRemember<PendingPotChestFarmMemory>(out PendingPotChestFarmMemory _))
        {
            return StatePriority.Never;
        }

        // After activity, get to camp for Treasure Sight before the next CE/FATE.
        if (memory.TryRemember<AutomaticTreasureSurveyMemory>(out AutomaticTreasureSurveyMemory survey)
            && survey.PendingSurvey
            && !zones.GetZone().IsInBasecamp())
        {
            return StatePriority.High;
        }

        if (!memory.TryRemember<IdleStateMemory>(out IdleStateMemory idle) || zones.GetZone().IsInBasecamp())
        {
            return StatePriority.Never;
        }

        // Waiting inside / near the goal FATE circle — don't Return-to-base.
        if (IsNearActiveFateGoal())
        {
            return StatePriority.Never;
        }

        // Committed to a CE (wait latch / SuspendTravel / live Preparing|Battle goal) — never
        // Opportunistic Return while Goal still shows that CE (e.g. Familiar / Unbridled).
        if (IsCommittedToCriticalEncounterGoal())
        {
            return StatePriority.Never;
        }

        // Raise nearby players before leaving the FATE/CE site.
        if (TriageSession.IsActive(memory))
        {
            return StatePriority.Never;
        }

        // Opportunistic Return while idle (OC has no Return CD). Keep below ChoosingActivity.
        return idle.IsReadyToReturn() ? StatePriority.VeryLow : StatePriority.Never;
    }

    public override void Enter()
    {
        base.Enter();
        autoRotation.DisableAi();
        addons.RegisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoListener);
    }

    public override void Handle()
    {
        if (!automator.Enabled || !zones.GetZone().IsOccultCrescentZone())
        {
            memory.Forget<ReturningStateMemory>();
            return;
        }

        if (conditions[ConditionFlag.Unconscious])
        {
            memory.Forget<ReturningStateMemory>();
            return;
        }

        // Gate: true = interval elapsed (was inverted before).
        if (!gate.Milliseconds(this, "ReturningHandler::Gate", 500))
        {
            return;
        }

        bool isCasting = conditions[ConditionFlag.Casting] || conditions[ConditionFlag.Casting87];
        bool isBetweenAreas = conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51];

        if (isCasting || isBetweenAreas)
        {
            return;
        }

        IZone zone = zones.GetZone();
        if (zone.IsInBasecamp())
        {
            memory.Forget<ReturningStateMemory>();
            return;
        }

        // Poll confirm — PostSetup alone can miss when BossMod slows UI setup.
        if (TryConfirmReturnDialog())
        {
            return;
        }

        if (IsReturnDialogVisible())
        {
            return;
        }

        // Path handoff: hold Returning while the rolled 2..max delay elapses.
        // Survey latch skips the humanize delay — get to camp for Sight ASAP.
        bool surveyLatch = memory.TryRemember<AutomaticTreasureSurveyMemory>(out AutomaticTreasureSurveyMemory latch)
                           && latch.PendingSurvey;
        if (memory.TryRemember<ReturningStateMemory>(out ReturningStateMemory returning)
            && !returning.IsReadyToCast()
            && !surveyLatch)
        {
            return;
        }

        if (Actions.Return.CanCast())
        {
            memory.TryAdd(new ReturningStateMemory(TimeSpan.Zero));
            Actions.Return.Cast();
            return;
        }

        // Fallback: some clients block Return on mount.
        DismountAssist.TryDismount(conditions);
    }

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);

        // The idle latch is spent once we leave Returning — either the Return cast, or something
        // (triage / a live FATE goal) pulled us off it and the next idle stretch rolls its own wait.
        memory.Forget<IdleStateMemory>();
        addons.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoListener);
    }

    private unsafe void SelectYesNoListener(AddonEvent ev, AddonArgs args)
    {
        if (!automator.Enabled
            || !zones.GetZone().IsOccultCrescentZone()
            || conditions[ConditionFlag.Unconscious])
        {
            return;
        }

        ReturnYesNo.TryAccept((AtkUnitBase*)args.Addon.Address);
    }

    private unsafe bool TryConfirmReturnDialog()
    {
        if (!automator.Enabled || !zones.GetZone().IsOccultCrescentZone())
        {
            return false;
        }

        if (!AddonHelpers.TryGetSelectYesno(out AddonSelectYesno* yesno))
        {
            return false;
        }

        return ReturnYesNo.TryAccept(&yesno->AtkUnitBase);
    }

    private unsafe bool IsReturnDialogVisible()
    {
        if (!AddonHelpers.TryGetSelectYesno(out AddonSelectYesno* yesno))
        {
            return false;
        }

        return ReturnYesNo.IsReturnConfirmation(&yesno->AtkUnitBase);
    }

    private bool IsIllegalModeMapHuntFillerActive()
    {
        // Paused = yielded to FATE/CE; Automator must be able to Return / buff / choose.
        if (hunter.ManagedByIllegalModeFiller && hunter.Running && !hunter.Paused)
        {
            return true;
        }

        return memory.TryRemember<AutomaticTreasureSurveyMemory>(out AutomaticTreasureSurveyMemory survey)
               && survey.PendingMapHunt;
    }

    private bool IsNearActiveFateGoal()
    {
        if (!memory.TryRemember<GoalMemory>(out GoalMemory goal) || goal.Goal.GoalType is not FateGoal fateGoal)
        {
            return false;
        }

        Fate? fate = fates.Snapshot().FirstOrDefault(f => f.Id.Value == fateGoal.id.Value);
        if (fate == null)
        {
            return false;
        }

        float radius = fate.Radius > 0f
            ? fate.Radius * 0.9f
            : NavigationConstants.EventArrivalRadius;
        return player.Position.Distance2D(fate.Position) <= radius;
    }

    private bool IsCommittedToCriticalEncounterGoal()
    {
        if (memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory _)
            || memory.TryRemember<SuspendTravelForActivityMemory>(out SuspendTravelForActivityMemory _))
        {
            return true;
        }

        if (!memory.TryRemember<GoalMemory>(out GoalMemory goal)
            || goal.Goal.GoalType is not CriticalEncounterGoal ceGoal)
        {
            return false;
        }

        CriticalEncounter? ce = criticalEncounters.SnapshotWithoutForkedTower()
            .FirstOrDefault(c => c.Id == ceGoal.id);
        return ce is { } encounter && (encounter.IsPreparing() || encounter.IsActive());
    }
}
