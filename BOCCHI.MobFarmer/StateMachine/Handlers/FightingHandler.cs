using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Targeting;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Flow;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class FightingHandler
(
    MobFarmerConfig config,
    AutomatorConfig automatorConfig,
    MovementConfig movementConfig,
    IMobFarmer farmer,
    IFarmerCombatController combat,
    IMobScanner scanner,
    ITargetManager targets,
    ICondition conditions,
    IObjectTable objects,
    IPathfinder pathfinder,
    IZoneProvider zones,
    IPlayer player
) : FlowStateHandler<FarmerPhase>(FarmerPhase.Fighting)
{
    public override void Enter()
    {
        base.Enter();
        // Stacking may still be walking — BossMod AI cannot dodge while vnav owns movement.
        pathfinder.Stop();
        combat.EnableFighting();
    }

    public override void Exit(FarmerPhase next)
    {
        combat.Disable();
        base.Exit(next);
    }

    public override FarmerPhase? Handle()
    {
        List<IBattleNpc> inCombat = scanner.InCombat.ToList();
        bool anyInCombat = inCombat.Count > 0;
        bool useAi = automatorConfig.CombatAutorotation.UsesCombatAutomation();

        if (anyInCombat || conditions[ConditionFlag.InCombat])
        {
            combat.EnableFighting();
            if (DismountAssist.TryDismount(conditions))
            {
                return null;
            }

            // Auto-target during pull only; with combat AI on, leave targeting to the AI.
            if (!useAi
                && config.ShouldHandleTargeting
                && inCombat.Count > 0
                && EzThrottler.Throttle("MobFarmer::Fighting::Target", 250))
            {
                IBattleNpc? target = TargetHelper.Select(inCombat, player.Position, config.ForceTargetCentralEnemy);
                if (target != null)
                {
                    targets.Target = target;
                }
            }

            if (useAi)
            {
                pathfinder.Stop();
            }

            return null;
        }

        combat.Disable();

        bool shouldReturnHome = config.ReturnToStartInWaitingPhase
                                && player.Position.Distance2D(farmer.StartingPoint) >= config.MinEuclideanDistanceToReturnHome;

        if (shouldReturnHome)
        {
            if (pathfinder.GetState() == PathfindingState.Idle)
            {
                pathfinder.PathfindAndMoveTo(new(farmer.StartingPoint)
                {
                    AllowFlying = false
                });
            }

            MountWait.TryCastIfNeeded(
                conditions,
                objects,
                farmer.StartingPoint,
                movementConfig.ShouldAutoMount,
                movementConfig.PreferredMountId,
                zones.GetZone().IsInBasecamp());

            return player.Position.Distance2D(farmer.StartingPoint) <= 2f ? FarmerPhase.Waiting : null;
        }

        pathfinder.Stop();
        return FarmerPhase.Waiting;
    }
}
