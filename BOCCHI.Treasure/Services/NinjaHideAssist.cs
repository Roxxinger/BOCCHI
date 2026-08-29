using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Services.PlayerState;
using ActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;
using ECommonsPlayer = ECommons.GameHelpers.Player;

namespace BOCCHI.Treasure.Services;

/// <summary>
///     Real Ninja Hide (2245 / status 614) + optional gearset swap for dangerous coffer approaches.
///     Optional Phantom Thief Occult Sprint while stealthed.
/// </summary>
public sealed class NinjaHideAssist(
    IPlayer player,
    ICondition conditions,
    ISupportJobFactory supportJobs,
    IPluginLog log)
{
    public const uint HideActionId = 2245;

    public const uint HiddenStatusId = 614;

    public const uint NinjaClassJobId = 30;

    private static readonly Ocelot.Actions.Action Hide = new(ActionType.Action, HideActionId);

    private static Ocelot.Actions.Action OccultSprint => new(ActionType.Action, PhantomActions.OccultSprint);

    private int? gearsetBeforeNinja;

    private SupportJobId? supportJobBeforeThief;

    public bool IsStealthed =>
        player.PlayerCharacter?.StatusList.Has(HiddenStatusId) == true;

    public bool IsNinja =>
        player.GetClassJob()?.RowId == NinjaClassJobId;

    public bool IsMounted =>
        conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting];

    /// <summary>
    ///     Prepare stealth for a dangerous walk. Returns false while still equipping / dismounting / casting.
    /// </summary>
    public bool EnsureReady(int ninjaGearsetNumber)
    {
        // Hide / gearset mid-fight fails and fights with combat movement.
        if (conditions[ConditionFlag.InCombat])
        {
            return false;
        }

        if (!IsNinja)
        {
            if (ninjaGearsetNumber <= 0)
            {
                return false;
            }

            RememberCurrentGearsetIfNeeded();
            TryEquipGearset(ninjaGearsetNumber);
            return false;
        }

        // Dismount jump/landing — Hide fails with "while jumping".
        if (ECommonsPlayer.IsJumping || DismountAssist.TryDismount(conditions))
        {
            return false;
        }

        if (IsStealthed)
        {
            return true;
        }

        TryCastHide();
        return false;
    }

    /// <summary>
    ///     Best-effort: Phantom Thief + Occult Sprint while Hide is up. Never blocks walking.
    /// </summary>
    public void TryOccultSprintWhileHidden()
    {
        if (!IsStealthed
            || conditions[ConditionFlag.InCombat]
            || conditions[ConditionFlag.Casting]
            || conditions[ConditionFlag.Casting87]
            || conditions[ConditionFlag.BetweenAreas]
            || conditions[ConditionFlag.BetweenAreas51]
            || ECommonsPlayer.IsJumping)
        {
            return;
        }

        if (!EnsurePhantomThief())
        {
            return;
        }

        if (!EzThrottler.Throttle("NinjaHide::OccultSprint", 750) || !OccultSprint.CanCast())
        {
            return;
        }

        OccultSprint.Cast();
    }

    /// <summary>Swap back to the job/gearset used before the Ninja Hide flow, if we changed it.</summary>
    public void RestorePreviousGearsetIfNeeded()
    {
        RestorePreviousSupportJobIfNeeded();

        if (gearsetBeforeNinja is not int previous || previous <= 0)
        {
            return;
        }

        if (TryGetActiveGearsetNumber() == previous)
        {
            gearsetBeforeNinja = null;
            return;
        }

        TryEquipGearsetNumber(previous, requireNinja: false);
        if (TryGetActiveGearsetNumber() == previous)
        {
            gearsetBeforeNinja = null;
        }
    }

    /// <summary>
    ///     Drop Hide prep before coffer / carrot interact so the treasure job and gearset are restored.
    /// </summary>
    public void EndStealthForInteract()
    {
        RestorePreviousGearsetIfNeeded();
    }

    /// <summary>Restore phantom job remembered before Occult Sprint Thief swap.</summary>
    public void RestorePreviousSupportJobIfNeeded()
    {
        if (supportJobBeforeThief is not { } restoreId)
        {
            return;
        }

        if (supportJobs.TryGetCurrent(out SupportJob current) && current.Id == restoreId)
        {
            supportJobBeforeThief = null;
            return;
        }

        if (!TryChangeSupportJob(restoreId))
        {
            return;
        }

        if (supportJobs.TryGetCurrent(out SupportJob after) && after.Id == restoreId)
        {
            supportJobBeforeThief = null;
        }
    }

    private bool EnsurePhantomThief()
    {
        if (supportJobs.TryGetCurrent(out SupportJob current)
            && current.Id == SupportJobId.PhantomThief)
        {
            return true;
        }

        RememberSupportJobIfNeeded();
        return TryChangeSupportJob(SupportJobId.PhantomThief)
               && supportJobs.TryGetCurrent(out SupportJob after)
               && after.Id == SupportJobId.PhantomThief;
    }

    private void RememberSupportJobIfNeeded()
    {
        if (supportJobBeforeThief != null)
        {
            return;
        }

        if (supportJobs.TryGetCurrent(out SupportJob current)
            && current.Id != SupportJobId.PhantomThief)
        {
            supportJobBeforeThief = current.Id;
        }
    }

    private unsafe bool TryChangeSupportJob(SupportJobId id)
    {
        if (!EzThrottler.Throttle($"NinjaHide::SupportJob::{id}", 750))
        {
            return false;
        }

        PublicContentOccultCrescent.ChangeSupportJob((byte)id);
        return supportJobs.TryGetCurrent(out SupportJob current) && current.Id == id;
    }

    /// <summary>
    ///     Drops Hide when travel no longer needs stealth (Hide toggles off).
    ///     Returns true when not stealthed and safe to mount.
    /// </summary>
    public bool TryEndStealthForTravel()
    {
        if (!IsStealthed)
        {
            return true;
        }

        if (conditions[ConditionFlag.InCombat] || ECommonsPlayer.IsJumping || IsMounted)
        {
            return false;
        }

        if (!IsNinja)
        {
            return true;
        }

        if (!EzThrottler.Throttle("NinjaHide::EndHide", 750))
        {
            return false;
        }

        if (Hide.CanCast())
        {
            Hide.Cast();
        }

        return !IsStealthed;
    }

    private void TryCastHide()
    {
        if (!IsNinja || IsStealthed || IsMounted || ECommonsPlayer.IsJumping)
        {
            return;
        }

        if (!EzThrottler.Throttle("NinjaHide::Cast", 750) || !Hide.CanCast())
        {
            return;
        }

        Hide.Cast();
    }

    private bool TryEquipGearset(int gearsetNumber) =>
        TryEquipGearsetNumber(gearsetNumber, requireNinja: true);

    private void RememberCurrentGearsetIfNeeded()
    {
        if (gearsetBeforeNinja != null)
        {
            return;
        }

        if (TryGetActiveGearsetNumber() is int current && current > 0)
        {
            gearsetBeforeNinja = current;
        }
    }

    private unsafe int? TryGetActiveGearsetNumber()
    {
        RaptureGearsetModule* module = RaptureGearsetModule.Instance();
        if (module == null)
        {
            return null;
        }

        int slot = module->CurrentGearsetIndex;
        return slot >= 0 ? slot + 1 : null;
    }

    private unsafe bool TryEquipGearsetNumber(int gearsetNumber, bool requireNinja)
    {
        if (gearsetNumber <= 0)
        {
            return false;
        }

        if (requireNinja && IsNinja)
        {
            return true;
        }

        if (!EzThrottler.Throttle("NinjaHide::Gearset", 1500))
        {
            return false;
        }

        RaptureGearsetModule* module = RaptureGearsetModule.Instance();
        if (module == null)
        {
            log.Warning("Ninja Hide: RaptureGearsetModule unavailable");
            return false;
        }

        // Gearset numbers are 1-based in the UI; module slots are 0-based.
        int slot = gearsetNumber - 1;
        if (!module->IsValidGearset(slot))
        {
            log.Warning("Ninja Hide: gearset {Number} is invalid or empty", gearsetNumber);
            return false;
        }

        RaptureGearsetModule.GearsetEntry* entry = module->GetGearset(slot);
        if (entry == null || entry->ClassJob == 0)
        {
            log.Warning("Ninja Hide: gearset {Number} missing or empty", gearsetNumber);
            return false;
        }

        if (requireNinja && entry->ClassJob != NinjaClassJobId)
        {
            log.Warning(
                "Ninja Hide: gearset {Number} is ClassJob {Job}, expected Ninja ({Ninja})",
                gearsetNumber,
                entry->ClassJob,
                NinjaClassJobId);
            return false;
        }

        int result = module->EquipGearset(slot);
        if (result != 0)
        {
            log.Warning("Ninja Hide: EquipGearset({Slot}) returned {Result}", slot, result);
            return false;
        }

        // Equip is async — caller polls active gearset / job.
        return false;
    }
}
