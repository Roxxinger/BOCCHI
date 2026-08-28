using BOCCHI.Common.Config;
using BOCCHI.Common.Services;
using BOCCHI.Common.Steps;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Ocelot.Chain;
using Ocelot.Services.PlayerState;

namespace BOCCHI.Services.Repair;

public class RepairService(
    IChainFactory chains,
    AutomatorConfig config,
    IObjectTable objects,
    IDataManager data,
    IPlayer player
) : IRepairService
{
    public unsafe bool ShouldRepair()
    {
        if (!TryGetEquipped(out InventoryContainer* equipped))
        {
            return false;
        }

        for (int i = 0; i < equipped->Size; i++)
        {
            InventoryItem* item = equipped->GetInventorySlot(i);
            if (item is null)
            {
                continue;
            }

            if (Convert.ToInt32(Convert.ToDouble(item->Condition) / 30000.0 * 100.0) <= config.AutoRepairThreshold)
            {
                return true;
            }
        }

        return false;
    }

    public IChain Repair()
    {
        IChain chain = chains.Create("Repairs");
        chain.Then<UnmountStep>();

        if (ShouldUseMender())
        {
            chain.Then<NpcRepairStep>();
        }
        else
        {
            chain.Then<RepairStep>();
        }

        return chain;
    }

    private bool ShouldUseMender()
    {
        bool menderNearby = RepairNpc.TryFindNearby(
            objects,
            data,
            player.Position,
            out _,
            out _);

        return config.AutoRepairMethod switch
        {
            AutoRepairMethod.MenderNpc => true,
            AutoRepairMethod.PreferMender => menderNearby,
            _ => false,
        };
    }

    private static unsafe bool TryGetEquipped(out InventoryContainer* equipped)
    {
        equipped = null;

        InventoryManager* inventory = InventoryManager.Instance();
        if (inventory == null)
        {
            return false;
        }

        equipped = inventory->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped == null || !equipped->IsLoaded)
        {
            equipped = null;
            return false;
        }

        return true;
    }
}
