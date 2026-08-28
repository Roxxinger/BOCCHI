using BOCCHI.Buff.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Extensions;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.States.Flow;

namespace BOCCHI.Buff.StateMachine.Handlers.ApplyingBuff;

public abstract class BaseHandler
(
    BuffState state,
    IBuffProvider buffs,
    IObjectTable objects,
    ICondition conditions,
    ISupportJobFactory supportJobs,
    ISupportJobChanger changer
) : FlowStateHandler<BuffState>(state)
{
    private readonly BuffState state = state;

    private DateTime lastCast = DateTime.MinValue;

    public override void Enter()
    {
        base.Enter();
        lastCast = DateTime.MinValue;
    }

    public override BuffState? Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return null;
        }

        BuffData buff = GetBuffData();
        if (player.GetRemainingMinutes(buff.StatusId) >= 29)
        {
            return BuffState.ChoosingBuffToApply;
        }

        // Don't gate on CanCast — mounted at crystal with a blocked dismount soft-locks.
        if (DismountAssist.TryDismount(conditions))
        {
            return null;
        }

        if (!(supportJobs.TryGetCurrent(out SupportJob supportJob) && supportJob.Id == GetBuffData().SupportJobId))
        {
            if (!changer.IsBusy())
            {
                changer.Change(buff.SupportJobId);
            }

            return null;
        }

        TimeSpan time = DateTime.UtcNow - lastCast;
        if (buff.Action.CanCast() && time.TotalSeconds >= 3)
        {
            lastCast = DateTime.UtcNow;
            buff.Action.Cast();
        }

        return null;
    }

    private BuffData GetBuffData() => buffs.GetBuffForState(state);
}
