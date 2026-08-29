using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;
using Ocelot.Config.Renderers.Enum;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 0, Order = 0)]
public class AutomatorConfig : IAutoConfig
{
    [Checkbox(Order = 0, Section = "activities")]
    public bool ShouldDoFates { get; set; } = true;

    [Checkbox(Order = 1, Section = "activities")]
    public bool PreferPotFates { get; set; } = false;

    [Checkbox(Order = 2, Section = "activities")]
    public bool ShouldFarmPotChests { get; set; } = false;

    [Checkbox(Order = 3, Section = "activities")]
    public bool ShouldPrepositionToPots { get; set; } = true;

    [Checkbox(Order = 4, Section = "activities")]
    public bool ShouldDoCriticalEncounters { get; set; } = true;

    /// <summary>
    ///     While still walking to a FATE, only switch to a CE when registration has this many
    ///     seconds or fewer left. 0 = switch as soon as a CE is up (old behaviour). Once you are
    ///     in the FATE, it still finishes first (#187).
    /// </summary>
    [IntRange(0, 180, Order = 5, Section = "activities")]
    public int LeaveFateTravelForCeSeconds { get; set; } = 90;

    /// <summary>
    ///     Illegal Mode combat automation: Wrath/RSR + BOCCHI AI, or full BossMod / BMR autorotation.
    /// </summary>
    [EnumSelect<CombatAutorotation, CombatAutorotationDisplay, CombatAutorotationFilter>(Order = 6, Section = "combat")]
    public CombatAutorotation CombatAutorotation { get; set; } = CombatAutorotation.WrathCombo;

    /// <summary>
    ///     When on, rebuild BOCCHI's BossMod FATE/CE presets from the settings below when they
    ///     change, Illegal Mode starts, or you change job / melee / ranged. When off, existing
    ///     presets are kept until you press Update presets.
    /// </summary>
    [BossModPresetOptions(Order = 7, Indent = 1, Section = "combat")]
    public bool UpdateBossModPresetsAutomatically { get; set; } = false;

    public bool BossModMaxDistanceByRole { get; set; } = true;

    public bool BossModMeleeOnHitbox { get; set; } = true;

    public float BossModMaxDistance { get; set; } = 15f;

    public float BossModMaxDistanceMelee { get; set; } = 2.6f;

    public float BossModMaxDistanceRanged { get; set; } = 15f;

    public BossModOverdodge BossModOverdodge { get; set; } = BossModOverdodge.None;

    public BossModMovementDelay BossModMovementDelay { get; set; } = BossModMovementDelay.None;

    public bool BossModSeparateDodgeDelay { get; set; } = false;

    public BossModMovementDelay BossModDodgeMovementDelay { get; set; } = BossModMovementDelay.None;

    /// <summary>
    ///     Stay mounted while a CE is preparing; dismount when it starts.
    /// </summary>
    [Checkbox(Order = 8, Section = "travel")]
    public bool StayMountedWhileWaitingForCe { get; set; } = false;

    /// <summary>
    ///     After FATE/CE: Return, teleport to the nearest aetheryte for the next activity, mount,
    ///     then stop — no auto-walk.
    /// </summary>
    [Checkbox(Order = 9, Section = "travel")]
    public bool StopAfterReturn { get; set; } = false;

    /// <summary>
    ///     When the current phantom job is maxed, switch to the next unlocked non-maxed job.
    /// </summary>
    [Checkbox(Order = 10, Section = "jobs")]
    public bool PhantomJobsLevelingMode { get; set; } = false;

    /// <summary>
    ///     After FATE/CE: if raisable corpses are nearby, raise with the selected phantom job then continue.
    ///     No bodies → no swap / no wait; Illegal Mode continues as usual.
    /// </summary>
    [Checkbox(Order = 11, Section = "triage")]
    public bool EnableTriageMode { get; set; } = false;

    /// <summary>Which phantom job Triage Mode swaps to for raises (falls back if not unlocked).</summary>
    [TriageRaiseJob(Order = 12, Indent = 1, Requires = nameof(EnableTriageMode), Section = "triage")]
    public TriageRaiseJobPreference PreferredTriageRaiseJob { get; set; } = TriageRaiseJobPreference.PhantomChemist;

    /// <summary>
    ///     Illegal Mode / Completionist: after CE/FATE, Sight (if known) then hunt, or map hunt
    ///     without Sight. Only Illegal Mode reads this, so it belongs here rather than on the
    ///     Treasure page where people configuring Illegal Mode would not find it.
    /// </summary>
    [Checkbox(Order = 13, Section = "treasure")]
    public bool EnableAutomaticTreasureHuntDuringIllegalMode { get; set; } = false;

    /// <summary>
    ///     Periodic camp Sight when auto-hunt is off. Auto-hunt casts Sight after FATE/CE instead.
    /// </summary>
    [Checkbox(
        Order = 14,
        Indent = 1,
        DisabledWhen = nameof(EnableAutomaticTreasureHuntDuringIllegalMode),
        Section = "treasure")]
    public bool ShouldCastTreasureSight { get; set; } = false;

    [IntRange(
        60,
        600,
        Order = 15,
        Indent = 2,
        Requires = nameof(ShouldCastTreasureSight),
        DisabledWhen = nameof(EnableAutomaticTreasureHuntDuringIllegalMode),
        Section = "treasure")]
    public int TreasureSightRecastIntervalSeconds { get; set; } = 120;

    /// <summary>Max random idle before Return; 0 delay when Treasure Sight is latched.</summary>
    [IntRange(2, 60, Order = 16, Section = "delays")]
    public int MaxRemoteIdleTimeSeconds { get; set; } = 10;

    /// <summary>
    ///     Upper bound (seconds) for a random 0..max idle at camp before teleporting to a FATE/CE.
    ///     0 = leave immediately.
    /// </summary>
    [IntRange(0, 60, Order = 17, Section = "delays")]
    public int MaxBaseTeleportDelaySeconds { get; set; } = 0;

    /// <summary>
    ///     Repair equipped gear when any piece falls to or below this condition (%).
    /// </summary>
    [IntRange(1, 99, Order = 18, Section = "repair")]
    public int AutoRepairThreshold { get; set; } = 30;

    /// <summary>Self-repair vs nearby mender at base camp.</summary>
    [EnumSelectDisplay<AutoRepairMethod, AutoRepairMethodDisplay>(Order = 19, Section = "repair")]
    public AutoRepairMethod AutoRepairMethod { get; set; } = AutoRepairMethod.SelfRepair;

    // Path conflict detection & humanizing randomization (AOCCH parity)
    /// <summary>
    ///     Radius (meters) of a random 2D offset applied to each pathfind target so
    ///     loops approach from slightly different angles instead of retracing one
    ///     exact line. 0 = disabled. Kept small so the player always lands inside the
    ///     activity's interaction radius.
    /// </summary>
    [FloatRange(0f, 6f, Order = 20, Section = "pathing")]
    public float PathJitterRadius { get; set; } = 2f;

    /// <summary>
    ///     Arrival range (meters) passed to vnavmesh as DistanceThreshold for jittered
    ///     pathfind targets. vnavmesh then picks the final leg itself instead of marching
    ///     to the exact point — more natural approach, no mesh-edge risk.
    ///     0 = arrive exactly (disabled).
    /// </summary>
    [FloatRange(0f, 6f, Order = 21, Section = "pathing")]
    public float PathArrivalRange { get; set; } = 2f;

    /// <summary>
    ///     Number of top-cost path candidates to consider for random selection instead of
    ///     always taking the absolute minimum. 1 = deterministic (current behavior).
    ///     2-5 = pick randomly among the best N paths, breaking identical routes across users.
    ///     0 = disabled.
    /// </summary>
    [IntRange(0, 10, Order = 22, Section = "pathing")]
    public int PathDiversityTopK { get; set; } = 1;

    /// <summary>
    ///     Master switch for humanizing randomization. When enabled, the values below
    ///     are randomized within their min/max ranges each time Illegal Mode starts,
    ///     a CE begins, or a FATE begins.
    /// </summary>
    [Checkbox(Order = 23, Section = "randomization")]
    public bool EnableRandomization { get; set; } = false;

    /// <summary>Minimum Overdodge AoE setting for randomization.</summary>
        [EnumSelect<BossModOverdodge, BossModOverdodgeDisplay, NoOpFilter<BossModOverdodge>>(Order = 24, Section = "randomization")]
        public BossModOverdodge RandomOverdodgeMin { get; set; } = BossModOverdodge.None;

        /// <summary>Maximum Overdodge AoE setting for randomization.</summary>
        [EnumSelect<BossModOverdodge, BossModOverdodgeDisplay, NoOpFilter<BossModOverdodge>>(Order = 25, Section = "randomization")]
        public BossModOverdodge RandomOverdodgeMax { get; set; } = BossModOverdodge.Large;

    /// <summary>Minimum Delayed Movement setting for randomization.</summary>
        [EnumSelect<BossModMovementDelay, BossModMovementDelayDisplay, NoOpFilter<BossModMovementDelay>>(Order = 26, Section = "randomization")]
        public BossModMovementDelay RandomDelayedMin { get; set; } = BossModMovementDelay.None;

        /// <summary>Maximum Delayed Movement setting for randomization.</summary>
        [EnumSelect<BossModMovementDelay, BossModMovementDelayDisplay, NoOpFilter<BossModMovementDelay>>(Order = 27, Section = "randomization")]
        public BossModMovementDelay RandomDelayedMax { get; set; } = BossModMovementDelay.Long;

    /// <summary>Minimum melee target range (meters) for randomization.</summary>
    [FloatRange(1.1f, 30f, Order = 28, Section = "randomization")]
    public float RandomMeleeRangeMin { get; set; } = 1.1f;

    /// <summary>Maximum melee target range (meters) for randomization.</summary>
    [FloatRange(1.1f, 30f, Order = 29, Section = "randomization")]
    public float RandomMeleeRangeMax { get; set; } = 5f;

    /// <summary>Minimum ranged target range (meters) for randomization.</summary>
    [FloatRange(1.1f, 30f, Order = 30, Section = "randomization")]
    public float RandomRangedRangeMin { get; set; } = 15f;

    /// <summary>Maximum ranged target range (meters) for randomization.</summary>
    [FloatRange(1.1f, 30f, Order = 31, Section = "randomization")]
    public float RandomRangedRangeMax { get; set; } = 30f;

    /// <summary>Randomization seed. 0 = time-based (different each run).</summary>
    [IntRange(0, int.MaxValue, Order = 32, Section = "randomization")]
    public int RandomizationSeed { get; set; } = 0;

    /// <summary>
    ///     Applies randomization to the current config values based on the min/max ranges.
    ///     Called when Illegal Mode starts, a CE begins, or a FATE begins.
    /// </summary>
    public void ApplyRandomization(bool isMelee, Random? rng = null)
    {
        if (!EnableRandomization) return;

        rng ??= new Random(RandomizationSeed == 0 ? Environment.TickCount : RandomizationSeed);

        // Randomize Overdodge AoE
        var overdodgeCount = Enum.GetValues<BossModOverdodge>().Length;
        var overdodgeMin = Math.Clamp((int)RandomOverdodgeMin, 0, overdodgeCount - 1);
        var overdodgeMax = Math.Clamp((int)RandomOverdodgeMax, overdodgeMin, overdodgeCount - 1);
        BossModOverdodge = (BossModOverdodge)rng.Next(overdodgeMin, overdodgeMax + 1);

        // Randomize Delayed Movement
        var delayedCount = Enum.GetValues<BossModMovementDelay>().Length;
        var delayedMin = Math.Clamp((int)RandomDelayedMin, 0, delayedCount - 1);
        var delayedMax = Math.Clamp((int)RandomDelayedMax, delayedMin, delayedCount - 1);
        BossModMovementDelay = (BossModMovementDelay)rng.Next(delayedMin, delayedMax + 1);

        // Randomize Melee/Ranged Target Range based on job type
        var meleeMin = Math.Clamp(RandomMeleeRangeMin, 1.1f, 30f);
        var meleeMax = Math.Clamp(RandomMeleeRangeMax, meleeMin, 30f);
        var rangedMin = Math.Clamp(RandomRangedRangeMin, 1.1f, 30f);
        var rangedMax = Math.Clamp(RandomRangedRangeMax, rangedMin, 30f);

        if (isMelee)
        {
            var range = (double)meleeMin + rng.NextDouble() * (double)(meleeMax - meleeMin);
            BossModMaxDistanceMelee = (float)Math.Round(range, 1);
        }
        else
        {
            var range = (double)rangedMin + rng.NextDouble() * (double)(rangedMax - rangedMin);
            BossModMaxDistanceRanged = (float)Math.Round(range, 1);
        }
    }
}