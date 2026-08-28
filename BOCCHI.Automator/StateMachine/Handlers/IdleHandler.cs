using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using BOCCHI.Treasure.Services;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.States.Score;
using Ocelot.Windows;
using System.Numerics;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class IdleHandler(
    IAutomatorMemory memory,
    IZoneProvider zones,
    IObjectTable objects,
    IPathfinder pathfinder,
    IVNavmeshIpc vnav,
    IChainManager chains,
    AutomatorConfig config,
    MovementConfig movement,
    AutoRotationController autoRotation,
    IUIService ui,
    ITranslator<MainWindow> translator,
    ITreasureHunter hunter
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Idle)
{
    public override StatePriority GetScore() => StatePriority.Lowest;

    public override void Enter()
    {
        base.Enter();
        PathStepSoftStop.Cancel(chains);
        // Survey / World PathTo use ActivityGoto chains — don't kill them on idle handoff.
        // Map-hunt filler keeps Automator Idle so FATE/CE can interrupt; don't Stop() its vnav.
        if (!IsNavigationInterrupted() && !IsIllegalModeMapHuntFillerActive())
        {
            StopMovement();
        }

        autoRotation.DisableAi();
        memory.TryAdd(new IdleStateMemory(ReturnDelay.Roll(config)));
    }

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);

        // Keep the latch when handing off to Returning: ReturningHandler re-scores from it on the
        // next tick, and dropping it here made that tick score Never, bounce straight back to Idle
        // and roll a brand new wait — so the opportunistic Return could never actually fire.
        if (next != AutomatorState.Returning)
        {
            memory.Forget<IdleStateMemory>();
        }

        if (!IsNavigationInterrupted())
        {
            StopMovement();
        }
    }

    public override void Handle()
    {
        if (IsNavigationInterrupted())
        {
            return;
        }

        // Stay Idle (so a FATE/CE can still score) but do not park / Stop() while the hunt leaves camp.
        if (IsIllegalModeMapHuntFillerActive())
        {
            return;
        }

        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        IZone zone = zones.GetZone();
        if (!zone.IsInBasecamp())
        {
            return;
        }

        if (!memory.TryRemember<IdleStateMemory>(out IdleStateMemory idle))
        {
            return;
        }

        // Inside cyan (idle band or closer / magenta) — stop; do not path into the crystal.
        if (zone.IsWithinIdleWait(player.Position))
        {
            idle.ApproachCandidateIndex = 0;
            StopMovement();
            return;
        }

        if (!pathfinder.IsIdle())
        {
            return;
        }

        // Path to spots spread between magenta (Lifestream) and cyan (idle outer).
        // Shuffle once per idle session so clients don't all take the nearest tile first.
        if (idle.WaitCandidates is not { Count: > 0 })
        {
            List<Vector3> built = zone.GetIdleWaitCandidates(player.Position).ToList();
            ShuffleInPlace(built);
            idle.WaitCandidates = built;
            idle.ApproachCandidateIndex = 0;
        }

        List<Vector3> candidates = idle.WaitCandidates;
        if (candidates.Count == 0)
        {
            return;
        }

        if (idle.ApproachCandidateIndex >= candidates.Count)
        {
            idle.ApproachCandidateIndex = 0;
        }

        Vector3 target = candidates[idle.ApproachCandidateIndex];
        idle.ApproachCandidateIndex++;

        SprintAssist.MaybeCast(movement.SprintOnAetheryteApproach, inBasecamp: true);
        pathfinder.PathfindAndMoveTo(new PathfinderConfig(target)
        {
            DistanceThreshold = AethernetNavigation.PathfindArrivalRadius,
            ShouldSnapToFloor = false,
        });
    }

    public override void Render()
    {
        base.Render();

        if (memory.TryRemember<IdleStateMemory>(out IdleStateMemory idle))
        {
            ui.LabelledValue(translator.T(".automation.automator.time_idle"), idle.GetIdleTime().Format());
        }
    }

    private void StopMovement()
    {
        pathfinder.Stop();
        vnav.Stop();
    }

    private bool IsNavigationInterrupted() =>
        memory.TryRemember<NavigationInterruptedMemory>(out NavigationInterruptedMemory _);

    /// <summary>
    ///     Map hunts (no Treasure Sight) keep Automator awake. While that hunt is moving, Idle must
    ///     not Stop() vnav — that re-queues the same camp→coffer path every tick.
    /// </summary>
    private bool IsIllegalModeMapHuntFillerActive() =>
        hunter.ManagedByIllegalModeFiller && hunter.Running && !hunter.Paused;

    private static void ShuffleInPlace(List<Vector3> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
