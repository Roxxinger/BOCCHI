using BOCCHI.Common.Data.OccultCrescent;

namespace BOCCHI.Common.Data.Shopping;

/// <summary>One buyable row inside a currency-shop tab.</summary>
public sealed class ShopItemDefinition
{
    public required uint ItemId { get; init; }
    public required string Name { get; init; }
    public required uint Cost { get; init; }
    public required uint RowIndex { get; init; }
}

/// <summary>One tab of a currency-shop page.</summary>
public sealed class ShopTabDefinition
{
    public required int TabId { get; init; }
    public required string TabLabel { get; init; }
    public List<ShopItemDefinition> Items { get; init; } = [];
}

/// <summary>
///     One menu entry of the Antiquarian dialog ("Enlightenment Silver Piece Exchange (IL 745)" ...).
/// </summary>
public sealed class ShopPageDefinition
{
    public required int MenuIndex { get; init; }
    public required string MenuLabel { get; init; }
    public required uint CurrencyItemId { get; init; }
    public required string CurrencyName { get; init; }
    public List<ShopTabDefinition> Tabs { get; init; } = [];
}

/// <summary>Known Antiquarian currency-shop rows (mirrors AOCCH's ShopCurrencyCatalog).</summary>
public static class ShopCatalog
{
    public static uint SilverPieceItemId => OccultCurrencies.SilverPieceItemId;

    public static uint GoldPieceItemId => OccultCurrencies.GoldPieceItemId;

    /// <summary>The other horn pays in obols rather than pieces.</summary>
    public static uint SilverObolItemId => OccultCurrencies.SilverObolItemId;

    public static uint GoldObolItemId => OccultCurrencies.GoldObolItemId;

    private const string SilverName = "Enlightenment Silver Piece";
    private const string GoldName = "Enlightenment Gold Piece";
    private const string SilverObolName = "Enlightenment Silver Obol";
    private const string GoldObolName = "Enlightenment Gold Obol";

    public static IReadOnlyList<ShopPageDefinition> Pages { get; } =
    [
        new ShopPageDefinition
        {
            MenuIndex = 0,
            MenuLabel = "Enlightenment Silver Piece Exchange (IL 745)",
            CurrencyItemId = 45043,
            CurrencyName = SilverName,
            Tabs =
            [
                new ShopTabDefinition { TabId = 0, TabLabel = "Weapons" },
                new ShopTabDefinition
                {
                    TabId = 1,
                    TabLabel = "Armor",
                    Items =
                    [
                        new() { ItemId = 47758, Name = "Arcanaut's Pelt of Fending", Cost = 4000, RowIndex = 0 },
                        new() { ItemId = 47773, Name = "Arcanaut's Pelt of Maiming", Cost = 4000, RowIndex = 1 },
                        new() { ItemId = 47788, Name = "Arcanaut's Bicorne of Striking", Cost = 4000, RowIndex = 2 },
                        new() { ItemId = 47818, Name = "Arcanaut's Bicorne of Scouting", Cost = 4000, RowIndex = 3 },
                        new() { ItemId = 47803, Name = "Arcanaut's Bicorne of Aiming", Cost = 4000, RowIndex = 4 },
                        new() { ItemId = 47848, Name = "Arcanaut's Sugarloaf Hat of Casting", Cost = 4000, RowIndex = 5 },
                        new() { ItemId = 47833, Name = "Arcanaut's Sugarloaf Hat of Healing", Cost = 4000, RowIndex = 6 },
                        new() { ItemId = 47759, Name = "Arcanaut's Vest of Fending", Cost = 4000, RowIndex = 7 },
                        new() { ItemId = 47774, Name = "Arcanaut's Vest of Maiming", Cost = 4000, RowIndex = 8 },
                        new() { ItemId = 47789, Name = "Arcanaut's Justaucorps of Striking", Cost = 4000, RowIndex = 9 },
                        new() { ItemId = 47819, Name = "Arcanaut's Justaucorps of Scouting", Cost = 4000, RowIndex = 10 },
                        new() { ItemId = 47804, Name = "Arcanaut's Justaucorps of Aiming", Cost = 4000, RowIndex = 11 },
                        new() { ItemId = 47849, Name = "Arcanaut's Robe of Casting", Cost = 4000, RowIndex = 12 },
                        new() { ItemId = 47834, Name = "Arcanaut's Robe of Healing", Cost = 4000, RowIndex = 13 },
                        new() { ItemId = 47760, Name = "Arcanaut's Armlets of Fending", Cost = 4000, RowIndex = 14 },
                        new() { ItemId = 47775, Name = "Arcanaut's Armlets of Maiming", Cost = 4000, RowIndex = 15 },
                        new() { ItemId = 47790, Name = "Arcanaut's Gloves of Striking", Cost = 4000, RowIndex = 16 },
                        new() { ItemId = 47820, Name = "Arcanaut's Gloves of Scouting", Cost = 4000, RowIndex = 17 },
                        new() { ItemId = 47805, Name = "Arcanaut's Gloves of Aiming", Cost = 4000, RowIndex = 18 },
                        new() { ItemId = 47850, Name = "Arcanaut's Wristgloves of Casting", Cost = 4000, RowIndex = 19 },
                        new() { ItemId = 47835, Name = "Arcanaut's Wristgloves of Healing", Cost = 4000, RowIndex = 20 },
                        new() { ItemId = 47761, Name = "Arcanaut's Loincloth of Fending", Cost = 4000, RowIndex = 21 },
                        new() { ItemId = 47776, Name = "Arcanaut's Loincloth of Maiming", Cost = 4000, RowIndex = 22 },
                        new() { ItemId = 47791, Name = "Arcanaut's Slops of Striking", Cost = 4000, RowIndex = 23 },
                        new() { ItemId = 47821, Name = "Arcanaut's Slops of Scouting", Cost = 4000, RowIndex = 24 },
                        new() { ItemId = 47806, Name = "Arcanaut's Slops of Aiming", Cost = 4000, RowIndex = 25 },
                        new() { ItemId = 47851, Name = "Arcanaut's Skirt of Casting", Cost = 4000, RowIndex = 26 },
                        new() { ItemId = 47836, Name = "Arcanaut's Skirt of Healing", Cost = 4000, RowIndex = 27 },
                        new() { ItemId = 47762, Name = "Arcanaut's Feet of Fending", Cost = 4000, RowIndex = 28 },
                        new() { ItemId = 47777, Name = "Arcanaut's Feet of Maiming", Cost = 4000, RowIndex = 29 },
                        new() { ItemId = 47792, Name = "Arcanaut's Boots of Striking", Cost = 4000, RowIndex = 30 },
                        new() { ItemId = 47822, Name = "Arcanaut's Boots of Scouting", Cost = 4000, RowIndex = 31 },
                        new() { ItemId = 47807, Name = "Arcanaut's Boots of Aiming", Cost = 4000, RowIndex = 32 },
                        new() { ItemId = 47852, Name = "Arcanaut's Boots of Casting", Cost = 4000, RowIndex = 33 },
                        new() { ItemId = 47837, Name = "Arcanaut's Boots of Healing", Cost = 4000, RowIndex = 34 },
                    ],
                },
                new ShopTabDefinition { TabId = 2, TabLabel = "Accessories" },
                new ShopTabDefinition { TabId = 3, TabLabel = "Other" },
            ],
        },
        new ShopPageDefinition
        {
            MenuIndex = 1,
            MenuLabel = "Enlightenment Silver Piece Exchange (Battlecraft Items)",
            CurrencyItemId = 45043,
            CurrencyName = SilverName,
            Tabs =
            [
                new ShopTabDefinition { TabId = 0, TabLabel = "Weapons" },
                new ShopTabDefinition { TabId = 1, TabLabel = "Armor" },
                new ShopTabDefinition { TabId = 2, TabLabel = "Accessories" },
                new ShopTabDefinition
                {
                    TabId = 3,
                    TabLabel = "Other",
                    Items =
                    [
                        new() { ItemId = 47755, Name = "Time Mage's Soul Shard", Cost = 1000, RowIndex = 0 },
                        new() { ItemId = 47756, Name = "Cannoneer's Soul Shard", Cost = 1000, RowIndex = 1 },
                        new() { ItemId = 48748, Name = "Chemist's Soul Shard", Cost = 1000, RowIndex = 2 },
                        new() { ItemId = 49823, Name = "Mystic Knight's Soul Shard", Cost = 1000, RowIndex = 3 },
                        new() { ItemId = 49825, Name = "Dancer's Soul Shard", Cost = 1000, RowIndex = 4 },
                        new() { ItemId = 47741, Name = "Occult Potion", Cost = 40, RowIndex = 5 },
                        new() { ItemId = 47740, Name = "Occult Coffer", Cost = 40, RowIndex = 6 },
                        new() { ItemId = 47864, Name = "Aetherspun Silver", Cost = 1200, RowIndex = 7 },
                        new() { ItemId = 41759, Name = "Savage Aim Materia XI", Cost = 100, RowIndex = 8 },
                        new() { ItemId = 41772, Name = "Savage Aim Materia XII", Cost = 200, RowIndex = 9 },
                        new() { ItemId = 41760, Name = "Savage Might Materia XI", Cost = 100, RowIndex = 10 },
                        new() { ItemId = 41773, Name = "Savage Might Materia XII", Cost = 200, RowIndex = 11 },
                        new() { ItemId = 41758, Name = "Heavens' Eye Materia XI", Cost = 100, RowIndex = 12 },
                        new() { ItemId = 41771, Name = "Heavens' Eye Materia XII", Cost = 200, RowIndex = 13 },
                        new() { ItemId = 41768, Name = "Quickarm Materia XI", Cost = 100, RowIndex = 14 },
                        new() { ItemId = 41781, Name = "Quickarm Materia XII", Cost = 200, RowIndex = 15 },
                        new() { ItemId = 41769, Name = "Quicktongue Materia XI", Cost = 100, RowIndex = 16 },
                        new() { ItemId = 41782, Name = "Quicktongue Materia XII", Cost = 200, RowIndex = 17 },
                        new() { ItemId = 41761, Name = "Battledance Materia XI", Cost = 100, RowIndex = 18 },
                        new() { ItemId = 41774, Name = "Battledance Materia XII", Cost = 200, RowIndex = 19 },
                        new() { ItemId = 41757, Name = "Piety Materia XI", Cost = 100, RowIndex = 20 },
                        new() { ItemId = 41770, Name = "Piety Materia XII", Cost = 200, RowIndex = 21 },
                    ],
                },
            ],
        },
        new ShopPageDefinition
        {
            MenuIndex = 2,
            MenuLabel = "Enlightenment Silver Piece Exchange (Other)",
            CurrencyItemId = 45043,
            CurrencyName = SilverName,
            Tabs =
            [
                new ShopTabDefinition { TabId = 0, TabLabel = "Weapons" },
                new ShopTabDefinition
                {
                    TabId = 1,
                    TabLabel = "Armor",
                    Items =
                    [
                        new() { ItemId = 47891, Name = "Lix Temple Chain", Cost = 1000, RowIndex = 10 },
                        new() { ItemId = 47892, Name = "Lix Chiton", Cost = 1000, RowIndex = 11 },
                        new() { ItemId = 47893, Name = "Lix Fingerless Gloves", Cost = 1000, RowIndex = 12 },
                        new() { ItemId = 47894, Name = "Lix Hose", Cost = 1000, RowIndex = 13 },
                        new() { ItemId = 47895, Name = "Lix Longboots", Cost = 1000, RowIndex = 14 },
                    ],
                },
                new ShopTabDefinition { TabId = 2, TabLabel = "Accessories" },
                new ShopTabDefinition
                {
                    TabId = 3,
                    TabLabel = "Other",
                    Items =
                    [
                        new() { ItemId = 48230, Name = "South Horn Riding Map", Cost = 3000, RowIndex = 0 },
                        new() { ItemId = 47975, Name = "Ancient Airship Identification Key", Cost = 5000, RowIndex = 1 },
                        new() { ItemId = 47972, Name = "Skallic Uolosapa", Cost = 600, RowIndex = 2 },
                        new() { ItemId = 49822, Name = "La Noscean Shorthair", Cost = 1000, RowIndex = 3 },
                        new() { ItemId = 48090, Name = "Occult Crescent Framer's Kit", Cost = 600, RowIndex = 4 },
                        new() { ItemId = 48206, Name = "Town Theme (Dawntrail) Orchestrion Roll", Cost = 1000, RowIndex = 5 },
                        new() { ItemId = 48207, Name = "A New World (Dawntrail) Orchestrion Roll", Cost = 1000, RowIndex = 6 },
                        new() { ItemId = 48144, Name = "Occult Crescent Map", Cost = 400, RowIndex = 7 },
                        new() { ItemId = 48139, Name = "Crescent Trophy", Cost = 400, RowIndex = 8 },
                        new() { ItemId = 50425, Name = "Mhachi Lamppost", Cost = 400, RowIndex = 9 },
                        new() { ItemId = 48157, Name = "Magicked Prism (Ribbons)", Cost = 1, RowIndex = 15 },
                    ],
                },
            ],
        },
        new ShopPageDefinition
        {
            MenuIndex = 3,
            MenuLabel = "Enlightenment Gold Piece Exchange (Battlecraft Items)",
            CurrencyItemId = 45044,
            CurrencyName = GoldName,
            Tabs =
            [
                new ShopTabDefinition { TabId = 0, TabLabel = "Weapons" },
                new ShopTabDefinition { TabId = 1, TabLabel = "Armor" },
                new ShopTabDefinition { TabId = 2, TabLabel = "Accessories" },
                new ShopTabDefinition
                {
                    TabId = 3,
                    TabLabel = "Other",
                    Items =
                    [
                        new() { ItemId = 47753, Name = "Samurai's Soul Shard", Cost = 1600, RowIndex = 0 },
                        new() { ItemId = 47754, Name = "Geomancer's Soul Shard", Cost = 1600, RowIndex = 1 },
                        new() { ItemId = 48749, Name = "Thief's Soul Shard", Cost = 1600, RowIndex = 2 },
                        new() { ItemId = 49824, Name = "Gladiator's Soul Shard", Cost = 1600, RowIndex = 3 },
                        new() { ItemId = 47741, Name = "Occult Potion", Cost = 50, RowIndex = 4 },
                        new() { ItemId = 47740, Name = "Occult Coffer", Cost = 50, RowIndex = 5 },
                        new() { ItemId = 47865, Name = "Aetherial Fixative", Cost = 1600, RowIndex = 6 },
                        new() { ItemId = 41759, Name = "Savage Aim Materia XI", Cost = 160, RowIndex = 7 },
                        new() { ItemId = 41772, Name = "Savage Aim Materia XII", Cost = 320, RowIndex = 8 },
                        new() { ItemId = 41760, Name = "Savage Might Materia XI", Cost = 160, RowIndex = 9 },
                        new() { ItemId = 41773, Name = "Savage Might Materia XII", Cost = 320, RowIndex = 10 },
                        new() { ItemId = 41758, Name = "Heavens' Eye Materia XI", Cost = 160, RowIndex = 11 },
                        new() { ItemId = 41771, Name = "Heavens' Eye Materia XII", Cost = 320, RowIndex = 12 },
                        new() { ItemId = 41768, Name = "Quickarm Materia XI", Cost = 160, RowIndex = 13 },
                        new() { ItemId = 41781, Name = "Quickarm Materia XII", Cost = 320, RowIndex = 14 },
                        new() { ItemId = 41769, Name = "Quicktongue Materia XI", Cost = 160, RowIndex = 15 },
                        new() { ItemId = 41782, Name = "Quicktongue Materia XII", Cost = 320, RowIndex = 16 },
                        new() { ItemId = 41761, Name = "Battledance Materia XI", Cost = 160, RowIndex = 17 },
                        new() { ItemId = 41774, Name = "Battledance Materia XII", Cost = 320, RowIndex = 18 },
                        new() { ItemId = 41757, Name = "Piety Materia XI", Cost = 160, RowIndex = 19 },
                        new() { ItemId = 41770, Name = "Piety Materia XII", Cost = 320, RowIndex = 20 },
                    ],
                },
            ],
        },
        new ShopPageDefinition
        {
            MenuIndex = 4,
            MenuLabel = "Enlightenment Gold Piece Exchange (Other)",
            CurrencyItemId = 45044,
            CurrencyName = GoldName,
            Tabs =
            [
                new ShopTabDefinition { TabId = 0, TabLabel = "Weapons" },
                new ShopTabDefinition
                {
                    TabId = 1,
                    TabLabel = "Armor",
                    Items =
                    [
                        new() { ItemId = 47896, Name = "Tycoon Hairpin", Cost = 1600, RowIndex = 3 },
                        new() { ItemId = 47897, Name = "Tycoon Leotard", Cost = 1600, RowIndex = 4 },
                        new() { ItemId = 47898, Name = "Tycoon Dress Gloves", Cost = 1600, RowIndex = 5 },
                        new() { ItemId = 47899, Name = "Tycoon Tights", Cost = 1600, RowIndex = 6 },
                        new() { ItemId = 47900, Name = "Tycoon Bootlets", Cost = 1600, RowIndex = 7 },
                        new() { ItemId = 47901, Name = "Scherwiz Hairpin", Cost = 1600, RowIndex = 11 },
                        new() { ItemId = 47902, Name = "Scherwiz Coat", Cost = 1600, RowIndex = 12 },
                        new() { ItemId = 47903, Name = "Scherwiz Vambraces", Cost = 1600, RowIndex = 13 },
                        new() { ItemId = 47904, Name = "Scherwiz Skirt", Cost = 1600, RowIndex = 14 },
                        new() { ItemId = 47905, Name = "Scherwiz Boots", Cost = 1600, RowIndex = 15 },
                    ],
                },
                new ShopTabDefinition { TabId = 2, TabLabel = "Accessories" },
                new ShopTabDefinition
                {
                    TabId = 3,
                    TabLabel = "Other",
                    Items =
                    [
                        new() { ItemId = 49821, Name = "Gallant Shepherd", Cost = 1600, RowIndex = 0 },
                        new() { ItemId = 48204, Name = "Garden Relics Orchestrion Roll", Cost = 1600, RowIndex = 1 },
                        new() { ItemId = 48205, Name = "Garden Ruins Orchestrion Roll", Cost = 1600, RowIndex = 2 },
                        new() { ItemId = 48143, Name = "Knowledge Crystal Replica", Cost = 960, RowIndex = 8 },
                        new() { ItemId = 50423, Name = "Occult Compass", Cost = 960, RowIndex = 9 },
                        new() { ItemId = 50424, Name = "Occult Pyramicula", Cost = 960, RowIndex = 10 },
                    ],
                },
            ],
        },

        new ShopPageDefinition
        {
            MenuIndex = 0,
            MenuLabel = "Enlightenment Silver Obol Exchange (IL 780)",
            CurrencyItemId = 51975,
            CurrencyName = SilverObolName,
            Tabs =
            [
                new ShopTabDefinition
                {
                    TabId = 1,
                    TabLabel = "Armor",
                    Items =
                    [
                        new() { ItemId = 51811, Name = "Phantom Vision Mask of Fending", Cost = 4000, RowIndex = 0 },
                        new() { ItemId = 51831, Name = "Phantom Vision Mask of Maiming", Cost = 4000, RowIndex = 1 },
                        new() { ItemId = 51851, Name = "Phantom Vision Turban of Striking", Cost = 4000, RowIndex = 2 },
                        new() { ItemId = 51891, Name = "Phantom Vision Turban of Scouting", Cost = 4000, RowIndex = 3 },
                        new() { ItemId = 51871, Name = "Phantom Vision Turban of Aiming", Cost = 4000, RowIndex = 4 },
                        new() { ItemId = 51931, Name = "Phantom Vision Nightcap of Casting", Cost = 4000, RowIndex = 5 },
                        new() { ItemId = 51911, Name = "Phantom Vision Nightcap of Healing", Cost = 4000, RowIndex = 6 },
                        new() { ItemId = 51812, Name = "Phantom Vision Corselet of Fending", Cost = 4000, RowIndex = 7 },
                        new() { ItemId = 51832, Name = "Phantom Vision Corselet of Maiming", Cost = 4000, RowIndex = 8 },
                        new() { ItemId = 51852, Name = "Phantom Vision Robe of Striking", Cost = 4000, RowIndex = 9 },
                        new() { ItemId = 51892, Name = "Phantom Vision Robe of Scouting", Cost = 4000, RowIndex = 10 },
                        new() { ItemId = 51872, Name = "Phantom Vision Robe of Aiming", Cost = 4000, RowIndex = 11 },
                        new() { ItemId = 51932, Name = "Phantom Vision Acton of Casting", Cost = 4000, RowIndex = 12 },
                        new() { ItemId = 51912, Name = "Phantom Vision Acton of Healing", Cost = 4000, RowIndex = 13 },
                        new() { ItemId = 51813, Name = "Phantom Vision Vambraces of Fending", Cost = 4000, RowIndex = 14 },
                        new() { ItemId = 51833, Name = "Phantom Vision Vambraces of Maiming", Cost = 4000, RowIndex = 15 },
                        new() { ItemId = 51853, Name = "Phantom Vision Wristwraps of Striking", Cost = 4000, RowIndex = 16 },
                        new() { ItemId = 51893, Name = "Phantom Vision Wristwraps of Scouting", Cost = 4000, RowIndex = 17 },
                        new() { ItemId = 51873, Name = "Phantom Vision Wristwraps of Aiming", Cost = 4000, RowIndex = 18 },
                        new() { ItemId = 51933, Name = "Phantom Vision Wristwraps of Casting", Cost = 4000, RowIndex = 19 },
                        new() { ItemId = 51913, Name = "Phantom Vision Wristwraps of Healing", Cost = 4000, RowIndex = 20 },
                        new() { ItemId = 51814, Name = "Phantom Vision Bottoms of Fending", Cost = 4000, RowIndex = 21 },
                        new() { ItemId = 51834, Name = "Phantom Vision Bottoms of Maiming", Cost = 4000, RowIndex = 22 },
                        new() { ItemId = 51854, Name = "Phantom Vision Sarouel of Striking", Cost = 4000, RowIndex = 23 },
                        new() { ItemId = 51894, Name = "Phantom Vision Sarouel of Scouting", Cost = 4000, RowIndex = 24 },
                        new() { ItemId = 51874, Name = "Phantom Vision Sarouel of Aiming", Cost = 4000, RowIndex = 25 },
                        new() { ItemId = 51934, Name = "Phantom Vision Sarouel of Casting", Cost = 4000, RowIndex = 26 },
                        new() { ItemId = 51914, Name = "Phantom Vision Sarouel of Healing", Cost = 4000, RowIndex = 27 },
                        new() { ItemId = 51815, Name = "Phantom Vision Sollerets of Fending", Cost = 4000, RowIndex = 28 },
                        new() { ItemId = 51835, Name = "Phantom Vision Sollerets of Maiming", Cost = 4000, RowIndex = 29 },
                        new() { ItemId = 51855, Name = "Phantom Vision Boots of Striking", Cost = 4000, RowIndex = 30 },
                        new() { ItemId = 51895, Name = "Phantom Vision Boots of Scouting", Cost = 4000, RowIndex = 31 },
                        new() { ItemId = 51875, Name = "Phantom Vision Boots of Aiming", Cost = 4000, RowIndex = 32 },
                        new() { ItemId = 51935, Name = "Phantom Vision Crakows of Casting", Cost = 4000, RowIndex = 33 },
                        new() { ItemId = 51915, Name = "Phantom Vision Crakows of Healing", Cost = 4000, RowIndex = 34 },
                    ],
                },
            ],
        },
        new ShopPageDefinition
        {
            MenuIndex = 1,
            MenuLabel = "Enlightenment Silver Obol Exchange (Other)",
            CurrencyItemId = 51975,
            CurrencyName = SilverObolName,
            Tabs =
            [
                new ShopTabDefinition
                {
                    TabId = 1,
                    TabLabel = "Armor",
                    Items =
                    [
                        new() { ItemId = 51952, Name = "Tule Tunic", Cost = 1000, RowIndex = 14 },
                        new() { ItemId = 51953, Name = "Tule Halfgloves", Cost = 1000, RowIndex = 15 },
                        new() { ItemId = 51954, Name = "Tule Trousers", Cost = 1000, RowIndex = 16 },
                        new() { ItemId = 51955, Name = "Tule Longboots", Cost = 1000, RowIndex = 17 },
                    ],
                },
                new ShopTabDefinition
                {
                    TabId = 3,
                    TabLabel = "Other",
                    Items =
                    [
                        new() { ItemId = 51967, Name = "Ninja's Soul Shard", Cost = 1000, RowIndex = 0 },
                        new() { ItemId = 51968, Name = "Black Mage's Soul Shard", Cost = 1000, RowIndex = 1 },
                        new() { ItemId = 51969, Name = "White Mage's Soul Shard", Cost = 1000, RowIndex = 2 },
                        new() { ItemId = 51973, Name = "Red Mage's Soul Shard", Cost = 1000, RowIndex = 3 },
                        new() { ItemId = 51966, Name = "North Horn Riding Map", Cost = 3000, RowIndex = 4 },
                        new() { ItemId = 52284, Name = "Nymian Uolosapa", Cost = 500, RowIndex = 5 },
                        new() { ItemId = 52366, Name = "Dungeon (Dawntrail) Orchestrion Roll", Cost = 1000, RowIndex = 6 },
                        new() { ItemId = 52367, Name = "Sealed Away (Dawntrail) Orchestrion Roll", Cost = 1000, RowIndex = 7 },
                        new() { ItemId = 51283, Name = "Crescent Stone Pillar", Cost = 400, RowIndex = 8 },
                        new() { ItemId = 51284, Name = "Crescent Wall Rack", Cost = 400, RowIndex = 9 },
                        new() { ItemId = 51282, Name = "Winged Scalekin Fossil", Cost = 400, RowIndex = 10 },
                        new() { ItemId = 47741, Name = "Occult Potion", Cost = 40, RowIndex = 11 },
                        new() { ItemId = 47740, Name = "Occult Coffer", Cost = 40, RowIndex = 12 },
                        new() { ItemId = 51978, Name = "Final Final Fixative", Cost = 1200, RowIndex = 13 },
                        new() { ItemId = 41759, Name = "Savage Aim Materia XI", Cost = 100, RowIndex = 18 },
                        new() { ItemId = 41772, Name = "Savage Aim Materia XII", Cost = 200, RowIndex = 19 },
                        new() { ItemId = 41760, Name = "Savage Might Materia XI", Cost = 100, RowIndex = 20 },
                        new() { ItemId = 41773, Name = "Savage Might Materia XII", Cost = 200, RowIndex = 21 },
                        new() { ItemId = 41758, Name = "Heavens' Eye Materia XI", Cost = 100, RowIndex = 22 },
                        new() { ItemId = 41771, Name = "Heavens' Eye Materia XII", Cost = 200, RowIndex = 23 },
                        new() { ItemId = 41768, Name = "Quickarm Materia XI", Cost = 100, RowIndex = 24 },
                        new() { ItemId = 41781, Name = "Quickarm Materia XII", Cost = 200, RowIndex = 25 },
                        new() { ItemId = 41769, Name = "Quicktongue Materia XI", Cost = 100, RowIndex = 26 },
                        new() { ItemId = 41782, Name = "Quicktongue Materia XII", Cost = 200, RowIndex = 27 },
                        new() { ItemId = 41761, Name = "Battledance Materia XI", Cost = 100, RowIndex = 28 },
                        new() { ItemId = 41774, Name = "Battledance Materia XII", Cost = 200, RowIndex = 29 },
                        new() { ItemId = 41757, Name = "Piety Materia XI", Cost = 100, RowIndex = 30 },
                        new() { ItemId = 41770, Name = "Piety Materia XII", Cost = 200, RowIndex = 31 },
                    ],
                },
            ],
        },
        new ShopPageDefinition
        {
            MenuIndex = 2,
            MenuLabel = "Enlightenment Gold Obol Exchange",
            CurrencyItemId = 51976,
            CurrencyName = GoldObolName,
            Tabs =
            [
                new ShopTabDefinition
                {
                    TabId = 1,
                    TabLabel = "Armor",
                    Items =
                    [
                        new() { ItemId = 51957, Name = "Torna Tunic", Cost = 1600, RowIndex = 9 },
                        new() { ItemId = 51958, Name = "Torna Wristlets", Cost = 1600, RowIndex = 10 },
                        new() { ItemId = 51960, Name = "Torna Boots", Cost = 1600, RowIndex = 11 },
                        new() { ItemId = 51961, Name = "Carwen Bandana", Cost = 1600, RowIndex = 12 },
                        new() { ItemId = 51962, Name = "Carwen Tunic", Cost = 1600, RowIndex = 13 },
                        new() { ItemId = 51963, Name = "Carwen Armlets", Cost = 1600, RowIndex = 14 },
                        new() { ItemId = 51964, Name = "Carwen Tights", Cost = 1600, RowIndex = 15 },
                        new() { ItemId = 51965, Name = "Carwen Boots", Cost = 1600, RowIndex = 16 },
                    ],
                },
                new ShopTabDefinition
                {
                    TabId = 3,
                    TabLabel = "Other",
                    Items =
                    [
                        new() { ItemId = 51970, Name = "Dragoon's Soul Shard", Cost = 1600, RowIndex = 0 },
                        new() { ItemId = 51971, Name = "Summoner's Soul Shard", Cost = 1600, RowIndex = 1 },
                        new() { ItemId = 52368, Name = "To the North Mountain (Dawntrail) Orchestrion Roll", Cost = 1600, RowIndex = 2 },
                        new() { ItemId = 51286, Name = "Blue Crescent Moon", Cost = 800, RowIndex = 3 },
                        new() { ItemId = 51287, Name = "Yellow Crescent Cube", Cost = 800, RowIndex = 4 },
                        new() { ItemId = 51288, Name = "Red Crescent Crystal", Cost = 800, RowIndex = 5 },
                        new() { ItemId = 47741, Name = "Occult Potion", Cost = 50, RowIndex = 6 },
                        new() { ItemId = 47740, Name = "Occult Coffer", Cost = 50, RowIndex = 7 },
                        new() { ItemId = 51978, Name = "Final Final Fixative", Cost = 1920, RowIndex = 8 },
                        new() { ItemId = 41759, Name = "Savage Aim Materia XI", Cost = 160, RowIndex = 17 },
                        new() { ItemId = 41772, Name = "Savage Aim Materia XII", Cost = 320, RowIndex = 18 },
                        new() { ItemId = 41760, Name = "Savage Might Materia XI", Cost = 160, RowIndex = 19 },
                        new() { ItemId = 41773, Name = "Savage Might Materia XII", Cost = 320, RowIndex = 20 },
                        new() { ItemId = 41758, Name = "Heavens' Eye Materia XI", Cost = 160, RowIndex = 21 },
                        new() { ItemId = 41771, Name = "Heavens' Eye Materia XII", Cost = 320, RowIndex = 22 },
                        new() { ItemId = 41768, Name = "Quickarm Materia XI", Cost = 160, RowIndex = 23 },
                        new() { ItemId = 41781, Name = "Quickarm Materia XII", Cost = 320, RowIndex = 24 },
                        new() { ItemId = 41769, Name = "Quicktongue Materia XI", Cost = 160, RowIndex = 25 },
                        new() { ItemId = 41782, Name = "Quicktongue Materia XII", Cost = 320, RowIndex = 26 },
                        new() { ItemId = 41761, Name = "Battledance Materia XI", Cost = 160, RowIndex = 27 },
                        new() { ItemId = 41774, Name = "Battledance Materia XII", Cost = 320, RowIndex = 28 },
                        new() { ItemId = 41757, Name = "Piety Materia XI", Cost = 160, RowIndex = 29 },
                        new() { ItemId = 41770, Name = "Piety Materia XII", Cost = 320, RowIndex = 30 },
                    ],
                },
            ],
        },
    ];

    /// <summary>All known item ids across every page/tab (for config sanitising).</summary>
    public static HashSet<uint> KnownItemIds =>
    [
        .. Pages.SelectMany(p => p.Tabs).SelectMany(t => t.Items).Select(i => i.ItemId),
    ];

    public static bool TryGet(uint itemId, out ShopItemDefinition entry)
    {
        foreach (ShopPageDefinition page in Pages)
        {
            foreach (ShopTabDefinition tab in page.Tabs)
            {
                foreach (ShopItemDefinition item in tab.Items)
                {
                    if (item.ItemId != itemId)
                    {
                        continue;
                    }

                    entry = item;
                    return true;
                }
            }
        }

        entry = null!;
        return false;
    }
}
