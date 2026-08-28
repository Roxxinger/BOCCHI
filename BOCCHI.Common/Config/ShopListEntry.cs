using BOCCHI.Common.Data.Shopping;

namespace BOCCHI.Common.Config;

/// <summary>
/// Per-item shopping goals: Keep = stock target, Buy = remaining purchases,
/// KeepBuying = currency sink (only one list entry may be true).
/// </summary>
[Serializable]
public class ShopListEntry
{
    /// <summary>Buy until inventory has at least this many (does not decrease between trips).</summary>
    public int KeepAmount { get; set; }

    /// <summary>Buy this many more times, then stop (decrements after each purchase).</summary>
    public int BuyAmount { get; set; }

    /// <summary>After Keep/Buy are satisfied, keep spending on this item.</summary>
    public bool KeepBuying { get; set; }

    /// <summary>
    /// When the item has multiple currency offers, which ones may be used.
    /// <see cref="ShopCurrencyPreference.None"/> = any offer.
    /// </summary>
    public ShopCurrencyPreference PreferredCurrencies { get; set; } = ShopCurrencyPreference.None;
}
