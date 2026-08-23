using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

/// <summary>
///     Settings that apply to every kind of travel — Illegal Mode, Treasure Hunt, Carrot Hunt and
///     Mob Farmer all read these. They lived on <see cref="AutomatorConfig"/>, which made them look
///     like Illegal Mode options and left people hunting for "jump when stuck" in the wrong page.
///     Illegal-Mode-only travel settings (StopAfterReturn, StayMountedWhileWaitingForCe) stay there.
/// </summary>
[Serializable]
[ConfigGroup("movement", GroupOrder = 10)]
public class MovementConfig : IAutoConfig
{
    /// <summary>Use Sprint on foot when closing in on an aetheryte.</summary>
    [Checkbox(Order = 0, Section = "mount")]
    public bool SprintOnAetheryteApproach { get; set; } = true;

    [Checkbox(Order = 1, Section = "mount")]
    public bool ShouldAutoMount { get; set; } = true;

    /// <summary>Preferred mount sheet row ID. 0 = Mount Roulette.</summary>
    [MountSelect(Order = 2, Section = "mount")]
    public uint PreferredMountId { get; set; } = 0;

    /// <summary>
    ///     Jump when movement stalls against rocks, ledges or stairs (#185).
    /// </summary>
    [Checkbox(Order = 3, Section = "unstuck")]
    public bool ShouldJumpWhenStuck { get; set; } = true;

    /// <summary>
        ///     Seconds without moving before jumping. Long enough not to fire on normal micro-stalls,
        ///     and well under the 20s at which pathing gives up on the route entirely.
        /// </summary>
        [IntRange(1, 15, Order = 4, Section = "unstuck")]
        public int JumpWhenStuckSeconds { get; set; } = 3;

        /// <summary>
        ///     Enable path conflict detection: check for other players on our active path and re-route if someone is ahead of us.
        /// </summary>
        [Checkbox(Order = 5, Section = "conflict")]
        public bool EnablePathConflictDetection { get; set; } = true;

        /// <summary>
        ///     Minimum time between path conflict checks (seconds).
        /// </summary>
        [IntRange(1, 30, Order = 6, Section = "conflict")]
        public int PathConflictCheckIntervalSeconds { get; set; } = 3;

        /// <summary>
        ///     Distance threshold (meters) — another player within this distance of any path waypoint is considered "on our path".
        /// </summary>
        [FloatRange(1f, 20f, Order = 7, Section = "conflict")]
        public float PathConflictDistanceThreshold { get; set; } = 5f;

        /// <summary>
        ///     How much closer to the destination the other player must be (meters) to trigger a re-route.
        ///     Prevents re-routing for players behind us or alongside us.
        /// </summary>
        [FloatRange(0f, 10f, Order = 8, Section = "conflict")]
        public float PathConflictAheadThreshold { get; set; } = 2f;
    }
