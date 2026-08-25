using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Shopping;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Debug;
using Dalamud.Bindings.ImGui;
using BOCCHI.Services.Shopping;
using Ocelot.Services.UI;

namespace BOCCHI.Debug.Panels;

/// <summary>
///     Lives in the main assembly (needs ShoppingService) but implements IDebugPanel from
///     BOCCHI.Debug, which discovers panels via DI. Live view of the shopping pipeline:
///     phase/status, configured targets vs the currently open shop, purchase state — the
///     fastest way to answer "why is it not buying?".
/// </summary>
public sealed class ShoppingDebugPanel(
    ShoppingConfig config,
    ShoppingService shopping,
    ShopInspectorController inspector,
    ShopPurchaseController purchases,
    IZoneProvider zones,
    IUIService ui
) : IDebugPanel
{
    public string Name => "Shopping";

    public void Render()
    {
        ui.LabelledValue("Phase", shopping.IsRunning ? "running" : "idle");
        ui.LabelledValue("Status", shopping.Status);
        ui.LabelledValue("Trigger", shopping.TriggerStatus);

        var zone = zones.GetZone();
        ui.LabelledValue("Zone", zone.ZoneId.ToString());

        var snap = inspector.Snapshot;
        ui.LabelledValue("Vendor menu open", snap.IsSelectIconStringOpen);
        ui.LabelledValue("Shop window open", snap.IsShopExchangeCurrencyOpen);
        if (snap.IsShopExchangeCurrencyOpen)
        {
            ui.LabelledValue("Live tab", snap.SelectedTabId.ToString());
            ui.LabelledValue("Currency id", snap.CurrencyItemId.ToString());
            ui.LabelledValue("Currency held", snap.CurrencyAmount.ToString());
            ui.LabelledValue("Live entries", snap.ShopEntries.Count.ToString());
        }

        ImGui.Separator();
        ui.Text("Purchase");
        ui.LabelledValue("Busy", purchases.IsBusy);
        ui.LabelledValue("Last result", $"{purchases.LastCompletionKind}: {purchases.LastStatus}");

        ImGui.Separator();
        ui.Text($"Targets ({config.Targets.Count})");
        foreach (var t in config.Targets)
        {
            var name = ShopCatalog.TryGet(t.ItemId, out var def) ? def.Name : $"Item {t.ItemId}";
            ui.LabelledValue(
                name,
                $"[{t.TerritoryKey}] menu={t.MenuIndex} tab={t.TabId} keep={t.KeepAmount} buy={t.BuyAmount}"
                + (t.KeepBuying ? " KEEP-BUYING" : string.Empty)
                + $" prio={t.Priority}");
        }

        if (snap.ShopEntries.Count > 0)
        {
            ImGui.Separator();
            ui.Text("Live shop rows");
            foreach (var e in snap.ShopEntries)
            {
                ui.LabelledValue($"{e.ItemName} ({e.ItemId})", $"cost={e.Cost} row={e.RowIndex} tab={e.TabId}");
            }
        }
    }
}
