using BOCCHI.Automator.Services;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Extensions;
using Ocelot.Lifecycle;

namespace BOCCHI.Services;

public class SupportJobChanger
(
    IChainManager chainManager,
    ISupportJobFactory supportJobs,
    IObjectTable objects,
    ICondition conditions
) : ISupportJobChanger, IOnUpdate
{
    private Task<ChainResult>? task;

    public void Update()
    {
        if (task?.IsCompleted == true)
        {
            task = null;
        }
    }

    public void Change(SupportJobId id)
    {
        if (task != null)
        {
            return;
        }

        if (PhantomJobChangeGate.IsBlocked(conditions))
        {
            return;
        }

        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        SupportJob job = supportJobs.Create(id);

        task = chainManager.ExecuteAsync(chains =>
        {
            return chains.Create("SupportJobChanger")
                .IfThen(
                    _ => supportJobs.TryGetCurrent(out SupportJob current) && current.Id == id,
                    _ => ValueTask.FromResult(StepResult.Break()),
                    "SupportJobChanger::CheckCurrentJob"
                )
                .Then(_ => PublicContentOccultCrescent.ChangeSupportJob((byte)id), "SupportJobChanger::Change")
                .WaitUntil(
                    _ => ValueTask.FromResult(player.StatusList.Has(job.StatusId)),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromMilliseconds(250),
                    "SupportJobChanger::WaitForChange"
                );
        });
    }

    public bool IsBusy() => task is { IsCompleted: false };
}
