using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services.Goals;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;

namespace BOCCHI.Automator.Services;

/// <summary>
///     Whether Illegal Mode would pick a live CE or FATE right now (not pot preposition).
/// </summary>
public interface IIllegalModeStartableActivityProbe
{
    bool HasStartableFateOrCriticalEncounter();
}

public sealed class IllegalModeStartableActivityProbe(
    IStartableCriticalEncounterFinder startableCriticalEncounters,
    IFateRepository fateRepository,
    IFateScorer fateScorer,
    IPotCycleTracker potCycle,
    IZoneProvider zones,
    IAutomatorContext automatorContext,
    IFieldNoteTracker fieldNotes,
    AutomatorConfig automatorConfig,
    FatesConfig fatesConfig,
    PotsConfig potsConfig
) : IIllegalModeStartableActivityProbe
{
    public bool HasStartableFateOrCriticalEncounter()
    {
        if (!automatorContext.IsPotsAndTreasure
            && startableCriticalEncounters.FindStartable() != null)
        {
            return true;
        }

        return FindStartableFate() != null;
    }

    /// <summary>Mirrors ChoosingActivityHandler live-FATE selection (no preposition).</summary>
    private Fate? FindStartableFate()
    {
        IReadOnlyList<Fate> snapshot = fateRepository.Snapshot();
        if (snapshot.Count == 0)
        {
            return null;
        }

        bool potsOnly = automatorContext.IsPotsAndTreasure;
        if (!potsOnly && !automatorConfig.ShouldDoFates)
        {
            return null;
        }

        Fate? bestPot = null;
        Fate? bestOther = null;
        float bestPotScore = float.MinValue;
        float bestOtherScore = float.MinValue;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        PotCycleSnapshot cycle = potCycle.Snapshot;
        bool potFarming = potsOnly
            || fatesConfig.IsPotFallbackGatingEnabled(
                (uint)cycle.PredictedNextPotFateId,
                automatorConfig.ShouldDoFates,
                automatorConfig.PreferPotFates,
                automatorConfig.ShouldFarmPotChests,
                automatorConfig.ShouldPrepositionToPots);
        IZone zone = zones.GetZone();

        foreach (Fate fate in snapshot)
        {
            bool isPot = zone.IsPotFate(fate.Id.Value);
            if (isPot)
            {
                if (!LivePotPriority.IsStartable(
                        fate,
                        zone,
                        automatorConfig,
                        fatesConfig,
                        potsConfig,
                        automatorContext,
                        fieldNotes))
                {
                    continue;
                }
            }
            else if (potsOnly)
            {
                continue;
            }
            else if (!fatesConfig.IsFateEnabledForIllegalMode(
                         fate.Id.Value,
                         isPotFate: false,
                         automatorConfig.PreferPotFates))
            {
                continue;
            }
            else if (automatorContext.IsCompletionist && !fieldNotes.ShouldPursueFate(fate.Id.Value))
            {
                continue;
            }
            else
            {
                TimeSpan cutoff = TimeSpan.FromMinutes(Math.Max(0, potsConfig.FateFallbackCutoffMinutes));
                PotFallbackStartDecision decision = PotFallbackWindow.Evaluate(
                    cycle,
                    now,
                    cutoff,
                    potFarming,
                    "FATE");
                if (!decision.AllowStart)
                {
                    continue;
                }
            }

            float scoreValue = potsOnly && isPot
                ? Math.Max(1f, fateScorer.Score(fate).Value)
                : fateScorer.Score(fate).Value;
            if (scoreValue <= 0f)
            {
                continue;
            }

            if (isPot)
            {
                if (scoreValue > bestPotScore)
                {
                    bestPotScore = scoreValue;
                    bestPot = fate;
                }
            }
            else if (scoreValue > bestOtherScore)
            {
                bestOtherScore = scoreValue;
                bestOther = fate;
            }
        }

        return bestPot ?? bestOther;
    }
}
