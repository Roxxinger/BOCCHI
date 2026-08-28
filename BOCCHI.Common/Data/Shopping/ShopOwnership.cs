using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace BOCCHI.Common.Data.Shopping;

/// <summary>
/// Owned / unlocked checks for shop badges, and purchase blocking for owned untradeables.
/// </summary>
public static unsafe class ShopOwnership
{
    public static bool? TryIsOwned(
        ShopCatalogEntry entry,
        ISupportJobFactory supportJobs,
        IDataManager data,
        IUnlockState unlockState)
    {
        return entry.Ownership switch
        {
            ShopOwnershipKind.Repeatable => null,
            ShopOwnershipKind.PhantomJob => entry.PhantomJob is { } job
                && supportJobs.Create(job).Level >= 1,
            ShopOwnershipKind.KeyItem => TryItemUnlockOwned(entry.ItemId, data, unlockState),
            ShopOwnershipKind.Armor => OwnsArmor(entry),
            ShopOwnershipKind.Minion
                or ShopOwnershipKind.Mount
                or ShopOwnershipKind.Orchestrion
                or ShopOwnershipKind.Emote
                or ShopOwnershipKind.Hairstyle
                or ShopOwnershipKind.Facewear
                or ShopOwnershipKind.FramersKit
                or ShopOwnershipKind.TripleTriad => TryItemUnlockOwned(entry.ItemId, data, unlockState),
            _ => InventoryItemAssist.Has(entry.ItemId, includeKeyItems: true)
                 ? true
                 : null,
        };
    }

    /// <summary>
    /// Skip auto-buy / lock list inputs when owned and the item cannot be traded
    /// (extra copies aren't useful). Owned tradeables stay buyable.
    /// </summary>
    public static bool ShouldBlockPurchase(
        ShopCatalogEntry entry,
        ISupportJobFactory supportJobs,
        IDataManager data,
        IUnlockState unlockState)
    {
        if (TryIsOwned(entry, supportJobs, data, unlockState) != true)
        {
            return false;
        }

        return IsUntradable(entry.ItemId, data);
    }

    public static bool IsUntradable(uint itemId, IDataManager data)
    {
        if (!data.GetExcelSheet<Item>().TryGetRow(itemId, out Item item))
        {
            // Unknown row — treat as untradeable when owned (safer).
            return true;
        }

        return item.IsUntradable;
    }

    private static bool? TryItemUnlockOwned(uint itemId, IDataManager data, IUnlockState unlockState)
    {
        if (InventoryItemAssist.Has(itemId, includeKeyItems: true))
        {
            return true;
        }

        if (!data.GetExcelSheet<Item>().TryGetRow(itemId, out Item item))
        {
            return null;
        }

        try
        {
            if (!unlockState.IsItemUnlockable(item))
            {
                return null;
            }

            return unlockState.IsItemUnlocked(item);
        }
        catch
        {
            // Unlock state / companion bit array may throw if ids are wrong or out of world.
            return null;
        }
    }

    private static bool OwnsArmor(ShopCatalogEntry entry)
    {
        if (HasAnywhere(entry.ItemId))
        {
            return true;
        }

        if (entry.UpgradeItemIds is not { Length: > 0 })
        {
            return false;
        }

        foreach (uint id in entry.UpgradeItemIds)
        {
            if (HasAnywhere(id))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnywhere(uint itemId) =>
        InventoryItemAssist.Has(itemId) || IsEquipped(itemId);

    private static bool IsEquipped(uint itemId)
    {
        InventoryManager* inv = InventoryManager.Instance();
        if (inv == null)
        {
            return false;
        }

        for (InventoryType type = InventoryType.EquippedItems; type <= InventoryType.EquippedItems; type++)
        {
            InventoryContainer* bag = inv->GetInventoryContainer(type);
            if (bag == null)
            {
                continue;
            }

            for (int i = 0; i < bag->Size; i++)
            {
                InventoryItem* slot = bag->GetInventorySlot(i);
                if (slot != null && slot->ItemId == itemId)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
