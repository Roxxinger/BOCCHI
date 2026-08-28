using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;

namespace BOCCHI.Common.Data.Shopping;

public static partial class ShopCatalog
{
    // SelectIconString menu indices for Expedition Antiquarian (South Horn).
    private const int ShMenuSilver = 0;
    private const int ShMenuGold = 1;
    private const int ShMenuSanguinite = 2;

    private static IEnumerable<ShopCatalogEntry> BuildSouthHorn()
    {
        ZoneId z = ZoneId.SouthHorn;
        uint silver = SilverPieceItemId;
        uint gold = GoldPieceItemId;

        foreach (ShopCatalogEntry e in ArmorSet(
                     z, silver, ShMenuSilver, "Silver · Fending",
                     47758,
                     "Arcanaut's Pelt of Fending",
                     "Arcanaut's Vest of Fending",
                     "Arcanaut's Armlets of Fending",
                     "Arcanaut's Loincloth of Fending",
                     "Arcanaut's Feet of Fending"))
        {
            yield return e;
        }

        foreach (ShopCatalogEntry e in ArmorSet(
                     z, silver, ShMenuSilver, "Silver · Maiming",
                     47773,
                     "Arcanaut's Pelt of Maiming",
                     "Arcanaut's Vest of Maiming",
                     "Arcanaut's Armlets of Maiming",
                     "Arcanaut's Loincloth of Maiming",
                     "Arcanaut's Feet of Maiming"))
        {
            yield return e;
        }

        foreach (ShopCatalogEntry e in ArmorSet(
                     z, silver, ShMenuSilver, "Silver · Striking",
                     47788,
                     "Arcanaut's Bicorne of Striking",
                     "Arcanaut's Justaucorps of Striking",
                     "Arcanaut's Gloves of Striking",
                     "Arcanaut's Slops of Striking",
                     "Arcanaut's Boots of Striking"))
        {
            yield return e;
        }

        foreach (ShopCatalogEntry e in ArmorSet(
                     z, silver, ShMenuSilver, "Silver · Aiming",
                     47803,
                     "Arcanaut's Bicorne of Aiming",
                     "Arcanaut's Justaucorps of Aiming",
                     "Arcanaut's Gloves of Aiming",
                     "Arcanaut's Slops of Aiming",
                     "Arcanaut's Boots of Aiming"))
        {
            yield return e;
        }

        foreach (ShopCatalogEntry e in ArmorSet(
                     z, silver, ShMenuSilver, "Silver · Scouting",
                     47818,
                     "Arcanaut's Bicorne of Scouting",
                     "Arcanaut's Justaucorps of Scouting",
                     "Arcanaut's Gloves of Scouting",
                     "Arcanaut's Slops of Scouting",
                     "Arcanaut's Boots of Scouting"))
        {
            yield return e;
        }

        foreach (ShopCatalogEntry e in ArmorSet(
                     z, silver, ShMenuSilver, "Silver · Healing",
                     47833,
                     "Arcanaut's Sugarloaf Hat of Healing",
                     "Arcanaut's Robe of Healing",
                     "Arcanaut's Wristgloves of Healing",
                     "Arcanaut's Skirt of Healing",
                     "Arcanaut's Boots of Healing"))
        {
            yield return e;
        }

        foreach (ShopCatalogEntry e in ArmorSet(
                     z, silver, ShMenuSilver, "Silver · Casting",
                     47848,
                     "Arcanaut's Sugarloaf Hat of Casting",
                     "Arcanaut's Robe of Casting",
                     "Arcanaut's Wristgloves of Casting",
                     "Arcanaut's Skirt of Casting",
                     "Arcanaut's Boots of Casting"))
        {
            yield return e;
        }

        // Silver battlecraft / other (common spend + unlockables).
        yield return E(47734, "Time Mage's Soul Shard", 1000, silver, ShMenuSilver, z, ShopOwnershipKind.PhantomJob, "Silver · Battlecraft", SupportJobId.PhantomTime);
        yield return E(47735, "Cannoneer's Soul Shard", 1000, silver, ShMenuSilver, z, ShopOwnershipKind.PhantomJob, "Silver · Battlecraft", SupportJobId.PhantomCannoneer);
        yield return E(47736, "Chemist's Soul Shard", 1000, silver, ShMenuSilver, z, ShopOwnershipKind.PhantomJob, "Silver · Battlecraft", SupportJobId.PhantomChemist);
        yield return E(47737, "Mystic Knight's Soul Shard", 1000, silver, ShMenuSilver, z, ShopOwnershipKind.PhantomJob, "Silver · Battlecraft", SupportJobId.PhantomMysticKnight);
        yield return E(47738, "Dancer's Soul Shard", 1000, silver, ShMenuSilver, z, ShopOwnershipKind.PhantomJob, "Silver · Battlecraft", SupportJobId.PhantomDancer);
        yield return E(48230, "South Horn Riding Map", 3000, silver, ShMenuSilver, z, ShopOwnershipKind.KeyItem, "Silver · Other");
        yield return E(47739, "Sanguine Cipher", 200, silver, ShMenuSilver, z, ShopOwnershipKind.Repeatable, "Silver · Ciphers");
        yield return E(46108, "Aetherspun Silver", 1200, silver, ShMenuSilver, z, ShopOwnershipKind.Repeatable, "Silver · Battlecraft");
        yield return E(45970, "Occult Coffer", 40, silver, ShMenuSilver, z, ShopOwnershipKind.Repeatable, "Silver · Battlecraft");
        yield return E(45969, "Occult Potion", 40, silver, ShMenuSilver, z, ShopOwnershipKind.Repeatable, "Silver · Battlecraft");

        foreach (ShopCatalogEntry m in MateriaPack(z, silver, ShMenuSilver, "Silver · Battlecraft", xi: 100, xii: 200))
        {
            yield return m;
        }

        // Glamour / other silver (IDs best-effort; ownership still works via ItemId when present).
        yield return E(47900, "Lix Temple Chain", 1000, silver, ShMenuSilver, z, ShopOwnershipKind.Armor, "Silver · Other");
        yield return E(47901, "Lix Chiton", 1000, silver, ShMenuSilver, z, ShopOwnershipKind.Armor, "Silver · Other");
        yield return E(47902, "Lix Fingerless Gloves", 1000, silver, ShMenuSilver, z, ShopOwnershipKind.Armor, "Silver · Other");
        yield return E(47903, "Lix Hose", 1000, silver, ShMenuSilver, z, ShopOwnershipKind.Armor, "Silver · Other");
        yield return E(47904, "Lix Longboots", 1000, silver, ShMenuSilver, z, ShopOwnershipKind.Armor, "Silver · Other");
        yield return E(47890, "Ancient Airship Identification Key", 5000, silver, ShMenuSilver, z, ShopOwnershipKind.Mount, "Silver · Other");
        yield return E(47891, "Skallic Uolosapa", 600, silver, ShMenuSilver, z, ShopOwnershipKind.Minion, "Silver · Other");
        yield return E(47892, "La Noscean Shorthair", 1000, silver, ShMenuSilver, z, ShopOwnershipKind.Minion, "Silver · Other");

        // Gold shop.
        yield return E(47745, "Samurai's Soul Shard", 1600, gold, ShMenuGold, z, ShopOwnershipKind.PhantomJob, "Gold · Battlecraft", SupportJobId.PhantomSamurai);
        yield return E(47746, "Geomancer's Soul Shard", 1600, gold, ShMenuGold, z, ShopOwnershipKind.PhantomJob, "Gold · Battlecraft", SupportJobId.PhantomGeomancer);
        yield return E(47747, "Thief's Soul Shard", 1600, gold, ShMenuGold, z, ShopOwnershipKind.PhantomJob, "Gold · Battlecraft", SupportJobId.PhantomThief);
        yield return E(47748, "Gladiator's Soul Shard", 1600, gold, ShMenuGold, z, ShopOwnershipKind.PhantomJob, "Gold · Battlecraft", SupportJobId.PhantomGladiator);
        yield return E(46109, "Aetherial Fixative", 1600, gold, ShMenuGold, z, ShopOwnershipKind.Repeatable, "Gold · Battlecraft");
        yield return E(45970, "Occult Coffer", 50, gold, ShMenuGold, z, ShopOwnershipKind.Repeatable, "Gold · Battlecraft");
        yield return E(45969, "Occult Potion", 50, gold, ShMenuGold, z, ShopOwnershipKind.Repeatable, "Gold · Battlecraft");
        yield return E(47739, "Sanguine Cipher", 320, gold, ShMenuGold, z, ShopOwnershipKind.Repeatable, "Gold · Ciphers");

        foreach (ShopCatalogEntry m in MateriaPack(z, gold, ShMenuGold, "Gold · Battlecraft", xi: 160, xii: 320))
        {
            yield return m;
        }

        // Sanguinite exchange (unlock-gated; live shop resolve skips if locked).
        yield return E(47895, "Petalodus Whistle", 99, SanguiniteItemId, ShMenuSanguinite, z, ShopOwnershipKind.Mount, "Sanguinite");
    }

    private static IEnumerable<ShopCatalogEntry> MateriaPack(
        ZoneId zone,
        uint currency,
        int menu,
        string section,
        uint xi,
        uint xii)
    {
        // Standard materia XI/XII item ids (Dawntrail).
        (uint Id, string Name, uint Cost)[] rows =
        [
            (41757, "Piety Materia XI", xi),
            (41758, "Heavens' Eye Materia XI", xi),
            (41759, "Savage Aim Materia XI", xi),
            (41760, "Savage Might Materia XI", xi),
            (41761, "Battledance Materia XI", xi),
            (41762, "Quickarm Materia XI", xi),
            (41763, "Quicktongue Materia XI", xi),
            (41764, "Piety Materia XII", xii),
            (41765, "Heavens' Eye Materia XII", xii),
            (41766, "Savage Aim Materia XII", xii),
            (41767, "Savage Might Materia XII", xii),
            (41768, "Battledance Materia XII", xii),
            (41769, "Quickarm Materia XII", xii),
            (41770, "Quicktongue Materia XII", xii),
        ];

        foreach ((uint id, string name, uint cost) in rows)
        {
            yield return E(id, name, cost, currency, menu, zone, ShopOwnershipKind.Repeatable, section);
        }
    }
}
