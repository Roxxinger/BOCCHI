using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Common.Data.CriticalEncounters;

public readonly record struct CriticalEncounterId(ushort Value)
{
    public override string ToString() => Value.ToString();
}

public class CriticalEncounter(
    CriticalEncounterId id,
    DynamicEvent ev,
    float radius,
    Vector3 fallbackPosition,
    ActivityAreaShape areaShape = ActivityAreaShape.Circle)
{
    private readonly Vector3 fallbackPosition = fallbackPosition;

    public readonly CriticalEncounterId Id = id;

    public readonly string Name = ev.Name.ToString();

    /// <summary>Padded size used for debug outer ring (circle radius or square half-extent).</summary>
    public float Radius { get; private set; } = radius;

    /// <summary>Unpadded wait radius after LGB sanitization.</summary>
    public float UnpaddedCombatRadius { get; private set; }

    public ActivityAreaShape AreaShape { get; private set; } = areaShape;

    /// <summary>Authored staging / travel aim. Not overwritten by LGB MapRange centre.</summary>
    public Vector3 Position { get; private set; } = ResolvePosition(ev, fallbackPosition);

    /// <summary>
    ///     Live registration centre from LGB (blue ring). Falls back to <see cref="Position"/> when
    ///     geometry is missing or looks wrong relative to authored staging.
    /// </summary>
    public Vector3 RegistrationCenter { get; private set; } = ResolvePosition(ev, fallbackPosition);

    public DynamicEventState State { get; private set; } = ev.State;

    public byte Progress { get; private set; } = ev.Progress;

    /// <summary>Unix seconds when registration/start is scheduled (game DynamicEvent).</summary>
    public int StartTimestamp { get; private set; } = ev.StartTimestamp;

    private static unsafe Vector3 TryReadLayoutPosition(DynamicEvent ev)
    {
        LayoutManager* layout = LayoutWorld.Instance()->ActiveLayout;
        if (layout == null)
        {
            return Vector3.NaN;
        }

        if (!layout->InstancesByType.TryGetValue(InstanceType.EventObject, out Pointer<StdMap<ulong, Pointer<ILayoutInstance>>> eventObjects, false))
        {
            return Vector3.NaN;
        }

        uint eventObjectId = ev.LGBEventObject;
        if (eventObjectId <= 0)
        {
            return Vector3.NaN;
        }

        Pointer<ILayoutInstance>? eventObject = eventObjects.Value->Values.FirstOrNull(e => e.Value->Id.InstanceKey == eventObjectId);
        if (eventObject == null)
        {
            return Vector3.NaN;
        }

        Transform* trans = eventObject.Value.Value->GetTransformImpl();
        Vector3 position = trans->Translation;

        return new(position.X, position.Y, position.Z);
    }

    private static Vector3 ResolvePosition(DynamicEvent ev, Vector3 fallbackPosition)
    {
        Vector3 live = TryReadLayoutPosition(ev);
        bool hasLive = !float.IsNaN(live.X);
        bool hasAuthored = !float.IsNaN(fallbackPosition.X);

        // Prefer authored — live LGB is often an entrance marker or under elevated CEs.
        if (hasAuthored)
        {
            return fallbackPosition;
        }

        return hasLive ? live : fallbackPosition;
    }

    public void Update(DynamicEvent ev)
    {
        State = ev.State;
        Progress = ev.Progress;
        StartTimestamp = ev.StartTimestamp;

        // Keep authored staging when we have it (elevated / square CEs); otherwise refresh live.
        if (!float.IsNaN(fallbackPosition.X))
        {
            Position = fallbackPosition;
            return;
        }

        Vector3 live = TryReadLayoutPosition(ev);
        if (!float.IsNaN(live.X))
        {
            Position = live;
            if (float.IsNaN(RegistrationCenter.X))
            {
                RegistrationCenter = live;
            }
        }
    }

    /// <summary>
    ///     Reject LGB centres this far from authored staging (wrong volume).
    /// </summary>
    public const float MaxRegistrationCenterSkew = 100f;

    /// <summary>
    ///     Reject LGB centres this far above/below authored staging (elevated MapRange vs ground ring).
    /// </summary>
    public const float MaxRegistrationElevationSkew = 20f;

    /// <summary>
    ///     Reject unpadded LGB radii above this (e.g. Eternal Watch ~560y).
    /// </summary>
    public const float MaxRegistrationRadius = 80f;

    /// <summary>
    ///     Fallback unpadded radius when LGB centre or size is rejected.
    /// </summary>
    public const float FallbackRegistrationRadius = 40f;

    /// <summary>
    ///     Choose wait centre + unpadded radius from live LGB vs authored staging.
    /// </summary>
    /// <param name="authoredCombatRadius">
    ///     When LGB is rejected, prefer this size over <see cref="FallbackRegistrationRadius"/>.
    /// </param>
    public static void SanitizeRegistration(
        Vector3 authoredStaging,
        Vector3 lgbCenter,
        float lgbRadius,
        out Vector3 center,
        out float radius,
        out bool rejected,
        float? authoredCombatRadius = null)
    {
        bool badCenter = !float.IsNaN(authoredStaging.X)
                         && (lgbCenter.Distance2D(authoredStaging) > MaxRegistrationCenterSkew
                             || MathF.Abs(lgbCenter.Y - authoredStaging.Y) > MaxRegistrationElevationSkew);
        bool badRadius = lgbRadius <= 0f || lgbRadius > MaxRegistrationRadius;
        rejected = badCenter || badRadius;
        if (rejected)
        {
            center = float.IsNaN(authoredStaging.X) ? lgbCenter : authoredStaging;
            float authored = authoredCombatRadius ?? 0f;
            radius = authored > 0f && authored <= MaxRegistrationRadius
                ? authored
                : FallbackRegistrationRadius;
            return;
        }

        center = lgbCenter;
        radius = lgbRadius;
    }

    /// <summary>Apply live LGB registration size and centre (unpadded combat radius).</summary>
    public void ApplyCombatGeometry(
        float combatRadius,
        ActivityAreaShape shape,
        Vector3? center = null,
        float? authoredCombatRadius = null)
    {
        AreaShape = shape;

        if (center is not { } lgb || float.IsNaN(lgb.X))
        {
            RegistrationCenter = Position;
            float authored = authoredCombatRadius ?? 0f;
            float size = combatRadius > 0f && combatRadius <= MaxRegistrationRadius
                ? combatRadius
                : authored > 0f && authored <= MaxRegistrationRadius
                    ? authored
                    : FallbackRegistrationRadius;
            UnpaddedCombatRadius = size;
            Radius = NavigationConstants.CriticalEncounterPaddedRadius(size, shape);
            return;
        }

        SanitizeRegistration(
            fallbackPosition,
            lgb,
            combatRadius,
            out Vector3 waitAt,
            out float sizeOk,
            out _,
            authoredCombatRadius);
        UnpaddedCombatRadius = sizeOk;
        RegistrationCenter = waitAt;
        Radius = NavigationConstants.CriticalEncounterPaddedRadius(sizeOk, shape);
    }

    public TimeSpan? GetTimeUntilStart()
    {
        if (StartTimestamp == 0)
        {
            return null;
        }

        TimeSpan remaining = DateTimeOffset.FromUnixTimeSeconds(StartTimestamp) - DateTimeOffset.UtcNow;
        return remaining;
    }

    public bool IsPreparing() => State is DynamicEventState.Register or DynamicEventState.Warmup;

    public bool IsActive() => State is DynamicEventState.Battle;
}
