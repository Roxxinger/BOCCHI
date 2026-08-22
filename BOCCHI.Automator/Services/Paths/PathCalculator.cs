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
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;
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

        GraphTraverser traverser = new(graph, pathfinder, logger);
        traverser.AddCalculator(new WalkTeleportWalkCalculator());
        traverser.AddCalculator(new DirectWalkCalculator());

        // Prefer Return over long walks; does not drop the pot.
        if (distance > NavigationConstants.MaxDirectWalkDistance)
        {
            traverser.AddCalculator(new ReturnTeleportWalkCalculator());
        }

        List<PathStep> steps = await traverser.FindPath(player.Position, goalNode);
        List<IPathStep> resolved = steps
            .Select(step => AethernetNavigation.ResolveAetherytePathStep(step, zone, player.Position))
            .Cast<IPathStep>()
            .ToList();

        if (resolved.Count == 0)
        {
            logger.Debug("No route to {Pos:F0} ({Dist:F0}y) — caller falls back to walking", destination, distance);
            return PathCalculationResult.Failed();
        }

        logger.Debug(
            "Position path planned: {Count} step(s) toward {Pos:F0} ({Dist:F0}y)",
            resolved.Count,
            destination,
            distance);

        return PathCalculationResult.Planned(resolved);
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
        if (goal.GoalType is CriticalEncounterGoal ceGoalForRadius
            && geometry.TryGet(ceGoalForRadius.id.Value) is { Radius: > 0 } area)
        {
            ceShape = NavigationConstants.ResolveCriticalEncounterShape(
                zone,
                ceGoalForRadius.id.Value,
                area.IsSquare);
            // Authored staging for travel; sanitize LGB wait centre / radius.
            pathGoal = goalNode;
            CriticalEncounter.SanitizeRegistration(
                goalNode.Position,
                area.Center,
                area.Radius,
                out Vector3 sanitizedCenter,
                out float sizeOk,
                out bool rejected);
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
                rejected ? ", bad MapRange size/centre ignored" : "");
        }

        Vector3 arrivalCheck = potPrepositionStandOff ?? pathGoal.Position;
        float distanceToGoal = player.Position.Distance2D(arrivalCheck);
        bool insideCeWait = ceCombatRadius > 0f
                            && ceWaitCenter is { } waitAt
                            && NavigationConstants.IsInsideCriticalEncounterWaitArea(
                                waitAt,
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

        GraphTraverser traverser = new(graph, pathfinder, logger);
        // Teleport-first: from camp this is usually instant. DirectWalk only for short hops.
        traverser.AddCalculator(new WalkTeleportWalkCalculator());
        traverser.AddCalculator(new DirectWalkCalculator());

        // Long trips: always add the Return calculator (#172).
        if (!insideCeWait && distanceToGoal > NavigationConstants.MaxDirectWalkDistance)
        {
            traverser.AddCalculator(new ReturnTeleportWalkCalculator());
        }

        List<PathStep> steps = await traverser.FindPath(player.Position, pathGoal);
        List<PathStep> resolvedSteps = steps
            .Select(step => AethernetNavigation.ResolveAetherytePathStep(step, zone, player.Position))
            .ToList();

        if (potPrepositionStandOff is { } standOff)
        {
            // Pot preposition: rewrite the last Pathfind only (#174).
            int lastPathfind = resolvedSteps.FindLastIndex(step => step.PathStepData is Pathfind);
            if (lastPathfind >= 0)
            {
                float range = resolvedSteps[lastPathfind].PathStepData is Pathfind(_, var r) ? r : 0f;
                resolvedSteps[lastPathfind] = PathStep.Pathfind(standOff, range);
            }
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
            // Teleport-only mode stripped walks — PathfindingHandler pauses for manual travel.
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
}
