using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones;

public static class NavigationConstants
{
    public const float MaxDirectWalkDistance = 80f;

    /// <summary>
    ///     Yalm-equivalent cost of casting Return. Every route planner shares these two so a hop is
    ///     priced the same whether the treasure hunt, the carrot hunt or graph traversal is asking.
    /// </summary>
    public const float ReturnCost = 40f;

    /// <summary>
    ///     Yalm-equivalent cost of one aethernet hop. A hop's teleport chain takes roughly 2.5s,
    ///     which is about 50 yalms of mounted travel — graph traversal previously priced it at 10
    ///     and so reached for teleports on hops it could comfortably walk.
    /// </summary>
    public const float AethernetHopCost = 50f;

    /// <summary>Player is considered at base camp within this distance of the aetheryte.</summary>
    public const float CampRadius = 80f;

    /// <summary>Stop this close to FATE/CE center so we enter the engagement circle.</summary>
    public const float EventApproachMinRadius = 0f;

    public const float EventApproachMaxRadius = 5f;

    /// <summary>Angular jitter (degrees) for FATE stand-off so clients don't stack on one ray.</summary>
    public const float EventApproachJitter = 50f;

    /// <summary>PathCalculator treats this as "arrived at event" — must be ≥ max approach.</summary>
    public const float EventArrivalRadius = 5f;

    /// <summary>
    ///     Yield FATE travel to BossMod AI once this close to a FATE enemy (yalms past hitbox).
    ///     Registration is the rim of a large circle — too far for AutoTarget / StayCloseToTarget.
    /// </summary>
    public const float FateAiHandoffRange = 25f;

    /// <summary>No FATE enemies up yet: yield once this close to the live FATE centre.</summary>
    public const float FateAiHandoffFromCenter = 25f;

    /// <param name="nearestTargetPastHitbox">
    ///     Distance past hitbox to the nearest FATE enemy, or <see cref="float.MaxValue"/> if none.
    /// </param>
    public static bool IsWithinFateAiHandoff(float distanceToCenter, float nearestTargetPastHitbox) =>
        nearestTargetPastHitbox <= FateAiHandoffRange
        || distanceToCenter <= FateAiHandoffFromCenter;

    /// <summary>
    ///     Added to LGB CE combat radius for debug green.
    ///     Red debug = padded − this (the in-game blue registration edge).
    /// </summary>
    public const float CriticalEncounterRadiusPadding = 7f;

    /// <summary>Yellow debug ring inset from padded CE radius (green − this).</summary>
    public const float CriticalEncounterYellowInset = 2f;

    /// <summary>Square CEs: cyan stand (path target) as a fraction of red half-extent.</summary>
    public const float CriticalEncounterSquareStandRatio = 0.7f;

    /// <summary>Circle CEs: cyan stand ring (path target while waiting), as a fraction of red.</summary>
    public const float CriticalEncounterCircleStandRatio = 0.45f;

    /// <summary>Debug green pad beyond red for square CEs (same idea as circle pad).</summary>
    public const float CriticalEncounterSquareRadiusPadding = 7f;

    /// <summary>
    ///     Inset from the blue registration edge. Waiting / arrival on the rim does not place you
    ///     into the instance (Tiny Terror, A Beast Unleashed).
    /// </summary>
    public const float CriticalEncounterWaitInset = 8f;

    /// <summary>Circle travel stand-off around the cyan ring — closer to centre than the rim.</summary>
    public const float CriticalEncounterApproachMinRatio = 0.25f;

    /// <summary>Circle travel stand-off outer (≤ stand ratio).</summary>
    public const float CriticalEncounterApproachMaxRatio = 0.4f;

    /// <summary>Square CEs: max Chebyshev stand-off from center as a fraction of half-extent.</summary>
    public const float CriticalEncounterSquareApproachMaxRatio = 0.25f;

    /// <summary>
    ///     Registration size/centre still come from LGB. Shape prefers the zone CE table when we
    ///     have a row — LGB <c>TriggerBoxShape</c> can disagree with the blue ring (e.g. Lost on
    ///     the Wind is a circle). Authored squares (A Beast Unleashed, Cursed Resurgence, Dark
    ///     Artistry) stay square.
    /// </summary>
    public static ActivityAreaShape ResolveCriticalEncounterShape(ActivityData? authored, bool lgbIsSquare) =>
        authored is not null
            ? authored.AreaShape
            : lgbIsSquare
                ? ActivityAreaShape.Square
                : ActivityAreaShape.Circle;

    public static ActivityAreaShape ResolveCriticalEncounterShape(IZone zone, int eventId, bool lgbIsSquare)
    {
        ActivityData? authored = zone.GetCriticalEncounterData().FirstOrDefault(a => a.Id == eventId);
        return ResolveCriticalEncounterShape(authored, lgbIsSquare);
    }

    /// <summary>Random stand-off ring while waiting for a predicted pot FATE.</summary>
    public const float PotPrepositionMinRadius = 12f;

    public const float PotPrepositionMaxRadius = 32f;

    /// <summary>Euclidean distance above which long pathfinds should mount first.</summary>
    public const float MountMinDistance = 20f;

    /// <summary>Red debug / combat radius from padded <c>ce.Radius</c>.</summary>
    public static float CriticalEncounterRedRadius(
        float paddedRadius,
        ActivityAreaShape shape = ActivityAreaShape.Circle)
    {
        float pad = shape == ActivityAreaShape.Square
            ? CriticalEncounterSquareRadiusPadding
            : CriticalEncounterRadiusPadding;
        return MathF.Max(0f, paddedRadius - pad);
    }

    /// <summary>Yellow debug radius from padded <c>ce.Radius</c>.</summary>
    public static float CriticalEncounterYellowRadius(float paddedRadius) =>
        MathF.Max(0f, paddedRadius - CriticalEncounterYellowInset);

    /// <summary>Cyan debug / preferred stand size (inside red).</summary>
    public static float CriticalEncounterStandRadius(float combatRadius, ActivityAreaShape shape)
    {
        if (combatRadius <= 0f)
        {
            return EventArrivalRadius;
        }

        float ratio = shape == ActivityAreaShape.Square
            ? CriticalEncounterSquareStandRatio
            : CriticalEncounterCircleStandRatio;
        return combatRadius * ratio;
    }

    /// <summary>Padded outer (green) size from LGB combat radius.</summary>
    public static float CriticalEncounterPaddedRadius(float combatRadius, ActivityAreaShape shape)
    {
        float pad = shape == ActivityAreaShape.Square
            ? CriticalEncounterSquareRadiusPadding
            : CriticalEncounterRadiusPadding;
        return combatRadius + pad;
    }

    /// <summary>
    ///     True when <paramref name="point"/> is inside the CE wait area — the blue registration
    ///     ring/box, inset by <see cref="CriticalEncounterWaitInset"/> so travel does not stop on
    ///     the rim. <paramref name="combatRadius"/> is the LGB size (circle radius or square half-extent).
    /// </summary>
    public static bool IsInsideCriticalEncounterWaitArea(
        Vector3 center,
        float combatRadius,
        ActivityAreaShape shape,
        Vector3 point)
    {
        if (combatRadius <= 0f)
        {
            return false;
        }

        float wait = MathF.Max(EventArrivalRadius, combatRadius - CriticalEncounterWaitInset);
        if (wait > combatRadius)
        {
            wait = combatRadius;
        }

        return IsInsideCriticalEncounterArea(center, wait, shape, point);
    }

    /// <summary>
    ///     True inside the full registration edge (red debug), with no wait inset.
    ///     Use after arrival so waiting is not yanked back from the stand ring toward the rim.
    /// </summary>
    public static bool IsInsideCriticalEncounterRegistrationArea(
        Vector3 center,
        float combatRadius,
        ActivityAreaShape shape,
        Vector3 point) =>
        combatRadius > 0f && IsInsideCriticalEncounterArea(center, combatRadius, shape, point);

    private static bool IsInsideCriticalEncounterArea(
        Vector3 center,
        float radiusOrHalfExtent,
        ActivityAreaShape shape,
        Vector3 point)
    {
        if (shape == ActivityAreaShape.Square)
        {
            float dx = MathF.Abs(point.X - center.X);
            float dz = MathF.Abs(point.Z - center.Z);
            return MathF.Max(dx, dz) <= radiusOrHalfExtent;
        }

        return point.Distance2D(center) <= radiusOrHalfExtent;
    }
}

public static class NavigationApproach
{
    public static Vector3 GetEventPosition(Vector3 destination, Vector3 from)
    {
        float range = NavigationConstants.EventApproachMinRadius
                      + Random.Shared.NextSingle() * (NavigationConstants.EventApproachMaxRadius - NavigationConstants.EventApproachMinRadius);

        return destination.GetApproachPosition(from, range, NavigationConstants.EventApproachJitter);
    }

    /// <summary>Random point inside the combat area so travel lands on the blue registration zone.</summary>
    /// <param name="standRadius">
    ///     Standable radius when tighter than <paramref name="combatRadius"/>; 0 to use the
    ///     registration rim. The rim can extend past the ground you can actually stand on.
    /// </param>
    public static Vector3 GetCriticalEncounterApproachPosition(
        Vector3 center,
        float combatRadius,
        ActivityAreaShape shape = ActivityAreaShape.Circle,
        float standRadius = 0f,
        int? stableSeed = null)
    {
        Random rng = stableSeed is int seed
            ? new Random(HashCode.Combine(seed, 0xCE))
            : Random.Shared;

        float red = MathF.Max(1f, standRadius > 0f ? standRadius : combatRadius);
        if (shape == ActivityAreaShape.Square)
        {
            // Squares (e.g. A Beast Unleashed): scatter inside the blue box — not one approach ray.
            float maxFromCenter = MathF.Min(
                red * NavigationConstants.CriticalEncounterSquareApproachMaxRatio,
                NavigationConstants.EventApproachMaxRadius);
            if (maxFromCenter < 0.5f)
            {
                maxFromCenter = 0.5f;
            }

            float x = (rng.NextSingle() * 2f - 1f) * maxFromCenter;
            float z = (rng.NextSingle() * 2f - 1f) * maxFromCenter;
            return center + new Vector3(x, 0f, z);
        }

        float min = red * NavigationConstants.CriticalEncounterApproachMinRatio;
        float max = red * NavigationConstants.CriticalEncounterApproachMaxRatio;
        if (max < min)
        {
            max = min;
        }

        // Scatter on the disc. An inbound ray from the aethernet often lands on a ramp outside the ring.
        float dist = min + rng.NextSingle() * (max - min);
        float angle = rng.NextSingle() * MathF.PI * 2f;
        return center + new Vector3(MathF.Cos(angle) * dist, 0f, MathF.Sin(angle) * dist);
    }

    public static Vector3 ResolveActivityApproach(Node goal, Vector3 from)
    {
        if (goal.Type == NodeType.CriticalEncounter
            && goal.Metadata is ActivityNodeMetadata { CombatRadius: > 0 } meta)
        {
            float radius = meta.CombatRadius > CriticalEncounter.MaxRegistrationRadius
                ? CriticalEncounter.FallbackRegistrationRadius
                : meta.CombatRadius;
            return GetCriticalEncounterApproachPosition(
                goal.Position,
                radius,
                meta.AreaShape,
                meta.StandRadius);
        }

        return GetEventPosition(goal.Position, from);
    }

    /// <summary>
    ///     World / non-Illegal PathTo: use CE inner stand-off when the destination is a known CE.
    /// </summary>
    public static bool TryResolveCriticalEncounterApproach(
        IZone zone,
        CriticalEncounterGeometry? geometry,
        Vector3 destination,
        Vector3 from,
        out Vector3 approach,
        out ActivityData? activity,
        out bool alreadyInside)
    {
        alreadyInside = false;
        const float matchRadius = 80f;
        foreach (ActivityData candidate in zone.GetCriticalEncounterData())
        {
            if (destination.Distance2D(candidate.Position) > matchRadius)
            {
                continue;
            }

            if (geometry?.TryResolveForAuthored(
                    (ushort)candidate.Id,
                    candidate.Position,
                    out _) is not { Radius: > 0 } area)
            {
                continue;
            }

            activity = candidate;
            ActivityAreaShape shape = NavigationConstants.ResolveCriticalEncounterShape(
                candidate,
                area.IsSquare);
            CriticalEncounter.SanitizeRegistration(
                candidate.Position,
                area.Center,
                area.Radius,
                out Vector3 center,
                out float radius,
                out _,
                candidate.CombatRadius);

            if (NavigationConstants.IsInsideCriticalEncounterWaitArea(
                    center, radius, shape, from))
            {
                approach = from;
                alreadyInside = true;
                return true;
            }

            approach = GetCriticalEncounterApproachPosition(
                center, radius, shape, candidate.StandRadius ?? 0f);
            return true;
        }

        activity = null;
        approach = default;
        return false;
    }

    public static Vector3 GetPotPrepositionPosition(Vector3 potCenter, Vector3 from)
    {
        float dist = from.Distance2D(potCenter);
        if (dist >= NavigationConstants.PotPrepositionMinRadius
            && dist <= NavigationConstants.PotPrepositionMaxRadius)
        {
            return from;
        }

        float range = NavigationConstants.PotPrepositionMinRadius
                      + Random.Shared.NextSingle()
                      * (NavigationConstants.PotPrepositionMaxRadius - NavigationConstants.PotPrepositionMinRadius);
        float angle = Random.Shared.NextSingle() * MathF.PI * 2f;

        return potCenter + new Vector3(MathF.Cos(angle) * range, 0f, MathF.Sin(angle) * range);
    }
}
