using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class InFateHandler
(
    IAutomatorMemory memory,
    IFateContext context,
    IObjectTable objects,
    ICondition conditions,
    IPathfinder pathfinder,
    AutoRotationController autoRotation,
    IPlayer playerState,
    AutomatorConfig config,
    IFateRepository fates,
    ILogger<InFateHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.InFate)
{
    public override StatePriority GetScore()
    {
        if (!memory.TryRemember<GoalMemory>(out GoalMemory goal) || goal.Goal.GoalType is not FateGoal fateGoal)
        {
            return StatePriority.Never;
        }

        if (context.GetFateId() != fateGoal.id)
        {
            return StatePriority.Never;
        }

        // Already handed off once — stay In FATE while EventId matches even if AI dodges
        // outside the 25y handoff ring (otherwise travel stays suspended and Pathfinding
        // cannot walk back in; manual re-entry also never re-scores In FATE).
        if (memory.TryRemember<SuspendTravelForActivityMemory>(out SuspendTravelForActivityMemory _))
        {
            return StatePriority.VeryHigh;
        }

        // First entry: AI cannot pick up a FATE from the registration rim. Stay in
        // Pathfinding until close enough for AutoTarget / StayCloseToTarget.
        // Combat None walks to mobs from here (no handoff gate).
        if (config.CombatAutorotation.UsesCombatAutomation()
            && !context.IsInCombatWith(fateGoal.id)
            && objects.LocalPlayer is { } player
            && !IsWithinAiHandoff(player, fateGoal.id))
        {
            return StatePriority.Never;
        }

        return StatePriority.VeryHigh;
    }

    public override void Enter()
            {
                base.Enter();
                memory.TryAdd(new SuspendTravelForActivityMemory());
                memory.Forget<GoalPathStepMemory>();
                pathfinder.Stop();
                autoRotation.EnableForFate();

                // Apply humanizing randomization on FATE start
                bool isMelee = playerState.IsMelee();
                config.ApplyRandomization(isMelee);

                logger.Info("Entered FATE {Id} — travel suspended", context.GetFateId()?.Value.ToString() ?? "?");
            }

    public override void Exit(AutomatorState next)
    {
        // Drop SuspendTravel first so DisableAi actually turns combat off while down.
        // After raise, Pathfinding walks back to handoff if EventId alone is not enough.
        if (next == AutomatorState.Dead)
        {
            memory.Forget<SuspendTravelForActivityMemory>();
            autoRotation.DisableAi();
            logger.Info("Died in FATE — clearing travel suspend for raise");
            base.Exit(next);
            return;
        }

        memory.Forget<SuspendTravelForActivityMemory>();
        autoRotation.DisableAi();
        logger.Info("Left FATE — travel resumed");
        base.Exit(next);
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        if (config.CombatAutorotation.UsesCombatAutomation())
        {
            DismountAssist.TryDismount(conditions);
            if (!pathfinder.IsIdle())
            {
                pathfinder.Stop();
            }

            return;
        }

        List<IBattleNpc> fateTargets = context.GetTargets().ToList();
        InitialCombatApproachMemory<FateId> approach = GetApproachMemory(context.GetFateId());
        if (CombatActivityHandler.HandleTargets(
                player,
                playerState,
                fateTargets,
                conditions,
                pathfinder,
                "InFate",
                approach.IsPending,
                stopPathfinderInCombat: true))
        {
            approach.Complete();
        }
    }

    private InitialCombatApproachMemory<FateId> GetApproachMemory(FateId? fateId)
    {
        if (!memory.TryRemember(out InitialCombatApproachMemory<FateId> approach))
        {
            approach = new();
            memory.TryAdd(approach);
        }

        approach.Track(fateId);
        return approach;
    }

    private bool IsWithinAiHandoff(IGameObject player, FateId id)
    {
        float nearest = float.MaxValue;
        foreach (IBattleNpc target in context.GetTargets())
        {
            nearest = MathF.Min(nearest, player.Position.Distance2D(target.Position) - target.HitboxRadius);
        }

        Fate? live = fates.Snapshot().FirstOrDefault(f => f.Id.Value == id.Value);
        float toCenter = live != null ? player.Position.Distance2D(live.Position) : float.MaxValue;
        return NavigationConstants.IsWithinFateAiHandoff(toCenter, nearest);
    }
}
