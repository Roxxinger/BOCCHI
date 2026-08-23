using System.Runtime.InteropServices;
using BOCCHI.Common.Data.Shopping;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Ocelot.Services.Logger;

namespace BOCCHI.Services.Shopping;

/// <summary>Live entry read from an open ShopExchangeCurrency window.</summary>
public sealed class LiveShopEntry
{
    public uint ItemId { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public uint CurrencyItemId { get; init; }
    public uint Cost { get; init; }
    public uint RowIndex { get; init; }
    public int TabId { get; init; } = -1;
    public uint MaxStackSize { get; init; }
}

/// <summary>Live row of the Antiquarian SelectIconString menu.</summary>
public readonly record struct LiveShopMenuEntry(int Index, string Label);

/// <summary>Snapshot of everything visible in the shop UI right now.</summary>
public sealed class LiveShopSnapshot
{
    public bool IsSelectIconStringOpen { get; init; }
    public bool IsShopExchangeCurrencyOpen { get; init; }
    public int SelectedTabId { get; init; } = -1;
    public uint CurrencyItemId { get; init; }
    public uint CurrencyAmount { get; init; }
    public IReadOnlyList<LiveShopMenuEntry> MenuEntries { get; init; } = [];
    public IReadOnlyList<LiveShopEntry> ShopEntries { get; init; } = [];
}

/// <summary>
///     Reads the live Antiquarian UI state from the ShopExchangeCurrency / SelectIconString
///     addons each framework tick. ATK value offsets mirror AOCCH's verified layout.
/// </summary>
public sealed class ShopInspectorController : IDisposable
{
    private const int ShopExchangeCurrencyNumEntries = 4;
    private const int ShopExchangeCurrencySelectedTab = 1;
    private const int ShopExchangeCurrencyCurrencyAmount = 86;
    private const int ShopExchangeCurrencyCurrencyIcon = 87;
    private const int ShopExchangeCurrencyCostBase = 456;
    private const int ShopExchangeCurrencyItemIdBase = 1066;
    private const int ShopExchangeCurrencyStackSizeBase = 1188;
    private const int ShopExchangeCurrencyRowIndexBase = 1310;

    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IDataManager dataManager;
    private readonly ILogger<ShopInspectorController> logger;
    private readonly object gate = new();
    private readonly Dictionary<uint, uint> itemIdByIcon = new();
    private readonly Dictionary<uint, string> itemNameById = new();

    private LiveShopSnapshot snapshot = new();

    public ShopInspectorController(IFramework framework, IGameGui gameGui, IDataManager dataManager, ILogger<ShopInspectorController> logger)
    {
        this.framework = framework;
        this.gameGui = gameGui;
        this.dataManager = dataManager;
        this.logger = logger;

        BuildCaches(dataManager);
        framework.Update += OnFrameworkUpdate;
    }

    public LiveShopSnapshot Snapshot
    {
        get
        {
            lock (gate)
            {
                return snapshot;
            }
        }
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
    }

    private void BuildCaches(IDataManager data)
    {
        try
        {
            foreach (var item in data.GetExcelSheet<Lumina.Excel.Sheets.Item>())
            {
                if (item.Icon != 0)
                {
                    itemIdByIcon.TryAdd(item.Icon, item.RowId);
                }

                var name = item.Name.ExtractText();
                if (!string.IsNullOrEmpty(name))
                {
                    itemNameById[item.RowId] = name;
                }
            }

            logger.Info($"[ShopInspector] op=init icons={itemIdByIcon.Count} names={itemNameById.Count}");
        }
        catch (Exception ex)
        {
            logger.Warn($"[ShopInspector] op=cache-init-failed err=\"{ex.Message}\"");
        }
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        LiveShopSnapshot next;
        try
        {
            next = Capture();
        }
        catch (Exception ex)
        {
            logger.Warn($"[ShopInspector] op=capture-failed err=\"{ex.Message}\"");
            return;
        }

        lock (gate)
        {
            snapshot = next;
        }
    }

    private unsafe LiveShopSnapshot Capture()
    {
        var menuEntries = ReadMenu(out var menuOpen);
        var shop = ReadShop();

        return new LiveShopSnapshot
        {
            IsSelectIconStringOpen = menuOpen,
            IsShopExchangeCurrencyOpen = shop.isOpen,
            SelectedTabId = shop.tabId,
            CurrencyItemId = shop.currencyItemId,
            CurrencyAmount = shop.currencyAmount,
            MenuEntries = menuEntries,
            ShopEntries = shop.entries,
        };
    }

    private unsafe IReadOnlyList<LiveShopMenuEntry> ReadMenu(out bool isOpen)
    {
        isOpen = false;
        var addon = (AddonSelectIconString*)gameGui.GetAddonByName("SelectIconString", 1).Address;
        if (addon == null || !addon->AtkUnitBase.IsReady)
        {
            return [];
        }

        isOpen = true;
        var entries = new List<LiveShopMenuEntry>();
        var count = addon->PopupMenu.PopupMenu.EntryCount;
        for (var i = 0; i < count; i++)
        {
            var p = addon->PopupMenu.PopupMenu.EntryNames[i].Value;
            if (p == null)
            {
                continue;
            }

            entries.Add(new LiveShopMenuEntry(i, Marshal.PtrToStringUTF8((nint)p)?.Trim() ?? string.Empty));
        }

        return entries;
    }

    private unsafe (bool isOpen, int tabId, uint currencyItemId, uint currencyAmount, IReadOnlyList<LiveShopEntry> entries) ReadShop()
    {
        var addon = (AtkUnitBase*)gameGui.GetAddonByName("ShopExchangeCurrency", 1).Address;
        if (addon == null || !addon->IsReady)
        {
            return (false, -1, 0, 0, []);
        }

        var numEntries = (int)addon->AtkValues[ShopExchangeCurrencyNumEntries].UInt;
        var tabId = (int)addon->AtkValues[ShopExchangeCurrencySelectedTab].UInt;
        var currencyAmount = addon->AtkValues[ShopExchangeCurrencyCurrencyAmount].UInt;
        var currencyItemId = itemIdByIcon.TryGetValue(addon->AtkValues[ShopExchangeCurrencyCurrencyIcon].UInt, out var cid) ? cid : 0;

        var entries = new List<LiveShopEntry>();
        for (var i = 0; i < numEntries; i++)
        {
            var itemId = addon->AtkValues[ShopExchangeCurrencyItemIdBase + i].UInt;
            if (itemId == 0)
            {
                continue;
            }

            entries.Add(new LiveShopEntry
            {
                ItemId = itemId,
                ItemName = itemNameById.TryGetValue(itemId, out var n) ? n : $"Item {itemId}",
                CurrencyItemId = currencyItemId,
                Cost = addon->AtkValues[ShopExchangeCurrencyCostBase + i].UInt,
                RowIndex = addon->AtkValues[ShopExchangeCurrencyRowIndexBase + i].UInt,
                TabId = tabId,
                MaxStackSize = addon->AtkValues[ShopExchangeCurrencyStackSizeBase + i].UInt,
            });
        }

        return (true, tabId, currencyItemId, currencyAmount, entries);
    }
}

/// <summary>
///     Matches the live shop window to a known catalog page/tab by item overlap and
///     cost/row exact matches — direct port of AOCCH's CurrentCurrencyShopPageMatcher.
/// </summary>
public sealed class ShopPageMatcher
{
    public sealed class Match
    {
        public required ShopPageDefinition Page { get; init; }
        public required ShopTabDefinition Tab { get; init; }
        public int ReportedTabId { get; init; }
    }

    public bool TryMatch(IReadOnlyList<ShopPageDefinition> pages, LiveShopSnapshot snapshot, out Match? match, out string reason)
    {
        match = null;
        reason = string.Empty;

        if (!snapshot.IsShopExchangeCurrencyOpen || snapshot.CurrencyItemId == 0 || snapshot.ShopEntries.Count == 0)
        {
            reason = "No supported currency shop is open.";
            return false;
        }

        var liveById = snapshot.ShopEntries.GroupBy(e => e.ItemId).ToDictionary(g => g.Key, g => g.First());
        var candidates = new List<(ShopPageDefinition Page, ShopTabDefinition Tab, int Overlap, int Exact, int TabBonus)>();

        foreach (var page in pages)
        {
            if (page.CurrencyItemId != snapshot.CurrencyItemId)
            {
                continue;
            }

            foreach (var tab in page.Tabs)
            {
                var overlap = 0;
                var exact = 0;
                foreach (var item in tab.Items)
                {
                    if (!liveById.TryGetValue(item.ItemId, out var live))
                    {
                        continue;
                    }

                    overlap++;
                    if (live.RowIndex == item.RowIndex && live.Cost == item.Cost)
                    {
                        exact++;
                    }
                }

                if (overlap > 0)
                {
                    candidates.Add((page, tab, overlap, exact, tab.TabId == snapshot.SelectedTabId ? 1 : 0));
                }
            }
        }

        if (candidates.Count == 0)
        {
            reason = $"No catalog page matches currency item {snapshot.CurrencyItemId}.";
            return false;
        }

        var ordered = candidates
            .OrderByDescending(c => c.Exact)
            .ThenByDescending(c => c.Overlap)
            .ThenByDescending(c => c.TabBonus)
            .ToList();

        var best = ordered[0];
        if (best.Exact == 0)
        {
            reason = "Page match confidence too low.";
            return false;
        }

        if (ordered.Count > 1)
        {
            var second = ordered[1];
            if (second.Overlap == best.Overlap && second.Exact == best.Exact && second.TabBonus == best.TabBonus)
            {
                reason = $"Ambiguous between {best.Page.MenuLabel}/{best.Tab.TabLabel} and {second.Page.MenuLabel}/{second.Tab.TabLabel}.";
                return false;
            }
        }

        match = new Match { Page = best.Page, Tab = best.Tab, ReportedTabId = snapshot.SelectedTabId };
        reason = $"Matched {best.Page.MenuLabel}/{best.Tab.TabLabel} overlap={best.Overlap} exact={best.Exact}";
        return true;
    }
}
