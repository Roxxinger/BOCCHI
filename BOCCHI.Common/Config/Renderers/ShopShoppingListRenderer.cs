using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.Shopping;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Ocelot.Config.Renderers;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using System.Numerics;
using System.Reflection;

namespace BOCCHI.Common.Config.Renderers;

/// <summary>Full Shopping config page — enable, thresholds, Keep / Buy / Sink list.</summary>
public class ShopShoppingListRenderer(
    IZoneProvider zones,
    ISupportJobFactory supportJobs,
    IDataManager data,
    IUnlockState unlockState,
    ITextureProvider textures)
    : IFieldRenderer<ShopShoppingListAttribute>
{
    private string catalogSearch = string.Empty;

    private const string AddPopupId = "bocchi_shop_add_popup";

    public bool Render(object target, PropertyInfo prop, ShopShoppingListAttribute attr, Type owner, ITranslator translator)
    {
        if (target is not ShoppingConfig config)
        {
            throw new InvalidOperationException("[ShopShoppingListRenderer] must be used on ShoppingConfig.");
        }

        if (prop.PropertyType != typeof(List<uint>))
        {
            throw new InvalidOperationException(
                $"[ShopShoppingListRenderer] requires List<uint>. {prop.DeclaringType?.Name}.{prop.Name} is {prop.PropertyType.Name}.");
        }

        config.ShoppingOrder ??= [];
        config.Shopping ??= new();
        prop.SetValue(target, config.ShoppingOrder);

        string listKey = prop.GetFieldLabelKey(owner);
        bool changed = false;

        changed |= DrawHeader(config, translator);
        BocchiUi.EndStickyHeader();
        changed |= DrawThresholds(config, translator);
        changed |= DrawListSection(config, listKey, translator);
        return changed;
    }

    private bool DrawHeader(ShoppingConfig config, ITranslator translator)
    {
        bool changed = false;
        if (BocchiUi.BeginPanel("shop_enable"))
        {
            bool enabled = config.EnableAutoShop;
            if (ImGui.Checkbox(translator.T("config.shopping.fields.enable_auto_shop.label"), ref enabled))
            {
                config.EnableAutoShop = enabled;
                changed = true;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(translator.T("config.shopping.fields.enable_auto_shop.tooltip"));
            }

            ZoneId zone = zones.GetZone().ZoneId;
            ImGui.SameLine(0, 16);
            BocchiUi.DrawStatusChip(
                zone switch
                {
                    ZoneId.SouthHorn => translator.T("config.shopping.fields.shopping_order.zone_south"),
                    ZoneId.NorthHorn => translator.T("config.shopping.fields.shopping_order.zone_north"),
                    _ => translator.T("config.shopping.fields.shopping_order.zone_any"),
                },
                zone is ZoneId.SouthHorn or ZoneId.NorthHorn
                    ? BocchiUi.StatusChipKind.Ok
                    : BocchiUi.StatusChipKind.Muted);

            BocchiUi.EndPanel();
        }

        return changed;
    }

    private static bool DrawThresholds(ShoppingConfig config, ITranslator translator)
    {
        bool changed = false;
        if (!BocchiUi.BeginPanel("shop_thresholds"))
        {
            return false;
        }

        BocchiUi.SectionTitle(translator.T("config.shopping.fields.shopping_order.when_ready"));
        ImGui.Spacing();
        BocchiUi.MutedWrapped(translator.T("config.shopping.fields.shopping_order.when_ready_hint"));
        ImGui.Spacing();

        if (ImGui.BeginTable("##shop_thresholds", 2, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            changed |= DrawCurrencyColumn(
                translator.T("config.shopping.fields.shopping_order.silver"),
                config.SilverThreshold,
                config.ReserveSilver,
                v => config.SilverThreshold = v,
                v => config.ReserveSilver = v,
                "config.shopping.fields.silver_threshold",
                "config.shopping.fields.reserve_silver",
                translator);

            ImGui.TableNextColumn();
            changed |= DrawCurrencyColumn(
                translator.T("config.shopping.fields.shopping_order.gold"),
                config.GoldThreshold,
                config.ReserveGold,
                v => config.GoldThreshold = v,
                v => config.ReserveGold = v,
                "config.shopping.fields.gold_threshold",
                "config.shopping.fields.reserve_gold",
                translator);

            ImGui.EndTable();
        }

        BocchiUi.EndPanel();
        return changed;
    }

    private static bool DrawCurrencyColumn(
        string title,
        int threshold,
        int reserve,
        Action<int> setThreshold,
        Action<int> setReserve,
        string thresholdKey,
        string reserveKey,
        ITranslator translator)
    {
        bool changed = false;
        BocchiUi.SectionTitle(title);
        ImGui.Spacing();

        ImGui.TextUnformatted(translator.T($"{thresholdKey}.label"));
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(translator.T($"{thresholdKey}.tooltip"));
        }

        ImGui.SetNextItemWidth(-1);
        int t = threshold;
        if (ImGui.SliderInt($"##thr_{title}", ref t, 0, 9999))
        {
            setThreshold(t);
            changed = true;
        }

        ImGui.TextUnformatted(translator.T($"{reserveKey}.label"));
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(translator.T($"{reserveKey}.tooltip"));
        }

        ImGui.SetNextItemWidth(-1);
        int r = reserve;
        if (ImGui.SliderInt($"##rsv_{title}", ref r, 0, 9999))
        {
            setReserve(r);
            changed = true;
        }

        return changed;
    }

    private bool DrawListSection(ShoppingConfig config, string listKey, ITranslator translator)
    {
        bool changed = false;
        bool openAdd = false;
        if (BocchiUi.BeginPanel("shop_list"))
        {
            BocchiUi.SectionTitle(translator.T(listKey));
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(translator.T(Key(listKey, ".modes_help")));
            }

            ImGui.Spacing();
            BocchiUi.MutedWrapped(translator.T(Key(listKey, ".tooltip")));
            ImGui.Spacing();

            if (ImGui.Button(translator.T(Key(listKey, ".add"))))
            {
                openAdd = true;
            }

            ImGui.SameLine();
            if (ImGui.Button(translator.T(Key(listKey, ".clear"))))
            {
                config.ShoppingOrder.Clear();
                config.Shopping.Clear();
                changed = true;
            }

            BocchiUi.EndPanel();
        }

        // Open popup after panels so ChannelsSplit clipping doesn't shrink it.
        if (openAdd)
        {
            ImGui.OpenPopup(AddPopupId);
        }

        changed |= DrawAddPopup(config, listKey, translator);
        ImGui.Spacing();

        if (config.ShoppingOrder.Count == 0)
        {
            BocchiUi.MutedText(translator.T(Key(listKey, ".empty")));
            return changed;
        }

        ZoneId zone = zones.GetZone().ZoneId;
        string ownedLabel = translator.T(Key(listKey, ".owned"));
        string missingLabel = translator.T(Key(listKey, ".missing"));
        string repeatableLabel = translator.T(Key(listKey, ".repeatable"));

        for (int i = 0; i < config.ShoppingOrder.Count; i++)
        {
            uint itemId = config.ShoppingOrder[i];
            if (!config.Shopping.TryGetValue(itemId, out ShopListEntry? setting) || setting == null)
            {
                setting = new ShopListEntry();
                config.Shopping[itemId] = setting;
                changed = true;
            }

            ShopCatalogEntry? catalog = ResolveCatalog(itemId, zone, setting);
            string name = catalog?.Name
                          ?? (data.GetExcelSheet<Item>().TryGetRow(itemId, out Item row) ? row.Name.ToString() : $"#{itemId}");
            string costLabel = FormatOfferCosts(itemId, zone, setting.PreferredCurrencies, listKey, translator);
            int have = InventoryItemAssist.Count(itemId);
            bool? owned = catalog is { } c
                ? ShopOwnership.TryIsOwned(c, supportJobs, data, unlockState)
                : null;

            ImGui.PushID((int)itemId);
            if (BocchiUi.BeginPanel($"row_{itemId}"))
            {
                DrawItemIcon(catalog?.ItemId ?? itemId);
                ImGui.TextUnformatted(name);
                ImGui.SameLine();
                BocchiUi.MutedText(costLabel);
                ImGui.SameLine(0, 12);
                BocchiUi.DrawStatusChip(
                    owned switch
                    {
                        true => ownedLabel,
                        false => missingLabel,
                        null => repeatableLabel,
                    },
                    owned switch
                    {
                        true => BocchiUi.StatusChipKind.Ok,
                        false => BocchiUi.StatusChipKind.Warn,
                        null => BocchiUi.StatusChipKind.Muted,
                    });

                ImGui.SameLine();
                float right = ImGui.GetContentRegionAvail().X;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, right - 90));
                using (ImRaii.Disabled(i == 0))
                {
                    if (ImGui.SmallButton("^") && i > 0)
                    {
                        (config.ShoppingOrder[i - 1], config.ShoppingOrder[i]) =
                            (config.ShoppingOrder[i], config.ShoppingOrder[i - 1]);
                        changed = true;
                    }
                }

                ImGui.SameLine();
                using (ImRaii.Disabled(i >= config.ShoppingOrder.Count - 1))
                {
                    if (ImGui.SmallButton("v") && i < config.ShoppingOrder.Count - 1)
                    {
                        (config.ShoppingOrder[i + 1], config.ShoppingOrder[i]) =
                            (config.ShoppingOrder[i], config.ShoppingOrder[i + 1]);
                        changed = true;
                    }
                }

                ImGui.SameLine();
                using (ImRaii.PushFont(UiBuilder.IconFont))
                {
                    if (ImGui.SmallButton($"{FontAwesomeIcon.Trash.ToIconString()}##rm"))
                    {
                        config.Shopping.Remove(itemId);
                        config.ShoppingOrder.RemoveAt(i);
                        changed = true;
                        BocchiUi.EndPanel();
                        ImGui.PopID();
                        break;
                    }
                }

                BocchiUi.MutedText($"{translator.T(Key(listKey, ".col_have"))}: {have}");
                ImGui.Spacing();

                bool blockPurchase = catalog is { } blocked
                    && ShopOwnership.ShouldBlockPurchase(blocked, supportJobs, data, unlockState);
                using (ImRaii.Disabled(blockPurchase))
                {
                    changed |= DrawAmountField(
                        translator.T(Key(listKey, ".col_keep")),
                        translator.T(Key(listKey, ".col_keep_hint")),
                        "##keep",
                        setting.KeepAmount,
                        v => setting.KeepAmount = v);

                    ImGui.SameLine(0, 16);
                    changed |= DrawAmountField(
                        translator.T(Key(listKey, ".col_buy")),
                        translator.T(Key(listKey, ".col_buy_hint")),
                        "##buy",
                        setting.BuyAmount,
                        v => setting.BuyAmount = v);

                    ImGui.SameLine(0, 16);
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(translator.T(Key(listKey, ".col_keep_buying")));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(translator.T(Key(listKey, ".col_keep_buying_hint")));
                    }

                    ImGui.SameLine();
                    bool keepBuying = setting.KeepBuying;
                    if (ImGui.Checkbox("##sink", ref keepBuying))
                    {
                        if (keepBuying)
                        {
                            foreach (ShopListEntry other in config.Shopping.Values)
                            {
                                other.KeepBuying = false;
                            }
                        }

                        setting.KeepBuying = keepBuying;
                        changed = true;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(translator.T(Key(listKey, ".col_keep_buying_hint")));
                    }
                }

                ShopCurrencyPreference available = ShopCatalog.AvailableCurrenciesForUi(itemId, zone);
                if (CountFlags(available) > 1)
                {
                    ImGui.Spacing();
                    BocchiUi.MutedText(translator.T(Key(listKey, ".currency_pick")));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(translator.T(Key(listKey, ".currency_pick_hint")));
                    }

                    changed |= DrawCurrencyChecks(setting, available, listKey, translator);
                }

                BocchiUi.EndPanel();
            }

            ImGui.PopID();
        }

        return changed;
    }

    private static bool DrawCurrencyChecks(
        ShopListEntry setting,
        ShopCurrencyPreference available,
        string listKey,
        ITranslator translator)
    {
        bool changed = false;
        // None means "any" — edit a full copy of available flags until saved.
        ShopCurrencyPreference working = setting.PreferredCurrencies == ShopCurrencyPreference.None
            ? available
            : setting.PreferredCurrencies & available;

        changed |= DrawCurrencyCheck(
            available,
            ShopCurrencyPreference.Silver,
            translator.T(Key(listKey, ".currency_silver")),
            ref working);
        ImGui.SameLine(0, 12);
        changed |= DrawCurrencyCheck(
            available,
            ShopCurrencyPreference.Gold,
            translator.T(Key(listKey, ".currency_gold")),
            ref working);
        ImGui.SameLine(0, 12);
        changed |= DrawCurrencyCheck(
            available,
            ShopCurrencyPreference.Amulet,
            translator.T(Key(listKey, ".currency_amulet")),
            ref working);

        if (changed)
        {
            setting.PreferredCurrencies = working == available
                ? ShopCurrencyPreference.None
                : working;
        }

        return changed;
    }

    private static bool DrawCurrencyCheck(
        ShopCurrencyPreference available,
        ShopCurrencyPreference flag,
        string label,
        ref ShopCurrencyPreference working)
    {
        if (!available.HasFlag(flag))
        {
            return false;
        }

        bool on = working.HasFlag(flag);
        if (!ImGui.Checkbox(label, ref on))
        {
            return false;
        }

        if (on)
        {
            working |= flag;
        }
        else
        {
            working &= ~flag;
            // Keep at least one currency selected among what's available.
            if ((working & available) == ShopCurrencyPreference.None)
            {
                working |= flag;
            }
        }

        return true;
    }

    private static int CountFlags(ShopCurrencyPreference flags)
    {
        int n = 0;
        if (flags.HasFlag(ShopCurrencyPreference.Silver))
        {
            n++;
        }

        if (flags.HasFlag(ShopCurrencyPreference.Gold))
        {
            n++;
        }

        if (flags.HasFlag(ShopCurrencyPreference.Amulet))
        {
            n++;
        }

        return n;
    }

    private static string FormatOfferCosts(
        uint itemId,
        ZoneId zone,
        ShopCurrencyPreference preferred,
        string? listKey = null,
        ITranslator? translator = null)
    {
        List<ShopCatalogEntry> offers = DistinctByCurrency(
                ShopCatalog.PreferredOffers(itemId, zone, preferred, fallbackAnyZone: true))
            .ToList();
        if (offers.Count == 0)
        {
            return "-";
        }

        if (offers.Count == 1)
        {
            return $"{offers[0].Cost}";
        }

        bool nameCurrencies = listKey != null && translator != null;
        return string.Join(
            " / ",
            offers.Select(o =>
            {
                if (!nameCurrencies)
                {
                    return o.Cost.ToString();
                }

                string cur = ShopCatalog.CurrencyKindOf(o.CurrencyItemId) switch
                {
                    ShopCurrencyPreference.Silver => translator!.T(Key(listKey!, ".currency_silver")),
                    ShopCurrencyPreference.Gold => translator!.T(Key(listKey!, ".currency_gold")),
                    ShopCurrencyPreference.Amulet => translator!.T(Key(listKey!, ".currency_amulet")),
                    _ => "?",
                };
                return $"{o.Cost} {cur}";
            }));
    }

    private static IEnumerable<ShopCatalogEntry> DistinctByCurrency(IEnumerable<ShopCatalogEntry> offers)
    {
        HashSet<ShopCurrencyPreference> seen = [];
        foreach (ShopCatalogEntry o in offers)
        {
            ShopCurrencyPreference kind = ShopCatalog.CurrencyKindOf(o.CurrencyItemId);
            if (kind == ShopCurrencyPreference.None || !seen.Add(kind))
            {
                continue;
            }

            yield return o;
        }
    }

    private static bool DrawAmountField(
        string label,
        string hint,
        string id,
        int value,
        Action<int> set)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(hint);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(56);
        int v = value;
        bool changed = ImGui.InputInt(id, ref v, 0, 0);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(hint);
        }

        if (changed)
        {
            set(Math.Max(0, v));
        }

        return changed;
    }

    private bool DrawAddPopup(ShoppingConfig config, string listKey, ITranslator translator)
    {
        bool changed = false;
        ImGui.SetNextWindowSize(new Vector2(560, 420), ImGuiCond.Always);
        ImGui.SetNextWindowSizeConstraints(new Vector2(520, 360), new Vector2(720, 560));
        if (!ImGui.BeginPopup(AddPopupId))
        {
            return false;
        }

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint(
            "##shop_catalog_search",
            translator.T(Key(listKey, ".search_hint")),
            ref catalogSearch,
            64);

        ZoneId zone = zones.GetZone().ZoneId;
        string ownedLabel = translator.T(Key(listKey, ".owned"));
        IEnumerable<ShopCatalogEntry> catalogRows = ShopCatalog.All
            .Where(e => zone is ZoneId.SouthHorn or ZoneId.NorthHorn ? e.Zone == zone : true)
            .Where(e => e.ItemId != 0)
            .Where(e => string.IsNullOrWhiteSpace(catalogSearch)
                        || e.Name.Contains(catalogSearch, StringComparison.OrdinalIgnoreCase)
                        || e.Section.Contains(catalogSearch, StringComparison.OrdinalIgnoreCase));

        float listHeight = Math.Max(240f, ImGui.GetContentRegionAvail().Y - 8f);
        using (ImRaii.Child("##shop_catalog_add", new Vector2(-1, listHeight), true))
        {
            string? lastSection = null;
            HashSet<uint> listed = [];
            foreach (ShopCatalogEntry entry in catalogRows)
            {
                if (!listed.Add(entry.ItemId))
                {
                    continue;
                }

                if (!string.Equals(lastSection, entry.Section, StringComparison.Ordinal))
                {
                    lastSection = entry.Section;
                    ImGui.Separator();
                    BocchiUi.MutedText($"{entry.Zone} · {entry.Section}");
                }

                bool owned = ShopOwnership.TryIsOwned(entry, supportJobs, data, unlockState) == true;
                bool blockPurchase = ShopOwnership.ShouldBlockPurchase(entry, supportJobs, data, unlockState);
                bool already = config.Shopping.ContainsKey(entry.ItemId);
                bool locked = already || blockPurchase;
                string cost = FormatOfferCosts(entry.ItemId, zone, ShopCurrencyPreference.None);
                string label = $"{entry.Name}  ({cost})##add_{entry.ItemId}";

                using (ImRaii.Disabled(locked))
                {
                    if (ImGui.Selectable(label, false) && !locked)
                    {
                        config.Shopping[entry.ItemId] = new ShopListEntry { BuyAmount = 1 };
                        config.ShoppingOrder.Add(entry.ItemId);
                        changed = true;
                    }
                }

                bool rowHovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
                if (owned)
                {
                    ImGui.SameLine();
                    BocchiUi.DrawStatusChip(ownedLabel, BocchiUi.StatusChipKind.Muted);
                }

                if (rowHovered)
                {
                    string tip = already
                        ? translator.T(Key(listKey, ".already_listed"))
                        : blockPurchase
                            ? $"{entry.Name}\n{cost}\n{ownedLabel}"
                            : owned
                                ? $"{entry.Name}\n{cost}\n{ownedLabel}"
                                : $"{entry.Name}\n{cost}";
                    ImGui.SetTooltip(tip);
                }
            }
        }

        ImGui.EndPopup();
        return changed;
    }

    private void DrawItemIcon(uint itemId)
    {
        if (!data.GetExcelSheet<Item>().TryGetRow(itemId, out Item item) || item.Icon == 0)
        {
            return;
        }

        ISharedImmediateTexture tex = textures.GetFromGameIcon(new GameIconLookup(item.Icon));
        ImGui.Image(tex.GetWrapOrEmpty().Handle, new Vector2(22, 22));
        ImGui.SameLine();
    }

    private static ShopCatalogEntry? ResolveCatalog(
        uint itemId,
        ZoneId zone,
        ShopListEntry? setting = null)
    {
        ShopCurrencyPreference preferred = setting?.PreferredCurrencies ?? ShopCurrencyPreference.None;
        foreach (ShopCatalogEntry e in ShopCatalog.PreferredOffers(itemId, zone, preferred, fallbackAnyZone: true))
        {
            return e;
        }

        return ShopCatalog.TryGet(itemId, out ShopCatalogEntry any) ? any : null;
    }

    private static string Key(string fieldKey, string suffix) =>
        fieldKey.EndsWith(".label", StringComparison.Ordinal)
            ? fieldKey.Replace(".label", suffix, StringComparison.Ordinal)
            : fieldKey + suffix;
}
