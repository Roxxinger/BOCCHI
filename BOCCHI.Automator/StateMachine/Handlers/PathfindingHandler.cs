using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Chain;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.Translation;
using Ocelot.States.Score;
using Ocelot.Windows;
using System.Numerics;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class PathfindingHandler
(
    IAutomatorMemory memory,
    IPathStepExecutor pathStepExecutor,
    IChainManager manager,
    IObjectTable objects,
    IPathfinder pathfinder,
    ITargetManager targetManager,
    IZoneProvider zones,
    AutomatorConfig config,
    MovementConfig movement,
    UIConfig uiConfig,
    ICondition conditions,
    IChatGui chat,
    ITranslator<MainWindow> translator,
    AutoRotationController autoRotation,
    IVNavmeshIpc vnav,
    ILogger<PathfindingHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Pathfinding)
{
    private static readonly TimeSpan MountBeforePauseTimeout = TimeSpan.FromSeconds(8);

    private Task<ChainResult>? currentPathTask;

    private string? pendingPauseReason;

    private DateTime mountBeforePauseDeadline = DateTime.MinValue;

    // Path conflict detection (mirrors AOCCH MovementController.CheckPathConflict)
    private DateTimeOffset lastPathConflictCheck = DateTimeOffset.MinValue;

    // Pre-computed alternate route to the active destination — swapped in on conflict so the
    // re-route is seamless (no stop-and-recalculate stutter).
    private Task<List<System.Numerics.Vector3>>? standbyPathTask;
    private System.Numerics.Vector3 standbyDestination;

    public override void Enter()
    {
        base.Enter();
        // Drop leftover combat target so rotations don't pull trash mid-path.
        targetManager.Target = null;
        autoRotation.DisableAi();
    }

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);

        // Don't cancel pathing on a same-frame return handoff (restart loop).
        if (next == AutomatorState.Returning)
        {
            currentPathTask = null;
            pathfinder.Stop();
            return;
        }

        // Soft-interrupt (e.g. Completionist survey click) owns vnav via ActivityGoto — leave it alone.
        if (memory.TryRemember<NavigationInterruptedMemory>(out NavigationInterruptedMemory _))
        {
            currentPathTask = null;
            pendingPauseReason = null;
            PathStepSoftStop.Cancel(manager);
            return;
        }

        ResetPathfinding();
    }

    public override StatePriority GetScore()
    {
        if (memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _))
        {
            return StatePriority.Never;
        }

        if (memory.TryRemember<SuspendTravelForActivityMemory>(out SuspendTravelForActivityMemory _))
        {
            return StatePriority.Never;
        }

        // Hold still for pot chest farm — leftover GoalPathStep must not Return/TP away first.
        if (memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _)
            || memory.TryRemember<PendingPotChestFarmMemory>(out PendingPotChestFarmMemory _))
        {
            return StatePriority.Never;
        }

        return memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _) ? StatePriority.High : StatePriority.Never;
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        if (pendingPauseReason != null && FinishMountBeforePause())
        {
            return;
        }

        if (!memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory path))
        {
            ResetPathfinding();
            return;
        }

        path.Update();

        // Keep standby path pre-computed whenever we're on a Pathfind step
        // Must run BEFORE CheckPathConflict so standby is ready for immediate swap
        if (path.GetNextPathStep()?.PathStepData is Pathfind(var dest, _))
        {
            TickStandbyPath(player, dest);
        }

        // Path conflict detection: check for other players on our active path
        CheckPathConflict(player);

        // Route calc found nothing while still far from the goal — don't hang forever.
        if (path.RoutingFailed && currentPathTask == null)
        {
            string message = translator.T(".automation.automator.path_routing_failed");
            BocchiChat.PrintError(chat, uiConfig, message);
            PauseForManualPathing(message);
            return;
        }

        // Teleport-only mode: calc produced no Return/Teleport steps → pause for manual.
        if (path.PauseWhenPlanCompletes && path.IsEmptyPlan && currentPathTask == null)
        {
            BeginMountThenPause(TeleportOnlyMessage("no travel steps left"));
            return;
        }

        if (currentPathTask != null)
        {
            // Remount mid-route if Treasure Sight (or anything else) left us on foot.
            if (path.GetNextPathStep()?.PathStepData is Pathfind(var destination, _))
            {
                MountWait.TryCastIfNeeded(
                    conditions,
                    objects,
                    destination,
                    movement.ShouldAutoMount,
                    movement.PreferredMountId,
                    zones.GetZone().IsInBasecamp());
            }

            if (currentPathTask.IsCompleted)
            {
                if (currentPathTask.Status == TaskStatus.RanToCompletion)
                {
                    ChainResult result = currentPathTask.Result;
                    if (result.IsSuccess)
                    {
                        logger.Debug("Finished current task step...");
                        PathStepKind completedKind = path.GetNextPathStep()?.Kind ?? PathStepKind.Pathfind;
                        path.DequeuePathStep();

                        if (path.PauseWhenPlanCompletes
                            && path.GetNextPathStep() == null
                            && completedKind is PathStepKind.Teleport or PathStepKind.Return)
                        {
                            currentPathTask = null;
                            memory.Forget<BaseTeleportDelayMemory>();
                            BeginMountThenPause(TeleportOnlyMessage("arrived at aetheryte"));
                            return;
                        }
                    }
                    else if (result.IsCanceled)
                    {
                        // Soft-stop / CE wait handoff / combat cancel — keep the goal and replan.
                        ReplanAfterPathCancel("Path step canceled");
                        return;
                    }
                    else
                    {
                        logger.Warning("Path step failed: {Error}", result.ErrorMessage ?? "unknown");
                        pathfinder.Stop();

                        // Keep Teleport — dequeuing skips the hop and leaves you stuck outside
                        // the pad (or walking the long way). Approach retries next tick.
                        if (path.GetNextPathStep()?.Kind != PathStepKind.Teleport)
                        {
                            path.DequeuePathStep();
                        }
                    }
                }
                else if (currentPathTask.IsCanceled)
                {
                    ReplanAfterPathCancel("Path step task canceled");
                    return;
                }
                else
                {
                    logger.Warning("Path step task faulted");
                    pathfinder.Stop();
                }

                currentPathTask = null;
            }

            return;
        }

        if (currentPathTask == null && path.GetNextPathStep() is { } step)
        {
            if (step.PathStepData is Return)
            {
                logger.Debug("Handing off return step to ReturningHandler...");
                memory.TryAdd(new ReturningStateMemory(ReturnDelay.Roll(config)));
                path.DequeuePathStep();

                if (path.PauseWhenPlanCompletes && path.GetNextPathStep() == null)
                {
                    BeginMountThenPause(TeleportOnlyMessage("returned to camp"));
                }

                return;
            }

            if (step.PathStepData is Teleport
                && zones.GetZone().IsInBasecamp()
                && !WaitForBaseTeleportDelay())
            {
                return;
            }

            logger.Debug("Starting next task step...");
            memory.Forget<BaseTeleportDelayMemory>();
            currentPathTask = pathStepExecutor.Execute(step);
            return;
        }

        // Empty plan (already at destination) — keep GoalPathStepMemory so Automator doesn't recreate.
        if (!path.IsValid)
        {
            memory.Forget<GoalPathStepMemory>();
        }
    }

    /// <summary>
    /// Checks for other players on our active vnavmesh path and triggers a re-route if someone is ahead of us.
    /// Mirrors AOCCH MovementController.CheckPathConflict.
    /// </summary>
    private void CheckPathConflict(IGameObject player)
    {
        if (!movement.EnablePathConflictDetection)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - lastPathConflictCheck < TimeSpan.FromSeconds(movement.PathConflictCheckIntervalSeconds))
        {
            return;
        }
        lastPathConflictCheck = now;

        // Need an active vnavmesh path with waypoints
        var waypoints = vnav.GetPathWaypoints();
        if (waypoints == null || waypoints.Count == 0)
        {
            return;
        }

        if (!memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory pathMemory))
        {
            return;
        }

        var nextStep = pathMemory.GetNextPathStep();
        if (nextStep?.PathStepData is not Pathfind(var destination, _))
        {
            return;
        }

        var localPlayer = objects.LocalPlayer;
        if (localPlayer == null) return;

        // Get nearby players (PCs, alive, not us)
        var nearbyPlayers = objects
            .Where(obj => obj.ObjectKind == ObjectKind.Pc
                && obj is ICharacter pc
                && pc.IsValid()
                && pc.GameObjectId != localPlayer.GameObjectId
                && pc.CurrentHp > 0)
            .Select(obj => (ICharacter)obj)
            .ToArray();

        if (nearbyPlayers.Length == 0)
        {
            return;
        }

        var playerPos = player.Position;
        float playerDistToDest = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(destination.X, destination.Z));

        foreach (var other in nearbyPlayers)
        {
            float minDist = float.MaxValue;
            for (int i = 0; i < waypoints.Count; i++)
            {
                var d = Vector2.Distance(
                    new Vector2(other.Position.X, other.Position.Z),
                    new Vector2(waypoints[i].X, waypoints[i].Z));
                if (d < minDist) minDist = d;
            }

            // Player on our path (within threshold of any waypoint) AND ahead of us (closer to destination)
            if (minDist < movement.PathConflictDistanceThreshold)
            {
                float otherDistToDest = Vector2.Distance(
                    new Vector2(other.Position.X, other.Position.Z),
                    new Vector2(destination.X, destination.Z));

                if (otherDistToDest < playerDistToDest - movement.PathConflictAheadThreshold)
                {
                    logger.Warning(
                        "[PathConflict] step=\"{Step}\" conflictingPlayer=\"{Name}\" playerDist={PlayerDist:0.0} otherDist={OtherDist:0.0} minDistToPath={MinDist:0.0}",
                        nextStep.Describe(), other.Name, playerDistToDest, otherDistToDest, minDist);

                    if (TrySwapToStandbyPath())
                    {
                        logger.Warning("[PathConflict] action=swap-to-standby seamless=true");
                    }
                    else
                    {
                        logger.Warning("[PathConflict] action=replan (no standby ready)");
                        ReplanAfterPathCancel("Path conflict — other player ahead on path");
                    }

                    return;
                }
            }
        }
    }

    /// <summary>
    ///     Keeps one alternate route to the active destination pre-computed in the background.
    ///     Pure vnavmesh.Pathfind — does not touch the running movement.
    /// </summary>
    private void TickStandbyPath(Dalamud.Game.ClientState.Objects.Types.IGameObject player, System.Numerics.Vector3 destination)
    {
        if (standbyPathTask != null)
        {
            if (standbyPathTask.IsCompleted
                && (standbyDestination != destination || standbyPathTask.IsFaulted || standbyPathTask.IsCanceled))
            {
                // Goal changed or the calc failed — drop it; a fresh one starts next tick.
                standbyPathTask = null;
            }
            else if (standbyPathTask.IsCompleted && !standbyPathTask.IsFaulted && !standbyPathTask.IsCanceled)
            {
                // Task completed successfully for same destination.
                // Check if player moved significantly since path was computed.
                // The path was computed from some earlier position; if we've moved far,
                // it's stale and we should restart.
                try
                {
                    var nodes = standbyPathTask.Result;
                    if (nodes != null && nodes.Count > 0)
                    {
                        var pathStart = nodes[0];
                        float moved = Vector2.Distance(
                            new Vector2(player.Position.X, player.Position.Z),
                            new Vector2(pathStart.X, pathStart.Z));
                        if (moved > 10f)
                        {
                            // Player has moved >10m from path start — path is stale
                            standbyPathTask = null;
                        }
                    }
                }
                catch
                {
                    standbyPathTask = null;
                }
            }

            return;
        }

        standbyDestination = destination;
        standbyPathTask = vnav.Pathfind(player.Position, destination, fly: false);
    }

    /// <summary>
    ///     Swaps movement onto the pre-computed standby route without stopping: vnav.Stop +
    ///     FollowPath is an instant handoff — no recalculation pause.
    /// </summary>
    private bool TrySwapToStandbyPath()
    {
        if (standbyPathTask is not { IsCompleted: true } task)
        {
            return false;
        }

        List<System.Numerics.Vector3>? nodes;
        try
        {
            nodes = task.Result;
        }
        catch
        {
            nodes = null;
        }

        standbyPathTask = null;
        if (nodes == null || nodes.Count < 2)
        {
            return false;
        }

        vnav.Stop();  // Stop current vnavmesh movement instantly
        vnav.FollowPath(nodes, fly: false);  // Start new route immediately
        return true;
    }

    /// <returns>False while still waiting; true when ready to teleport.</returns>
    private bool WaitForBaseTeleportDelay()
    {
        if (config.MaxBaseTeleportDelaySeconds <= 0)
        {
            return true;
        }

        if (!memory.TryRemember<BaseTeleportDelayMemory>(out BaseTeleportDelayMemory delay))
        {
            delay = new BaseTeleportDelayMemory(BaseTeleportDelay.Roll(config));
            if (delay.Delay <= TimeSpan.Zero)
            {
                return true;
            }

            memory.TryAdd(delay);
            logger.Debug("Waiting {Seconds:F1}s at camp before teleport", delay.Delay.TotalSeconds);
            return false;
        }

        return delay.IsReady();
    }

    private void BeginMountThenPause(string reason)
    {
        if (!movement.ShouldAutoMount || conditions[ConditionFlag.Mounted])
        {
            PauseForManualPathing(reason);
            return;
        }

        pendingPauseReason = reason;
        mountBeforePauseDeadline = DateTime.UtcNow + MountBeforePauseTimeout;
        if (!conditions[ConditionFlag.Mounting])
        {
            MountWait.TryCast(movement.PreferredMountId);
        }
    }

    /// <returns>True when this frame handled the mount-before-pause wait.</returns>
    private bool FinishMountBeforePause()
    {
        if (pendingPauseReason == null)
        {
            return false;
        }

        if (conditions[ConditionFlag.Mounted]
            || !movement.ShouldAutoMount
            || DateTime.UtcNow >= mountBeforePauseDeadline)
        {
            string reason = pendingPauseReason;
            pendingPauseReason = null;
            PauseForManualPathing(reason);
            return true;
        }

        if (!conditions[ConditionFlag.Mounting]
            && EzThrottler.Throttle("Pathfinding::MountBeforePause", 750))
        {
            MountWait.TryCast(movement.PreferredMountId);
        }

        return true;
    }

    private static string TeleportOnlyMessage(string where) =>
        $"Stop after return and teleport: {where} — paused so you can walk the rest "
        + "(Illegal Mode → Stop after return and teleport; toggle Illegal Mode to resume)";

    private void ReplanAfterPathCancel(string reason)
    {
        logger.Debug("{Reason} — dropping route for replan", reason);
        pathfinder.Stop();
        currentPathTask = null;
        standbyPathTask = null;
        pendingPauseReason = null;
        memory.Forget<GoalPathStepMemory>();
        memory.Forget<BaseTeleportDelayMemory>();
        // GoalMemory kept — Automator.Update rebuilds GoalPathStepMemory.
    }

    private void PauseForManualPathing(string reason)
    {
        logger.Info("{Reason} (toggle Illegal Mode to resume)", reason);
        pathfinder.Stop();
        ResetPathfinding();
        memory.Forget<GoalPathStepMemory>();
        memory.Forget<GoalMemory>();
        memory.Forget<BaseTeleportDelayMemory>();
        memory.TryAdd<NavigationInterruptedMemory>();
    }

    private void ResetPathfinding()
    {
        PathStepSoftStop.Cancel(manager);

        currentPathTask = null;
        pendingPauseReason = null;
        pathfinder.Stop();
    }
}
