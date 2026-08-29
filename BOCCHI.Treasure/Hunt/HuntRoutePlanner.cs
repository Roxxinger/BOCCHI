using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;
using System.Text.Json;

namespace BOCCHI.Treasure.Hunt;

public interface IHuntRoutePlanner
{
    HuntPathfinderState State { get; }

    /// <summary>Authored segment ids in route order; empty when there is no authored route.</summary>
    IReadOnlyList<string> SegmentIds { get; }

    /// <summary>Authored segment owning this pad, or null when the pad is not in the route.</summary>
    string? TryGetNodeSegment(uint nodeId);

    /// <summary>Authored route order index (0-based), or null if not in treasure_route.json.</summary>
    int? TryGetNodeOrderIndex(uint nodeId);

    /// <summary>First authored pad of a segment, or null when the id is unknown.</summary>
    uint? TryGetSegmentFirstNode(string segmentId);

    /// <summary>
    ///     Camp entry to <paramref name="toNodeId"/>: Return (unless already in camp), then best
    ///     aethernet + walk, or walk from camp when that is cheaper.
    /// </summary>
    List<HuntPathfinderStep> BuildEntryLeg(uint toNodeId, bool alreadyInCamp = false);

    /// <param name="preferStartNodes">
    ///     When set, visit these remaining pads first (closest Nearby peel-off chain), then the rest.
    /// </param>
    /// <param name="continueAfterNodeId">
    ///     Last finished pad. Authored routes resume at the next pad after this instead of the
    ///     geographically nearest remaining one.
    /// </param>
    /// <param name="entryNodeId">
    ///     Forces the tour to start here and wrap (session-start segment rotation). Wins over
    ///     <paramref name="continueAfterNodeId"/>.
    /// </param>
    Task<List<HuntPathfinderStep>> FindPath(
        Vector3 start,
        List<uint> nodes,
        IReadOnlyList<uint>? preferStartNodes = null,
        uint? continueAfterNodeId = null,
        uint? entryNodeId = null);
}

/// <summary>
///     Routes remaining coffers via authored treasure_route.json (v2) when present; otherwise
///     open-path nearest-neighbor TSP. Re-solved on every FindPath.
/// </summary>
public abstract class HuntRoutePlanner
(
    ZoneId zoneId,
    IDalamudPluginInterface plugin,
    IPluginLog log
) : IHuntRoutePlanner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Parsed hunt data cached per zone per session.</summary>
    private static readonly Dictionary<(ZoneId Zone, string File), HuntNodeDataSchema> NodeDataCache = [];

    private static readonly Dictionary<ZoneId, AuthoredRoutePayload> AuthoredRouteCache = [];

    /// <summary>Parsed treasure_route.json, cached per zone.</summary>
    private readonly record struct AuthoredRoutePayload(
        List<AuthoredRouteEntry> Entries,
        List<AuthoredRouteSegment> Segments);

    private HuntNodeDataSchema data = new();

    /// <summary>Flattened authored pads in order; empty when falling back to TSP.</summary>
    private List<AuthoredRouteEntry> authoredEntries = [];

    /// <summary>Authored segments in route order; indexed by <see cref="AuthoredRouteEntry.SegmentIndex"/>.</summary>
    private List<AuthoredRouteSegment> authoredSegments = [];

    public IReadOnlyList<string> SegmentIds => authoredSegments.Select(seg => seg.Id).ToList();

    private HuntAethernet BaseCampAethernet => zoneId switch
    {
        ZoneId.NorthHorn => HuntAethernet.NorthHornBaseCamp,
        _ => HuntAethernet.BaseCamp,
    };

    public HuntPathfinderState State { get; private set; } = HuntPathfinderState.None;

    public Task<List<HuntPathfinderStep>> FindPath(
        Vector3 start,
        List<uint> nodes,
        IReadOnlyList<uint>? preferStartNodes = null,
        uint? continueAfterNodeId = null,
        uint? entryNodeId = null)
    {
        if (State != HuntPathfinderState.FileLoaded && State != HuntPathfinderState.PathfindingDone)
        {
            throw new InvalidOperationException("Hunt route data not loaded");
        }

        State = HuntPathfinderState.Pathfinding;

        List<uint> remaining = nodes.Distinct().ToList();
        if (remaining.Count == 0)
        {
            State = HuntPathfinderState.PathfindingDone;
            return Task.FromResult(new List<HuntPathfinderStep>());
        }

        List<uint> preferPrefix = [];
        if (preferStartNodes != null)
        {
            HashSet<uint> seen = [];
            foreach (uint id in preferStartNodes)
            {
                if (remaining.Contains(id) && seen.Add(id))
                {
                    preferPrefix.Add(id);
                }
            }
        }

        uint? primaryPrefer = preferPrefix.Count > 0 ? preferPrefix[0] : null;
        List<uint> tour = authoredEntries.Count > 0
            ? BuildAuthoredTour(start, remaining, primaryPrefer, continueAfterNodeId, entryNodeId)
            : BuildTspTour(start, remaining, primaryPrefer);

        if (preferPrefix.Count > 0)
        {
            // Closest Nearby chain first, then the rest of the authored/TSP tour.
            HashSet<uint> prefixSet = preferPrefix.ToHashSet();
            tour = preferPrefix.Concat(tour.Where(id => !prefixSet.Contains(id))).ToList();
        }

        if (tour.Count == 0)
        {
            State = HuntPathfinderState.PathfindingDone;
            return Task.FromResult(new List<HuntPathfinderStep>());
        }

        log.Debug(
            authoredEntries.Count > 0
                ? "Treasure hunt authored route: {Count} remaining (start {Start}, nearbyPrefix {Prefix}, segment {Segment})"
                : "Treasure hunt nearest-neighbor route: {Count} remaining (start {Start}, nearbyPrefix {Prefix}, segment {Segment})",
            tour.Count,
            tour[0],
            preferPrefix.Count,
            TryGetNodeSegment(tour[0]) ?? "-");

        List<HuntPathfinderStep> steps = BuildStepsForTour(tour);
        State = HuntPathfinderState.PathfindingDone;
        return Task.FromResult(steps);
    }

    public string? TryGetNodeSegment(uint nodeId) =>
        TryGetSegmentIndex(nodeId) is int index ? authoredSegments[index].Id : null;

    public uint? TryGetSegmentFirstNode(string segmentId)
    {
        int index = authoredSegments.FindIndex(seg =>
            string.Equals(seg.Id, segmentId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return null;
        }

        foreach (AuthoredRouteEntry entry in authoredEntries)
        {
            if (entry.SegmentIndex == index)
            {
                return entry.NodeId;
            }
        }

        return null;
    }

    /// <summary>Index into <see cref="authoredSegments"/>, or null for pads outside the route.</summary>
    private int? TryGetSegmentIndex(uint nodeId)
    {
        foreach (AuthoredRouteEntry entry in authoredEntries)
        {
            if (entry.NodeId == nodeId)
            {
                return entry.SegmentIndex >= 0 ? entry.SegmentIndex : null;
            }
        }

        return null;
    }

    public int? TryGetNodeOrderIndex(uint nodeId)
    {
        for (int i = 0; i < authoredEntries.Count; i++)
        {
            if (authoredEntries[i].NodeId == nodeId)
            {
                return i;
            }
        }

        return null;
    }

    protected abstract Vector3 GetNodePosition(uint nodeId);

    /// <summary>Drop the cached parse (zone data changed on disk / plugin reload).</summary>
    public static void InvalidateCaches()
    {
        NodeDataCache.Clear();
        AuthoredRouteCache.Clear();
    }

    protected void LoadFile(string filename)
    {
        State = HuntPathfinderState.LoadingFile;

        if (NodeDataCache.TryGetValue((zoneId, filename), out HuntNodeDataSchema? cached))
        {
            data = cached;
            if (AuthoredRouteCache.TryGetValue(zoneId, out AuthoredRoutePayload route))
            {
                authoredEntries = route.Entries;
                authoredSegments = route.Segments;
            }
            else
            {
                authoredEntries = [];
                authoredSegments = [];
            }

            State = HuntPathfinderState.FileLoaded;
            return;
        }

        string file = GetDataFile(plugin, zoneId, filename);
        if (!File.Exists(file))
        {
            log.Error($"Required hunt data file not found: {file}");
            return;
        }

        string json = File.ReadAllText(file);
        data = JsonSerializer.Deserialize<HuntNodeDataSchema>(json) ?? new HuntNodeDataSchema();
        LoadAuthoredRoute();

        NodeDataCache[(zoneId, filename)] = data;
        AuthoredRouteCache[zoneId] = new AuthoredRoutePayload(authoredEntries, authoredSegments);
        log.Debug(
            "Cached hunt route data for {Zone}: {Nodes} node(s), {Pads} authored pad(s)",
            zoneId,
            data.NodeToNodeDistances.Count,
            authoredEntries.Count);

        State = HuntPathfinderState.FileLoaded;
    }

    private void LoadAuthoredRoute()
    {
        authoredEntries = [];
        authoredSegments = [];
        string file = GetDataFile(plugin, zoneId, "treasure_route.json");
        if (!File.Exists(file))
        {
            log.Debug("No treasure_route.json for {Zone}; using nearest-neighbor TSP", zoneId);
            return;
        }

        try
        {
            string json = File.ReadAllText(file);
            AuthoredTreasureRoute? route = JsonSerializer.Deserialize<AuthoredTreasureRoute>(json, JsonOptions);
            if (route is not { SchemaVersion: >= 2 } || route.Segments.Count == 0)
            {
                log.Debug(
                    "treasure_route.json for {Zone} is not schema v2 with segments; using TSP",
                    zoneId);
                return;
            }

            foreach (AuthoredTreasureSegment segment in route.Segments)
            {
                if (segment.Nodes.Count == 0)
                {
                    continue;
                }

                int segmentIndex = authoredSegments.Count;
                authoredSegments.Add(new AuthoredRouteSegment(segment.Id, segment.TransitionAfter));
                foreach (uint nodeId in segment.Nodes)
                {
                    authoredEntries.Add(new AuthoredRouteEntry(nodeId, segmentIndex));
                }
            }

            log.Debug(
                "Loaded authored treasure route for {Zone}: {Pads} pads in {Segments} segment(s)",
                zoneId,
                authoredEntries.Count,
                route.Segments.Count);
        }
        catch (Exception ex)
        {
            authoredEntries = [];
            authoredSegments = [];
            log.Warning(ex, "Failed to load treasure_route.json for {Zone}; using TSP", zoneId);
        }
    }

    private List<uint> BuildTspTour(Vector3 start, List<uint> remaining, uint? preferStartNode)
    {
        uint startNode = preferStartNode is uint preferred && remaining.Contains(preferred)
            ? preferred
            : remaining
                .OrderBy(id => Vector3.DistanceSquared(start, GetNodePosition(id)))
                .First();

        Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> graph =
            BuildCostGraph(remaining);
        List<uint> route = SolveTspNearestNeighbor(startNode, remaining, graph);
        return ImproveWithTwoOpt(route, graph);
    }

    /// <summary>2-opt improvement on the NN tour; start pad pinned.</summary>
    private static List<uint> ImproveWithTwoOpt(
        List<uint> route,
        Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> graph)
    {
        const int maxPasses = 40;
        const float minGain = 0.01f;

        if (route.Count < 4)
        {
            return route;
        }

        for (int pass = 0; pass < maxPasses; pass++)
        {
            bool improved = false;

            for (int i = 0; i < route.Count - 2; i++)
            {
                for (int k = i + 2; k < route.Count; k++)
                {
                    float before = EdgeCost(graph, route[i], route[i + 1]);
                    float after = EdgeCost(graph, route[i], route[k]);

                    // Open path: the tail edge only exists when k is not the last pad.
                    if (k + 1 < route.Count)
                    {
                        before += EdgeCost(graph, route[k], route[k + 1]);
                        after += EdgeCost(graph, route[i + 1], route[k + 1]);
                    }

                    if (after + minGain >= before)
                    {
                        continue;
                    }

                    route.Reverse(i + 1, k - i);
                    improved = true;
                }
            }

            if (!improved)
            {
                break;
            }
        }

        return route;
    }

    private static float EdgeCost(
        Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> graph,
        uint from,
        uint to)
    {
        if (graph.TryGetValue(from, out Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>? edges)
            && edges.TryGetValue(to, out (float Cost, List<HuntPathfinderStep> Steps) edge))
        {
            return edge.Cost;
        }

        // Missing pair — treat as very expensive but finite so the comparison stays well defined.
        return 1e9f;
    }

    private List<uint> BuildAuthoredTour(
        Vector3 start,
        List<uint> remaining,
        uint? preferStartNode,
        uint? continueAfterNodeId = null,
        uint? entryNodeId = null)
    {
        HashSet<uint> remainingSet = remaining.ToHashSet();
        Dictionary<uint, int> orderIndex = [];
        List<AuthoredRouteEntry> orderedUnique = [];
        foreach (AuthoredRouteEntry entry in authoredEntries)
        {
            if (!remainingSet.Contains(entry.NodeId) || orderIndex.ContainsKey(entry.NodeId))
            {
                continue;
            }

            orderIndex[entry.NodeId] = orderedUnique.Count;
            orderedUnique.Add(entry);
        }

        // Pads in remaining but missing from authored file — append after the authored tail.
        foreach (uint id in remaining.Where(id => !orderIndex.ContainsKey(id)))
        {
            orderIndex[id] = orderedUnique.Count;
            orderedUnique.Add(new AuthoredRouteEntry(id, -1));
        }

        if (orderedUnique.Count == 0)
        {
            return [];
        }

        List<uint> tour = BuildOrderedTour(start, orderedUnique, continueAfterNodeId, entryNodeId);
        if (preferStartNode is uint prefer && orderIndex.ContainsKey(prefer))
        {
            tour.Remove(prefer);
            tour.Insert(0, prefer);
        }

        return tour;
    }

    /// <summary>
    ///     Authored order, entered at the rotation pad when one is requested, else resumed after the
    ///     pad we just finished, else at the nearest remaining pad. Wraps in every case, so the run
    ///     still covers every segment regardless of where it started.
    /// </summary>
    private List<uint> BuildOrderedTour(
        Vector3 start,
        List<AuthoredRouteEntry> orderedUnique,
        uint? continueAfterNodeId,
        uint? entryNodeId)
    {
        HashSet<uint> remainingIds = orderedUnique.Select(e => e.NodeId).ToHashSet();

        uint? entry = entryNodeId is uint rotation && remainingIds.Contains(rotation)
            ? rotation
            : null;

        entry ??= continueAfterNodeId is uint after
            ? TryGetNextAuthoredAfter(after, orderedUnique)
            : null;

        entry ??= orderedUnique
            .OrderBy(e => Vector3.DistanceSquared(start, GetNodePosition(e.NodeId)))
            .First()
            .NodeId;

        return OrderFromEntry(orderedUnique, entry.Value);
    }

    /// <summary>
    ///     First still-remaining authored pad after <paramref name="afterNodeId"/>, wrapping.
    ///     Null when that pad is not in the authored route or nothing after it remains.
    /// </summary>
    private uint? TryGetNextAuthoredAfter(uint afterNodeId, List<AuthoredRouteEntry> remaining)
    {
        int start = -1;
        for (int i = 0; i < authoredEntries.Count; i++)
        {
            if (authoredEntries[i].NodeId == afterNodeId)
            {
                start = i;
                break;
            }
        }

        if (start < 0)
        {
            return null;
        }

        HashSet<uint> remainingIds = remaining.Select(e => e.NodeId).ToHashSet();
        for (int step = 1; step <= authoredEntries.Count; step++)
        {
            uint id = authoredEntries[(start + step) % authoredEntries.Count].NodeId;
            if (remainingIds.Contains(id))
            {
                return id;
            }
        }

        return null;
    }

    private static List<uint> OrderFromEntry(List<AuthoredRouteEntry> orderedUnique, uint entry)
    {
        int idx = orderedUnique.FindIndex(e => e.NodeId == entry);
        if (idx < 0)
        {
            return orderedUnique.Select(e => e.NodeId).ToList();
        }

        List<uint> tour = [];
        for (int i = 0; i < orderedUnique.Count; i++)
        {
            tour.Add(orderedUnique[(idx + i) % orderedUnique.Count].NodeId);
        }

        return tour;
    }

    private List<HuntPathfinderStep> BuildStepsForTour(List<uint> tour)
    {
        List<HuntPathfinderStep> steps = [HuntPathfinderStep.WalkToDestination(tour[0])];
        for (int i = 0; i < tour.Count - 1; i++)
        {
            uint from = tour[i];
            uint to = tour[i + 1];
            AuthoredTreasureTransition? transition = FindTransitionBetween(from, to);
            steps.AddRange(ResolveLeg(from, to, transition));
        }

        return steps;
    }

    /// <summary>Segment boundary owns the transition (last pad may be absent from the plan).</summary>
    private AuthoredTreasureTransition? FindTransitionBetween(uint from, uint to)
    {
        int? fromSegment = TryGetSegmentIndex(from);
        int? toSegment = TryGetSegmentIndex(to);

        // Interior of one segment — the authored order already walks it.
        if (fromSegment != null && fromSegment == toSegment)
        {
            return new AuthoredTreasureTransition { Type = "walk" };
        }

        // Pads outside the authored route (layout-only) have no boundary to honor; let the bake pick.
        if (fromSegment is not int index)
        {
            return new AuthoredTreasureTransition { Type = "auto" };
        }

        // A wrapped tour ends on the last segment and continues into the first, which has no
        // authored transition — "auto" costs walk against hop and Return and takes the cheapest.
        return authoredSegments[index].TransitionAfter
               ?? new AuthoredTreasureTransition { Type = "auto" };
    }

    private List<HuntPathfinderStep> ResolveLeg(uint fromId, uint toId, AuthoredTreasureTransition? transition)
    {
        string type = transition?.Type?.Trim().ToLowerInvariant() ?? "walk";
        switch (type)
        {
            case "return":
                return BuildEntryLeg(toId);
            case "teleport" when TryParseAethernet(transition?.To, out HuntAethernet shard):
                if (!IsUsableHuntDestination(shard))
                {
                    return GetBestSteps(fromId, toId).Steps;
                }

                return
                [
                    HuntPathfinderStep.ReturnToBaseCamp(),
                    HuntPathfinderStep.TeleportToAethernet(shard),
                    HuntPathfinderStep.WalkToDestination(toId)
                ];
            case "none":
            case "walk":
                return [HuntPathfinderStep.WalkToDestination(toId)];
            default:
                // auto: cheapest hop (walk / aethernet / Return).
                return GetBestSteps(fromId, toId).Steps;
        }
    }

    /// <summary>
    ///     Entry to the next pad from camp: optional Return, then best aethernet + walk (or walk
    ///     from camp when cheaper). When <paramref name="alreadyInCamp"/>, skip Return so a start
    ///     at base still uses aethernet instead of walking the whole map.
    /// </summary>
    public List<HuntPathfinderStep> BuildEntryLeg(uint toId, bool alreadyInCamp = false)
    {
        HuntAethernet baseCamp = BaseCampAethernet;

        float bestCost = float.MaxValue;
        if (data.AethernetToNodeDistances.TryGetValue(baseCamp, out List<HuntToNode>? fromBase))
        {
            HuntToNode walkFromCamp = fromBase.FirstOrDefault(x => x.Id == toId);
            if (walkFromCamp.Id == toId)
            {
                bestCost = walkFromCamp.Distance;
            }
        }

        HuntAethernet? bestShard = null;
        foreach ((HuntAethernet aethernet, List<HuntToNode> list) in data.AethernetToNodeDistances)
        {
            if (aethernet == baseCamp || !IsUsableHuntDestination(aethernet))
            {
                continue;
            }

            HuntToNode to = list.FirstOrDefault(x => x.Id == toId);
            if (to.Id != toId)
            {
                continue;
            }

            float cost = NavigationConstants.AethernetHopCost + to.Distance;
            if (cost >= bestCost)
            {
                continue;
            }

            bestCost = cost;
            bestShard = aethernet;
        }

        List<HuntPathfinderStep> steps = [];
        if (!alreadyInCamp)
        {
            steps.Add(HuntPathfinderStep.ReturnToBaseCamp());
        }

        if (bestShard is HuntAethernet shard)
        {
            steps.Add(HuntPathfinderStep.TeleportToAethernet(shard));
        }

        steps.Add(HuntPathfinderStep.WalkToDestination(toId));
        return steps;
    }

    private static bool TryParseAethernet(string? name, out HuntAethernet aethernet)
    {
        aethernet = default;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return Enum.TryParse(name.Trim(), ignoreCase: true, out aethernet);
    }

    /// <summary>Lifestream destinations only — walking to a locked shard to open the menu is still fine.</summary>
    private bool IsUsableHuntDestination(HuntAethernet aethernet) =>
        aethernet == BaseCampAethernet
        || OccultCrescentHelper.IsAethernetUnlocked((uint)aethernet);

    /// <summary>Cheapest of walk, aethernet hop, or Return (+ optional aethernet from camp).</summary>
    protected (float Cost, List<HuntPathfinderStep> Steps) GetBestSteps(uint fromId, uint toId)
    {
        float bestCost = float.MaxValue;
        List<HuntPathfinderStep> bestSteps = [];

        if (data.NodeToNodeDistances.TryGetValue(fromId, out List<HuntToNode>? directList))
        {
            HuntToNode direct = directList.FirstOrDefault(x => x.Id == toId);
            if (direct.Id == toId)
            {
                bestCost = direct.Distance;
                bestSteps = [HuntPathfinderStep.WalkToDestination(toId)];
            }
        }

        if (data.NodeToAethernetDistances.TryGetValue(fromId, out List<HuntToAethernet>? shardList) && shardList.Count > 0)
        {
            HuntToAethernet fromShard = shardList.OrderBy(x => x.Distance).First();
            foreach ((HuntAethernet aethernet, List<HuntToNode> list) in data.AethernetToNodeDistances)
            {
                if (!IsUsableHuntDestination(aethernet))
                {
                    continue;
                }

                HuntToNode to = list.FirstOrDefault(x => x.Id == toId);
                if (to.Id != toId)
                {
                    continue;
                }

                float cost = fromShard.Distance + NavigationConstants.AethernetHopCost + to.Distance;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestSteps =
                    [
                        HuntPathfinderStep.WalkToAethernet(fromShard.Aethernet),
                        HuntPathfinderStep.TeleportToAethernet(aethernet),
                        HuntPathfinderStep.WalkToDestination(toId)
                    ];
                }
            }
        }

        HuntAethernet baseCamp = BaseCampAethernet;
        if (data.AethernetToNodeDistances.TryGetValue(baseCamp, out List<HuntToNode>? fromBaseList))
        {
            HuntToNode walkFromBase = fromBaseList.FirstOrDefault(x => x.Id == toId);
            if (walkFromBase.Id == toId)
            {
                float returnWalkCost = NavigationConstants.ReturnCost + walkFromBase.Distance;
                if (returnWalkCost < bestCost)
                {
                    bestCost = returnWalkCost;
                    bestSteps =
                    [
                        HuntPathfinderStep.ReturnToBaseCamp(),
                        HuntPathfinderStep.WalkToDestination(toId)
                    ];
                }
            }
        }

        // Return lands at base camp — optional aethernet hop from there to a closer shard.
        foreach ((HuntAethernet aethernet, List<HuntToNode> list) in data.AethernetToNodeDistances)
        {
            if (aethernet == baseCamp || !IsUsableHuntDestination(aethernet))
            {
                continue;
            }

            HuntToNode to = list.FirstOrDefault(x => x.Id == toId);
            if (to.Id != toId)
            {
                continue;
            }

            float cost = NavigationConstants.ReturnCost + NavigationConstants.AethernetHopCost + to.Distance;
            if (cost < bestCost)
            {
                bestCost = cost;
                bestSteps =
                [
                    HuntPathfinderStep.ReturnToBaseCamp(),
                    HuntPathfinderStep.TeleportToAethernet(aethernet),
                    HuntPathfinderStep.WalkToDestination(toId)
                ];
            }
        }

        if (bestSteps.Count == 0)
        {
            // Fallback when bake data is missing a pair — walk via destination id only.
            bestCost = Vector3.Distance(GetNodePosition(fromId), GetNodePosition(toId));
            bestSteps = [HuntPathfinderStep.WalkToDestination(toId)];
        }

        return (bestCost, bestSteps);
    }

    private Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> BuildCostGraph(List<uint> nodes)
    {
        Dictionary<uint, Dictionary<uint, (float, List<HuntPathfinderStep>)>> graph = new();

        foreach (uint from in nodes)
        {
            graph[from] = new();
            foreach (uint to in nodes)
            {
                if (from == to)
                {
                    continue;
                }

                graph[from][to] = GetBestSteps(from, to);
            }
        }

        return graph;
    }

    /// <summary>Open-path nearest-neighbor TSP.</summary>
    private static List<uint> SolveTspNearestNeighbor(
        uint start,
        List<uint> nodes,
        Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> graph
    )
    {
        if (nodes.Count == 0)
        {
            return [];
        }

        if (nodes.Count == 1)
        {
            return [start];
        }

        List<uint> route = [start];
        HashSet<uint> unvisited = new(nodes.Where(n => n != start));

        while (unvisited.Count > 0)
        {
            uint last = route[^1];
            uint? nearest = null;
            float minCost = float.MaxValue;

            foreach (uint candidate in unvisited)
            {
                float cost = graph[last][candidate].Cost;
                if (cost < minCost)
                {
                    minCost = cost;
                    nearest = candidate;
                }
            }

            if (nearest is not uint next)
            {
                break;
            }

            route.Add(next);
            unvisited.Remove(next);
        }

        return route;
    }

    private static string GetDataFile(IDalamudPluginInterface plugin, ZoneId zoneId, string filename)
    {
        string pluginDir = GetPluginDirectory(plugin);
        return Path.Combine(pluginDir, "Data", zoneId.TreasureDataFolder(), filename);
    }

    private static string GetPluginDirectory(IDalamudPluginInterface plugin)
    {
        string? pluginDir = plugin.AssemblyLocation.DirectoryName;
        if (!string.IsNullOrEmpty(pluginDir))
        {
            return pluginDir;
        }

        string? assemblyDir = Path.GetDirectoryName(plugin.GetType().Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDir))
        {
            return assemblyDir;
        }

        throw new InvalidOperationException("Unable to resolve the BOCCHI plugin directory for hunt data files.");
    }
}
