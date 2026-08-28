namespace BOCCHI.Common.Data.Shopping;

/// <summary>
/// Which Antiquarian currencies may be used for a list item that has multiple offers.
/// <see cref="None"/> means any available offer.
/// </summary>
[Flags]
public enum ShopCurrencyPreference
{
    None = 0,
    Silver = 1 << 0,
    Gold = 1 << 1,
    Amulet = 1 << 2,
}
