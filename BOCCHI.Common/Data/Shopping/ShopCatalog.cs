using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace BOCCHI.Common.Data.Shopping;

/// <summary>Expedition Antiquarian catalogs for South and North Horn.</summary>
public static partial class ShopCatalog
{
    public static uint SilverPieceItemId => OccultCurrencies.SilverPieceItemId;

    public static uint GoldPieceItemId => OccultCurrencies.GoldPieceItemId;

    public static uint SilverObolItemId => OccultCurrencies.SilverObolItemId;

    public static uint GoldObolItemId => OccultCurrencies.GoldObolItemId;

    public static uint SanguiniteItemId { get; private set; } = 47742;

    public static uint ArcaneAmuletItemId { get; private set; } = 51977;

    private static ShopCatalogEntry[] entries = [];

    private static Dictionary<uint, ShopCatalogEntry> byItemId = new();

    public static IReadOnlyList<ShopCatalogEntry> All => entries;

    public static bool TryGet(uint itemId, out ShopCatalogEntry entry) =>
        byItemId.TryGetValue(itemId, out entry);

    public static IEnumerable<ShopCatalogEntry> EntriesForItem(uint itemId, ZoneId zone) =>
        entries.Where(e => e.ItemId == itemId && e.Zone == zone);

    public static IEnumerable<ShopCatalogEntry> EntriesForItem(uint itemId) =>
        entries.Where(e => e.ItemId == itemId);

    public static ShopCurrencyPreference CurrencyKindOf(uint currencyItemId)
    {
        if (OccultCurrencies.IsSilverCurrency(currencyItemId))
        {
            return ShopCurrencyPreference.Silver;
        }

        if (OccultCurrencies.IsGoldCurrency(currencyItemId))
        {
            return ShopCurrencyPreference.Gold;
        }

        if (OccultCurrencies.IsAmuletCurrency(currencyItemId)
            || currencyItemId == ArcaneAmuletItemId)
        {
            return ShopCurrencyPreference.Amulet;
        }

        return ShopCurrencyPreference.None;
    }

    public static ShopCurrencyPreference AvailableCurrenciesFor(uint itemId, ZoneId zone)
    {
        ShopCurrencyPreference flags = ShopCurrencyPreference.None;
        foreach (ShopCatalogEntry e in EntriesForItem(itemId, zone))
        {
            flags |= CurrencyKindOf(e.CurrencyItemId);
        }

        return flags;
    }

    /// <summary>Union of currencies across every zone that sells this item.</summary>
    public static ShopCurrencyPreference AvailableCurrenciesFor(uint itemId)
    {
        ShopCurrencyPreference flags = ShopCurrencyPreference.None;
        foreach (ShopCatalogEntry e in EntriesForItem(itemId))
        {
            flags |= CurrencyKindOf(e.CurrencyItemId);
        }

        return flags;
    }

    /// <summary>
    /// Currencies for config UI: prefer the zone you're in when it sells the item,
    /// otherwise fall back to any-zone (so multi-currency prefs work outside OC).
    /// </summary>
    public static ShopCurrencyPreference AvailableCurrenciesForUi(uint itemId, ZoneId zone)
    {
        ShopCurrencyPreference inZone = AvailableCurrenciesFor(itemId, zone);
        if (inZone != ShopCurrencyPreference.None)
        {
            return inZone;
        }

        return AvailableCurrenciesFor(itemId);
    }

    public static IEnumerable<ShopCatalogEntry> PreferredOffers(
        uint itemId,
        ZoneId zone,
        ShopCurrencyPreference preferred,
        bool fallbackAnyZone = false)
    {
        List<ShopCatalogEntry> inZone = EntriesForItem(itemId, zone)
            .Where(e => MatchesCurrencyPreference(e, preferred))
            .ToList();
        if (inZone.Count > 0 || !fallbackAnyZone)
        {
            return inZone;
        }

        return EntriesForItem(itemId).Where(e => MatchesCurrencyPreference(e, preferred));
    }

    public static bool MatchesCurrencyPreference(
        ShopCatalogEntry entry,
        ShopCurrencyPreference preferred)
    {
        if (preferred == ShopCurrencyPreference.None)
        {
            return true;
        }

        ShopCurrencyPreference kind = CurrencyKindOf(entry.CurrencyItemId);
        return kind != ShopCurrencyPreference.None && preferred.HasFlag(kind);
    }

    public static void Initialize(IDataManager data)
    {
        ArcaneAmuletItemId = OccultCurrencies.NorthHornCipherItemId != 0
            ? OccultCurrencies.NorthHornCipherItemId
            : ArcaneAmuletItemId;

        List<ShopCatalogEntry> built = [];
        built.AddRange(BuildSouthHorn());
        built.AddRange(BuildNorthHorn());
        Dictionary<string, uint> byName = BuildEnglishItemNameMap(data);
        ResolveItemIdsByName(built, byName);
        AttachUpgradeChains(built, byName);

        entries = built.ToArray();
        byItemId = entries
            .GroupBy(e => e.ItemId)
            .Where(g => g.Key != 0)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private static void ResolveItemIdsByName(List<ShopCatalogEntry> list, Dictionary<string, uint> byName)
    {
        for (int i = 0; i < list.Count; i++)
        {
            ShopCatalogEntry e = list[i];
            if (!byName.TryGetValue(e.Name, out uint id) || id == 0)
            {
                continue;
            }

            if (e.ItemId != id)
            {
                list[i] = e with { ItemId = id };
            }
        }
    }

    private static Dictionary<string, uint> BuildEnglishItemNameMap(IDataManager data)
    {
        Dictionary<string, uint> byName = new(StringComparer.OrdinalIgnoreCase);
        foreach (Item row in data.GetExcelSheet<Item>(ClientLanguage.English))
        {
            string name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name) || byName.ContainsKey(name))
            {
                continue;
            }

            byName[name] = row.RowId;
        }

        return byName;
    }

    private static void AttachUpgradeChains(List<ShopCatalogEntry> list, Dictionary<string, uint> byName)
    {
        for (int i = 0; i < list.Count; i++)
        {
            ShopCatalogEntry e = list[i];
            if (e.Ownership != ShopOwnershipKind.Armor)
            {
                continue;
            }

            List<uint> upgrades = [];
            if (e.UpgradeItemIds is { Length: > 0 })
            {
                upgrades.AddRange(e.UpgradeItemIds);
            }

            foreach (string suffix in new[] { " +1", " +2", " +3" })
            {
                if (byName.TryGetValue(e.Name + suffix, out uint id) && id != e.ItemId)
                {
                    upgrades.Add(id);
                }
            }

            // Arcanaut (SH) → Phantom Vision (NH) counterparts by role/slot naming.
            if (e.Name.StartsWith("Arcanaut's ", StringComparison.Ordinal)
                && TryMapArcanautToPhantomVision(e.Name, byName, out uint phantomId))
            {
                upgrades.Add(phantomId);
                foreach (string suffix in new[] { " +1", " +2", " +3" })
                {
                    if (byName.TryGetValue(
                            PhantomVisionNameFromArcanaut(e.Name) + suffix,
                            out uint upId))
                    {
                        upgrades.Add(upId);
                    }
                }
            }

            if (upgrades.Count == 0)
            {
                continue;
            }

            list[i] = e with { UpgradeItemIds = upgrades.Distinct().ToArray() };
        }
    }

    private static bool TryMapArcanautToPhantomVision(
        string arcanautName,
        Dictionary<string, uint> byName,
        out uint phantomId)
    {
        string phantom = PhantomVisionNameFromArcanaut(arcanautName);
        return byName.TryGetValue(phantom, out phantomId);
    }

    private static string PhantomVisionNameFromArcanaut(string arcanautName)
    {
        // Arcanaut's Pelt/Vest/... of Role → Phantom Vision Mask/Corselet/... of Role
        ReadOnlySpan<string> map =
        [
            "Arcanaut's Pelt of ", "Phantom Vision Mask of ",
            "Arcanaut's Vest of ", "Phantom Vision Corselet of ",
            "Arcanaut's Armlets of ", "Phantom Vision Vambraces of ",
            "Arcanaut's Loincloth of ", "Phantom Vision Bottoms of ",
            "Arcanaut's Feet of ", "Phantom Vision Sollerets of ",
            "Arcanaut's Bicorne of ", "Phantom Vision Turban of ",
            "Arcanaut's Justaucorps of ", "Phantom Vision Robe of ",
            "Arcanaut's Gloves of ", "Phantom Vision Wristwraps of ",
            "Arcanaut's Boots of ", "Phantom Vision Boots of ",
            "Arcanaut's Slops of ", "Phantom Vision Sarouel of ",
            "Arcanaut's Sugarloaf Hat of ", "Phantom Vision Nightcap of ",
            "Arcanaut's Robe of ", "Phantom Vision Acton of ",
            "Arcanaut's Wristgloves of ", "Phantom Vision Wristwraps of ",
            "Arcanaut's Skirt of ", "Phantom Vision Sarouel of ",
        ];

        for (int i = 0; i < map.Length; i += 2)
        {
            if (arcanautName.StartsWith(map[i], StringComparison.Ordinal))
            {
                return map[i + 1] + arcanautName[map[i].Length..];
            }
        }

        return arcanautName;
    }

    private static ShopCatalogEntry E(
        uint itemId,
        string name,
        uint cost,
        uint currency,
        int menu,
        ZoneId zone,
        ShopOwnershipKind ownership,
        string section,
        SupportJobId? job = null,
        uint[]? upgrades = null) =>
        new(itemId, name, cost, currency, menu, zone, ownership, job, upgrades, section);

    private static IEnumerable<ShopCatalogEntry> ArmorSet(
        ZoneId zone,
        uint currency,
        int menu,
        string section,
        uint headId,
        string head,
        string body,
        string hands,
        string legs,
        string feet)
    {
        yield return E(headId, head, 4000, currency, menu, zone, ShopOwnershipKind.Armor, section);
        yield return E(headId + 1, body, 4000, currency, menu, zone, ShopOwnershipKind.Armor, section);
        yield return E(headId + 2, hands, 4000, currency, menu, zone, ShopOwnershipKind.Armor, section);
        yield return E(headId + 3, legs, 4000, currency, menu, zone, ShopOwnershipKind.Armor, section);
        yield return E(headId + 4, feet, 4000, currency, menu, zone, ShopOwnershipKind.Armor, section);
    }
}
