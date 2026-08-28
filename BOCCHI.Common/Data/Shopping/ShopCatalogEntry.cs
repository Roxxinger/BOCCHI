using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;

namespace BOCCHI.Common.Data.Shopping;

/// <summary>One Antiquarian exchange row (South or North Horn).</summary>
public readonly record struct ShopCatalogEntry(
    uint ItemId,
    string Name,
    uint Cost,
    uint CurrencyItemId,
    int MenuIndex,
    ZoneId Zone,
    ShopOwnershipKind Ownership,
    SupportJobId? PhantomJob = null,
    uint[]? UpgradeItemIds = null,
    string Section = "");
