using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using System.Numerics;
using Path = Ocelot.Services.Pathfinding.Path;

namespace BOCCHI.Common.Data.Zones.Graph;

/// <summary>How CE join / combat area is measured around <see cref="ActivityData.Position"/>.</summary>
public enum ActivityAreaShape
{
    /// <summary>Euclidean radius (LGB MapRange for CEs).</summary>
    Circle = 0,

    /// <summary>Axis-aligned square; size is half-extent (center → edge).</summary>
    Square = 1,
}

/// <param name="Position">Path / wait destination (CE staging or FATE start).</param>
/// <param name="PreferredAethernetId">PlaceNameId of preferred inbound shard, if any.</param>
/// <param name="AreaShape">
///     Circle (default) or axis-aligned square. Wins over LGB TriggerBoxShape when a CE row
///     exists (authored squares today: A Beast Unleashed, Cursed Resurgence).
/// </param>
/// <param name="StandRadius">
///     Standable area when smaller than the registration rim. Null uses the live LGB size.
/// </param>
/// <param name="CombatRadius">
///     Authored registration size when live LGB is missing or rejected (Eternal Watch's elevated
///     MapRange is ~560y; authored stand is the walkable platform). Null uses the shared 40y fallback.
/// </param>
public record ActivityData(
    int Id,
    Vector3 Position,
    uint? PreferredAethernetId = null,
    ActivityAreaShape AreaShape = ActivityAreaShape.Circle,
    float? StandRadius = null,
    float? CombatRadius = null);

public record CarrotData(int Id, Vector3 Position, int Level);

public record TreasureData(int Id, int Level, Vector3? Position = null)
{
    private const float PositionMatchDistanceSquared = 4f;

    public bool Matches(uint treasureRowId, Vector3 worldPosition) =>
        Id == treasureRowId
        || Position is { } position && Vector3.DistanceSquared(position, worldPosition) <= PositionMatchDistanceSquared;

    /// <summary>
    ///     Resolve enemy level for a layout pad (id first, then nearest authored position).
    /// </summary>
    public static bool TryResolveLevel(
        uint layoutId,
        Vector3 layoutPosition,
        IReadOnlyList<TreasureData> treasureData,
        out int level)
    {
        TreasureData? byId = treasureData.FirstOrDefault(entry => entry.Id == layoutId);
        if (byId != null)
        {
            level = byId.Level;
            return true;
        }

        TreasureData? nearest = null;
        float nearestSq = float.MaxValue;
        foreach (TreasureData entry in treasureData)
        {
            if (entry.Position is not { } position)
            {
                continue;
            }

            float distSq = Vector3.DistanceSquared(position, layoutPosition);
            if (distSq > PositionMatchDistanceSquared || distSq >= nearestSq)
            {
                continue;
            }

            nearestSq = distSq;
            nearest = entry;
        }

        if (nearest != null)
        {
            level = nearest.Level;
            return true;
        }

        level = 0;
        return false;
    }
}

public record PotChestData(Vector3 Position, int Level);

public class GraphConfig(IPathfinder pathfinder, ILogger logger)
{
#if DEBUG
    public static readonly List<List<Vector3>> DebugPathLines = [];
#endif

    public float TeleportCost { get; init; } = 10f;

    public async Task<float> GetWalkingCost(Vector3 from, Vector3 to)
    {
        logger.Debug($"Calculating walking cost (from = {from:f2}, to = {to:f2})");
        Path result = await pathfinder.Pathfind(new(to)
        {
            From = from,
            AllowFlying = false
        });

#if DEBUG
        DebugPathLines.Add(result.Nodes.ToList());
#endif

        return result.CostOrUnreachable();
    }

    public async Task<float> GetWalkingCost(Node from, Node to) => await GetWalkingCost(from.Position, to.Position);
}

/// <summary>
///     vnav reports <c>Distance 0</c> for a path it could not build (fewer than two nodes).
///     Treat that as unreachable so traversal does not prefer a failed path as free.
/// </summary>
public static class PathReachability
{
    public static bool IsReachable(this Path path) => path.Nodes.Count >= 2;

    /// <summary>Path cost, or <see cref="float.PositiveInfinity"/> when vnav could not reach it.</summary>
    public static float CostOrUnreachable(this Path path) =>
        path.IsReachable() ? path.Distance : float.PositiveInfinity;
}
