using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Actions;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Chain.Middleware.Chain;
using Ocelot.Chain.Middleware.Step;
using Ocelot.Extensions;

namespace BOCCHI.Treasure.ChainRecipes;

/// <summary>
///     Dismount → Freelancer → Treasure Sight (Phantom Action II) → restore previous phantom job.
/// </summary>
public class HuntTreasureSightChain
(
    IChainFactory chains,
    ICondition conditions,
    ISupportJobFactory supportJobs,
    IObjectTable objects
) : ChainRecipe(chains)
{
    private static readonly TimeSpan SightCooldownGrace = TimeSpan.FromSeconds(4);

    public override string Name => "Hunt Treasure Sight";

    private SupportJobId? restoreAfterSight;

    private bool restoreCaptured;

    private bool sightCastSkippedCd;

    protected override IChain Compose(IChain chain)
    {
        restoreAfterSight = null;
        restoreCaptured = false;
        sightCastSkippedCd = false;

        SupportJob freelancer = supportJobs.Create(SupportJobId.PhantomFreelancer);
        var castState = new CastState();

        return chain
            .UseMiddleware<LogChainMiddleware>()
            .UseMiddleware(new RestorePhantomJobMiddleware(this))
            .UseStepMiddleware<LogStepMiddleware>()
            .UseStepMiddleware<RunOnMainThreadMiddleware>()
            .IfThen(
                _ =>
                {
                    CaptureRestoreIfNeeded();
                    return !SupportJobTreasureSight.CanCast(supportJobs);
                },
                _ => ValueTask.FromResult(StepResult.Break()),
                "HuntTreasureSight::FreelancerTooLow"
            )
            .WaitUntil(
                _ =>
                {
                    CaptureRestoreIfNeeded();
                    return ValueTask.FromResult(IsOnFoot());
                },
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(250),
                "HuntTreasureSight::Dismount"
            )
            .WaitUntil(
                _ =>
                {
                    CaptureRestoreIfNeeded();
                    return ValueTask.FromResult(TryBecomeJob(SupportJobId.PhantomFreelancer, freelancer.StatusId));
                },
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(250),
                "HuntTreasureSight::ToFreelancer"
            )
            .WaitUntil(
                _ => ValueTask.FromResult(TryCastSight(castState)),
                TimeSpan.FromSeconds(20),
                TimeSpan.FromMilliseconds(250),
                "HuntTreasureSight::Cast"
            )
            .WaitUntil(
                _ => ValueTask.FromResult(TryRestore(restoreAfterSight)),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(250),
                "HuntTreasureSight::RestoreJob"
            );
    }

    /// <summary>Ready when on foot and not in the dismount landing beat.</summary>
    private bool IsOnFoot() => !DismountAssist.TryDismount(conditions);

    private void CaptureRestoreIfNeeded()
    {
        if (restoreCaptured)
        {
            return;
        }

        restoreCaptured = true;
        if (supportJobs.TryGetCurrent(out SupportJob current)
            && current.Id != SupportJobId.PhantomFreelancer)
        {
            restoreAfterSight = current.Id;
        }
    }

    private bool TryBecomeJob(SupportJobId id, uint statusId)
    {
        if (objects.LocalPlayer is not { } player)
        {
            return false;
        }

        if (supportJobs.TryGetCurrent(out SupportJob current) && current.Id == id)
        {
            return true;
        }

        if (PhantomJobChangeGate.IsBlocked(conditions))
        {
            return false;
        }

        if (!EzThrottler.Throttle($"HuntTreasureSight::Change::{id}", 750))
        {
            return false;
        }

        unsafe
        {
            PublicContentOccultCrescent.ChangeSupportJob((byte)id);
        }

        return player.StatusList.Has(statusId);
    }

    /// <summary>
    /// Start Treasure Sight and wait until the cast finishes.
    /// Returning true on UseAction alone restored the previous job mid-cast and cancelled Sight.
    /// </summary>
    private bool TryCastSight(CastState state)
    {
        CaptureRestoreIfNeeded();

        if (IsCasting())
        {
            state.SawCasting = true;
            state.CdBlockedSinceUtc = null;
            return false;
        }

        if (state.SawCasting || state.Issued)
        {
            // Cast completed (or never entered casting for an instant-style success).
            return true;
        }

        // Remount / mount transition after Dismount step — wait instead of burning the cast window.
        if (DismountAssist.TryDismount(conditions))
        {
            return false;
        }

        if (!EzThrottler.Throttle("HuntTreasureSight::Cast", 500))
        {
            return false;
        }

        if (!Actions.PhantomActionII.CanCast())
        {
            state.CdBlockedSinceUtc ??= DateTime.UtcNow;
            if (DateTime.UtcNow - state.CdBlockedSinceUtc.Value >= SightCooldownGrace)
            {
                // Shared action CD (buffs, etc.) — skip the cast but still run restore.
                sightCastSkippedCd = true;
                return true;
            }

            return false;
        }

        state.CdBlockedSinceUtc = null;

        if (Actions.PhantomActionII.Cast())
        {
            state.Issued = true;
        }

        return false;
    }

    private bool IsCasting() =>
        conditions[ConditionFlag.Casting] || conditions[ConditionFlag.Casting87];

    private bool TryRestore(SupportJobId? restoreId)
    {
        // Never swap while still casting — job change cancels Treasure Sight.
        if (IsCasting())
        {
            return false;
        }

        if (restoreId is not { } id)
        {
            return true;
        }

        SupportJob job = supportJobs.Create(id);
        return TryBecomeJob(id, job.StatusId);
    }

    private void TryRestorePhantomJob()
    {
        if (restoreAfterSight is not { } id)
        {
            return;
        }

        if (supportJobs.TryGetCurrent(out SupportJob current) && current.Id != SupportJobId.PhantomFreelancer)
        {
            return;
        }

        SupportJob job = supportJobs.Create(id);
        TryBecomeJob(id, job.StatusId);
    }

    private sealed class RestorePhantomJobMiddleware(HuntTreasureSightChain owner) : IChainMiddleware
    {
        public async Task<ChainResult> InvokeAsync(IChainContext context, ChainMiddlewareDelegate next)
        {
            ChainResult result;
            try
            {
                result = await next();
            }
            finally
            {
                owner.TryRestorePhantomJob();
            }

            if (owner.sightCastSkippedCd)
            {
                return ChainResult.Failure("Treasure Sight on cooldown");
            }

            return result;
        }
    }

    private sealed class CastState
    {
        public DateTime? CdBlockedSinceUtc;

        public bool Issued;

        public bool SawCasting;
    }
}
