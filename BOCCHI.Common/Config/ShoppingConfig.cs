using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;
using BOCCHI.Common.Config.Fields;

namespace BOCCHI.Common.Config;

/// <summary>One configured purchase target for the Antiquarian currency shop.</summary>
[Serializable]
public sealed class CurrencyShopTarget
{
    /// <summary>ZoneId as string — "SouthHorn" / "NorthHorn".</summary>
    public string TerritoryKey { get; set; } = "SouthHorn";

    public uint ItemId { get; set; }

    public int MenuIndex { get; set; }

    public int TabId { get; set; } = -1;

    /// <summary>Buy until the inventory holds this many (0 = off).</summary>
    public int KeepAmount { get; set; }

    /// <summary>Buy exactly this many, then stop (0 = off).</summary>
    public int BuyAmount { get; set; }

    /// <summary>Spend all surplus currency on this item.</summary>
    public bool KeepBuying { get; set; }

    /// <summary>Lower runs first within a page/tab.</summary>
    public int Priority { get; set; }
}

/// <summary>Currency to hold back per territory (never spent).</summary>
[Serializable]
public sealed class CurrencyShopReserveSetting
{
    public string TerritoryKey { get; set; } = "SouthHorn";

    public uint CurrencyItemId { get; set; }

    public int ReserveAmount { get; set; }
}

/// <summary>Auto-shopping start threshold per territory and currency.</summary>
[Serializable]
public sealed class CurrencyShopThresholdSetting
{
    public string TerritoryKey { get; set; } = "SouthHorn";

    public uint CurrencyItemId { get; set; }

    public int StartThreshold { get; set; }
}

/// <summary>
///     Antiquarian currency shopping. Targets are evaluated Keep → Buy → Keep Buying,
///     ordered by Priority inside their page/tab.
/// </summary>
[Serializable]
[ConfigGroup("shopping", GroupOrder = 25)]
public class ShoppingConfig : IAutoConfig
{
    [Checkbox(Order = 0)]
    public bool EnableAutoShop { get; set; } = false;

    [ShopTargetList(Order = 1)]
    public List<CurrencyShopTarget> Targets { get; set; } = [];

    /// <summary>
    ///     Item IDs to buy from the Antiquarian currency shop (legacy simple allowlist — kept so
    ///     existing configs keep loading; superseded by <see cref="Targets"/> when non-empty).
    /// </summary>
    [ShopShoppingList(Order = 0)]
    public List<uint> ShoppingOrder { get; set; } = [];

    public Dictionary<uint, ShopListEntry> Shopping { get; set; } = new();

    /// <summary>Legacy checkbox picks — migrated into <see cref="Shopping"/> on load.</summary>
    public HashSet<uint> PreferredItemIds { get; set; } = [];

    /// <summary>Per-territory currency reserves and start thresholds (edited in config JSON / debug for now).</summary>
    public List<CurrencyShopReserveSetting> Reserves { get; set; } = [];

    public List<CurrencyShopThresholdSetting> Thresholds { get; set; } = [];

    public int GetReserve(string territoryKey, uint currencyItemId) =>
        Reserves.FirstOrDefault(s => MatchesTerritory(s.TerritoryKey, territoryKey) && s.CurrencyItemId == currencyItemId)?.ReserveAmount ?? 0;

    public int GetThreshold(string territoryKey, uint currencyItemId) =>
        Thresholds.FirstOrDefault(s => MatchesTerritory(s.TerritoryKey, territoryKey) && s.CurrencyItemId == currencyItemId)?.StartThreshold ?? 0;

    private static bool MatchesTerritory(string configuredKey, string territoryKey) =>
        string.Equals(configuredKey, territoryKey, StringComparison.OrdinalIgnoreCase);
}
