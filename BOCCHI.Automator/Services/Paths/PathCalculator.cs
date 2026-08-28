using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Data.Zones.Graph.Traversal;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using BOCCHI.Treasure.Services;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using System.Numerics;

namespace BOCCHI.Automator.Services.Paths;

public class PathCalculator
(
    IPathfinder pathfinder,
    IObjectTable objects,
    IZoneProvider zones,
    IFateRepository fates,
    IFateContext fateContext,
    AutomatorConfig config,
    CriticalEncounterGeometry geometry,
    IVNavmeshIpc vnav,
    ILogger<PathCalculator> logger
) : IPathCalculator
{
    public Task<PathCalculationResult> Calculate(IGoal goal) => Calculate(goal, allowAutoRebuild: true);

    /// <inheritdoc />
    public async Task<PathCalculationResult> CalculateToPosition(Vector3 destination, float arrivalRange)
    {
        if (objects.LocalPlayer is not { } player)
        {
            return PathCalculationResult.NoTravelNeeded();
        }

        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            return PathCalculationResult.NoTravelNeeded();
        }

        float distance = player.Position.Distance2D(destination);
        if (distance <= arrivalRange)
        {
            return PathCalculationResult.NoTravelNeeded();
        }

        ZoneGraph graph = await zone.GetGraph();

        // Goal is a free Node (like live FATE); Return / aethernet estimate when there are no wired edges.
        Node goalNode = new()
        {
            Type = NodeType.PotChest,
            Position = destination,
        };

        List<PathStep> steps = await FindResolvedPathSteps(
            graph,
            zone,
            player.Position,
            goalNode,
            distance > NavigationConstants.MaxDirectWalkDistance);

        if (steps.Count == 0)
        {
            logger.Debug("No route to {Pos:F0} ({Dist:F0}y) — caller falls back to walking", destination, distance);
            return PathCalculationResult.Failed();
        }

        logger.Debug(
            "Position path planned: {Count} step(s) toward {Pos:F0} ({Dist:F0}y)",
            steps.Count,
            destination,
            distance);

        return PathCalculationResult.Planned(steps);
    }

    private async Task<PathCalculationResult> Calculate(IGoal goal, bool allowAutoRebuild)
    {
        if (objects.LocalPlayer is not { } player)
        {
            logger.Warn("No Player");
            return PathCalculationResult.NoTravelNeeded();
        }

        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            logger.Warn("In wrong zone");
            return PathCalculationResult.NoTravelNeeded();
        }

        // Combat None: InFate walks to mobs from the rim. AI cannot — stay on the centre path
        // until we are actually close enough for AutoTarget / StayCloseToTarget.
        if (goal.GoalType is FateGoal fateGoal
            && fateContext.GetFateId() == fateGoal.id
            && (!config.CombatAutorotation.UsesCombatAutomation()
                || fateContext.IsInCombatWith(fateGoal.id)
                || IsWithinFateAiHandoff(player.Position, fateGoal.id)))
        {
            logger.Debug("Already inside target FATE.");
            return PathCalculationResult.NoTravelNeeded();
        }

        ZoneGraph graph = await zone.GetGraph();

        Node goalNode;
        try
        {
            goalNode = GetGoalNode(goal, graph);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to resolve goal node");
            return await AutoRebuildAndRetry(zone, goal, allowAutoRebuild, "missing activity node");
        }

        // Prefer live FATE center; CEs path to the LGB MapRange centre (blue ring), not authored staging.
        Node pathGoal = goalNode;
        Vector3? potPrepositionStandOff = null;
        if (goal.GoalType is FateGoal liveFateGoal
            && fates.Snapshot().FirstOrDefault(f => f.Id.Value == liveFateGoal.id.Value) is { } liveFate)
        {
            pathGoal = new Node
            {
                Id = goalNode.Id,
                Type = goalNode.Type,
                Position = liveFate.Position,
                Metadata = goalNode.Metadata
            };
        }
        else if (goal.GoalType is FateGoal potPreposition
                 && zone.IsPotFate(potPreposition.id.Value))
        {
            potPrepositionStandOff = NavigationApproach.GetPotPrepositionPosition(goalNode.Position, player.Position);
        }

        float ceCombatRadius = 0f;
        ActivityAreaShape ceShape = ActivityAreaShape.Circle;
        Vector3? ceWaitCenter = null;
        float ceStandRadius = 0f;
        int ceId = 0;
        Vector3 ceStaging = pathGoal.Position;
        if (goal.GoalType is CriticalEncounterGoal ceGoalForRadius)
        {
            ceId = ceGoalForRadius.id.Value;
            ActivityData? authoredCe = zone.GetCriticalEncounterData()
                .FirstOrDefault(a => a.Id == ceId);
            Vector3 authoredStaging = authoredCe?.Position ?? pathGoal.Position;
            ceStaging = authoredStaging;
            if (geometry.TryResolveForAuthored(
                    ceGoalForRadius.id.Value,
                    authoredStaging,
                    out string resolveDetail) is { Radius: > 0 } area)
            {
                ceShape = NavigationConstants.ResolveCriticalEncounterShape(
                    zone,
                    ceGoalForRadius.id.Value,
                    area.IsSquare);
                ceStandRadius = authoredCe?.StandRadius ?? 0f;
                CriticalEncounter.SanitizeRegistration(
                    authoredStaging,
                    area.Center,
                    area.Radius,
                    out Vector3 sanitizedCenter,
                    out float sizeOk,
                    out bool rejected,
                    authoredCe?.CombatRadius);
                ceWaitCenter = sanitizedCenter;
                ceCombatRadius = sizeOk;
                zone.ApplyCriticalEncounterCombat(ceGoalForRadius.id.Value, ceCombatRadius, ceShape);
                logger.Debug(
                    "CE {Id} path goal at authored {Pos:F0} ({Shape}, combat radius {Radius:F0}, wait centre {Center:F0}{Note})",
                    ceGoalForRadius.id.Value,
                    pathGoal.Position,
                    ceShape,
                    ceCombatRadius,
                    ceWaitCenter.Value,
                    rejected || resolveDetail.StartsWith("alternate", StringComparison.Ordinal)
                        ? $", {resolveDetail}"
                        : "");
            }
        }

        Vector3 arrivalCheck = potPrepositionStandOff ?? pathGoal.Position;
        float distanceToGoal = player.Position.Distance2D(arrivalCheck);
        bool insideCeWait = ceCombatRadius > 0f
                            && ceWaitCenter is { } waitCenter
                            && NavigationConstants.IsInsideCriticalEncounterWaitArea(
                                waitCenter,
                                ceCombatRadius,
                                ceShape,
                                player.Position);

        if (insideCeWait)
        {
            logger.Debug("Inside CE wait area at {Pos:F0} — no travel steps", arrivalCheck);
            return PathCalculationResult.NoTravelNeeded();
        }

        if (ceCombatRadius <= 0f && distanceToGoal <= NavigationConstants.EventArrivalRadius)
        {
            logger.Debug("Too close to destination ({Dist:F1}y).", distanceToGoal);
            return PathCalculationResult.NoTravelNeeded();
        }

        List<PathStep> resolvedSteps = await FindResolvedPathSteps(
            graph,
            zone,
            player.Position,
            pathGoal,
            !insideCeWait && distanceToGoal > NavigationConstants.MaxDirectWalkDistance);

        if (potPrepositionStandOff is { } standOff)
        {
            RewriteLastPathfind(resolvedSteps, standOff);
        }
        else if (ceCombatRadius > 0f && ceWaitCenter is { } waitAt)
        {
            float red = NavigationConstants.CriticalEncounterRedRadius(
                NavigationConstants.CriticalEncounterPaddedRadius(ceCombatRadius, ceShape),
                ceShape);
            Vector3 approach = NavigationApproach.GetCriticalEncounterApproachPosition(
                waitAt, red, ceShape, ceStandRadius, stableSeed: ceId);
            approach = ResolveCriticalEncounterPathfindTarget(approach, waitAt, player.Position);

            if (CriticalEncounterPathOverrides.TryGetApproachVias(
                    zone.ZoneId,
                    ceId,
                    player.Position,
                    ceStaging,
                    out IReadOnlyList<Vector3> vias))
            {
                InsertPathfindBeforeLast(resolvedSteps, vias, NavigationConstants.EventArrivalRadius);
            }

            RewriteLastPathfind(resolvedSteps, approach);
        }

        int stepsBeforeTeleportOnlyStrip = resolvedSteps.Count;
        if (config.StopAfterReturn)
        {
            // Keep Return / Teleport; drop the walk to the FATE or CE.
            resolvedSteps = resolvedSteps
                .Where(step => step.Kind != PathStepKind.Pathfind)
                .ToList();
            logger.Debug("TeleportOnlyTravel: {Count} step(s) after dropping pathfinds", resolvedSteps.Count);
        }

        if (resolvedSteps.Count == 0)
        {
            if (config.StopAfterReturn && stepsBeforeTeleportOnlyStrip > 0)
            {
                logger.Debug(
                    "Path planned: 0 step(s) toward {Pos:F0} ({Dist:F0}y) after teleport-only strip",
                    arrivalCheck,
                    distanceToGoal);
                return PathCalculationResult.Planned([]);
            }

            logger.Error(
                "No route to {Pos:F0} ({Dist:F0}y) — zone path map may be incomplete",
                arrivalCheck,
                distanceToGoal);
            return await AutoRebuildAndRetry(zone, goal, allowAutoRebuild, "no route to activity");
        }

        logger.Debug(
            "Path planned: {Count} step(s) toward {Pos:F0} ({Dist:F0}y)",
            resolvedSteps.Count,
            arrivalCheck,
            distanceToGoal);

        return PathCalculationResult.Planned(resolvedSteps);
    }

    private async Task<PathCalculationResult> AutoRebuildAndRetry(
        IZone zone,
        IGoal goal,
        bool allowAutoRebuild,
        string reason)
    {
        if (!allowAutoRebuild)
        {
            return PathCalculationResult.Failed();
        }

        logger.Warning("Auto-rebuilding zone path map ({Reason}) and retrying once", reason);
        zone.InvalidateGraph(reason);
        // Kick load now so UI shows Loading/Building and the retry uses the fresh map.
        await zone.GetGraph();
        return await Calculate(goal, allowAutoRebuild: false);
    }

    private Node GetGoalNode(IGoal goal, ZoneGraph graph)
    {
        return goal.GoalType switch
        {
            CriticalEncounterGoal(var id) => GetActivityNode(id.Value, graph, NodeType.CriticalEncounter),
            FateGoal(var id) => GetActivityNode(id.Value, graph, NodeType.NormalFate, NodeType.PotFate),
            var _ => throw new ArgumentOutOfRangeException()
        };
    }

    private Node GetActivityNode(int id, ZoneGraph graph, params NodeType[] types)
    {
        List<Node> nodes = graph.GetNodesByTypes(types).Where(n =>
        {
            if (n.Metadata is not ActivityNodeMetadata meta)
            {
                return false;
            }

            return meta.Id == id;
        }).ToList();

        return nodes.Count == 0 ? throw new InvalidOperationException("No nodes for Activity") : nodes.First();
    }

    private bool IsWithinFateAiHandoff(Vector3 player, FateId id)
    {
        float nearest = float.MaxValue;
        foreach (IBattleNpc target in fateContext.GetTargets())
        {
            nearest = MathF.Min(nearest, player.Distance2D(target.Position) - target.HitboxRadius);
        }

        Fate? live = fates.Snapshot().FirstOrDefault(f => f.Id.Value == id.Value);
        float toCenter = live != null ? player.Distance2D(live.Position) : float.MaxValue;
        return NavigationConstants.IsWithinFateAiHandoff(toCenter, nearest);
    }

    private async Task<List<PathStep>> FindResolvedPathSteps(
        ZoneGraph graph,
        IZone zone,
        Vector3 from,
        Node goal,
        bool addReturnCalculator)
    {
        GraphTraverser traverser = new(graph, pathfinder, logger);
        traverser.AddCalculator(new WalkTeleportWalkCalculator());
        traverser.AddCalculator(new DirectWalkCalculator());
        if (addReturnCalculator)
        {
            traverser.AddCalculator(new ReturnTeleportWalkCalculator());
        }

        List<PathStep> steps = await traverser.FindPath(from, goal);
        return steps
            .Select(step => AethernetNavigation.ResolveAetherytePathStep(step, zone, from))
            .ToList();
    }

    private Vector3 ResolveCriticalEncounterPathfindTarget(Vector3 approach, Vector3 waitCenter, Vector3 player)
    {
        if (TreasurePathing.TrySnapToNavmesh(approach, player.Y, vnav, out Vector3 snapped))
        {
            return snapped;
        }

        if (TreasurePathing.TrySnapToNavmesh(waitCenter, player.Y, vnav, out snapped))
        {
            logger.Debug(
                "CE approach off-mesh at {Approach:F0} — using snapped wait centre {Center:F0}",
                approach,
                snapped);
            return snapped;
        }

        logger.Debug(
            "CE approach off-mesh at {Approach:F0} — keeping unsnapped target (navmesh may still be loading)",
            approach);
        return approach;
    }

    private static void InsertPathfindBeforeLast(List<PathStep> steps, IReadOnlyList<Vector3> vias, float range)
    {
        int lastPathfind = steps.FindLastIndex(step => step.PathStepData is Pathfind);
        if (lastPathfind < 0)
        {
            return;
        }

        foreach (Vector3 via in vias)
        {
            steps.Insert(lastPathfind, PathStep.Pathfind(via, range));
        }
    }

    private static void RewriteLastPathfind(List<PathStep> steps, Vector3 destination)
    {
        int lastPathfind = steps.FindLastIndex(step => step.PathStepData is Pathfind);
        if (lastPathfind < 0)
        {
            return;
        }

        float range = steps[lastPathfind].PathStepData is Pathfind(_, var r) ? r : 0f;
        steps[lastPathfind] = PathStep.Pathfind(destination, range);
    }
}
