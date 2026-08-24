using BOCCHI.Common.Config;
using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Data.Shopping;
using BOCCHI.Common.Data.Zones;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Ocelot.Config.Renderers;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using System.Numerics;
using System.Reflection;

namespace BOCCHI.Common.Config.Renderers;

/// <summary>
///     AOCCH-style shopping target editor. Add items via page → tab → item dropdowns,
///     then manage them in a priority table: reorder up/down, Keep / Buy amounts, a single
///     Keep Buying slot and remove. Currency reserve/threshold rows sit above the list.
///     Saves flow through the config renderer's dirty flag (returning changed = true).
/// </summary>
public sealed class ShopTargetListRenderer(IZoneProvider zones) : IFieldRenderer<ShopTargetListAttribute>
{
    private int pageIndex;
    private int tabIndex;
    private int itemIndex;

    public bool Render(object target, PropertyInfo prop, ShopTargetListAttribute attr, Type owner, ITranslator translator)
    {
        if (prop.PropertyType != typeof(List<CurrencyShopTarget>))
        {
            throw new InvalidOperationException(
                $"[ShopTargetListRenderer] must be used on List<{nameof(CurrencyShopTarget)}> properties. "
                + $"{prop.DeclaringType?.Name}.{prop.Name} is {prop.PropertyType.Name}.");
        }

        List<CurrencyShopTarget> targets = (List<CurrencyShopTarget>?)prop.GetValue(target) ?? [];
        if (prop.GetValue(target) == null)
        {
            prop.SetValue(target, targets);
        }

        string fieldKey = prop.GetFieldLabelKey(owner);
        ImGui.TextUnformatted(translator.T(fieldKey));
        prop.Tooltip(owner, translator);

        bool changed = false;
        var config = (ShoppingConfig)target;

        // Only the current zone's territory key is editable here — matches how AOCCH
        // scopes the whole tab to the active territory.
        var territoryKey = TerritoryKey();
        var pages = ShopCatalog.Pages.Where(p => p.Tabs.Any(t => t.Items.Count > 0)).ToList();

        changed |= DrawCurrencyTable(translator, fieldKey, config, territoryKey, pages);

        ImGui.Separator();

        // ------------------------------------------------------------- add item
        ImGui.TextUnformatted(translator.T(fieldKey.Replace(".label", ".add_hint", StringComparison.Ordinal)));
        if (pages.Count == 0)
        {
            ImGui.TextUnformatted(translator.T(fieldKey.Replace(".label", ".empty_catalog", StringComparison.Ordinal)));
            return changed;
        }

        pageIndex = Math.Clamp(pageIndex, 0, pages.Count - 1);
        var selectedPage = pages[pageIndex];
        var pageLabels = pages.Select(p => p.MenuLabel).ToArray();
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.Combo("##shop-page", ref pageIndex, pageLabels, pageLabels.Length))
        {
            tabIndex = 0;
            itemIndex = 0;
        }

        selectedPage = pages[Math.Clamp(pageIndex, 0, pages.Count - 1)];
        var tabs = selectedPage.Tabs.Where(t => t.Items.Count > 0).ToList();
        if (tabs.Count > 0)
        {
            tabIndex = Math.Clamp(tabIndex, 0, tabs.Count - 1);
            var tabLabels = tabs.Select(t => t.TabLabel).ToArray();
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.Combo("##shop-tab", ref tabIndex, tabLabels, tabLabels.Length))
            {
                itemIndex = 0;
            }

            var selectedTab = tabs[Math.Clamp(tabIndex, 0, tabs.Count - 1)];
            var items = selectedTab.Items.ToList();
            itemIndex = Math.Clamp(itemIndex, 0, items.Count - 1);
            var itemLabels = items.Select(i => i.Name).ToArray();

            float addButtonWidth = ImGui.CalcTextSize(translator.T(fieldKey.Replace(".label", ".add", StringComparison.Ordinal))).X
                                   + ImGui.GetStyle().FramePadding.X * 2f;
            ImGui.SetNextItemWidth(-addButtonWidth - ImGui.GetStyle().ItemSpacing.X);
            ImGui.Combo("##shop-item", ref itemIndex, itemLabels, itemLabels.Length);
            ImGui.SameLine();

            if (ImGui.Button(translator.T(fieldKey.Replace(".label", ".add", StringComparison.Ordinal))))
            {
                var selectedItem = items[itemIndex];
                if (!targets.Any(t => string.Equals(t.TerritoryKey, territoryKey, StringComparison.OrdinalIgnoreCase)
                                      && t.ItemId == selectedItem.ItemId
                                      && t.MenuIndex == selectedPage.MenuIndex
                                      && t.TabId == selectedTab.TabId))
                {
                    targets.Add(new CurrencyShopTarget
                    {
                        TerritoryKey = territoryKey,
                        ItemId = selectedItem.ItemId,
                        MenuIndex = selectedPage.MenuIndex,
                        TabId = selectedTab.TabId,
                        KeepAmount = 0,
                        BuyAmount = 1,
                        KeepBuying = false,
                        Priority = targets.Count(t => string.Equals(t.TerritoryKey, territoryKey, StringComparison.OrdinalIgnoreCase)),
                    });
                    NormalizePriorities(targets);
                    changed = true;
                }
            }
        }

        ImGui.Separator();

        // ------------------------------------------------------- priority table
        ImGui.TextUnformatted(translator.T(fieldKey.Replace(".label", ".priority_list", StringComparison.Ordinal)));
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            using var tt = ImRaii.Tooltip();
            ImGui.TextUnformatted(translator.T(fieldKey.Replace(".label", ".priority_tooltip", StringComparison.Ordinal)));
        }

        var activeIndices = targets
            .Select((t, i) => (T: t, I: i))
            .Where(x => string.Equals(x.T.TerritoryKey, territoryKey, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.I)
            .ToList();

        if (activeIndices.Count == 0)
        {
            ImGui.TextUnformatted(translator.T(fieldKey.Replace(".label", ".none", StringComparison.Ordinal)));
            return changed;
        }

        float rowHeight = ImGui.GetFrameHeightWithSpacing();
        using (var child = ImRaii.Child("##shop-priority-list", new Vector2(0, rowHeight * Math.Min(activeIndices.Count + 1, 11) + 8f), true))
        {
            if (child.Success)
            {
                using var table = ImRaii.Table("##shop-priority-table", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp);
                if (table)
                {
                    ImGui.TableSetupColumn(translator.T(fieldKey.Replace(".label", ".col_order", StringComparison.Ordinal)), ImGuiTableColumnFlags.WidthFixed, 70f);
                    ImGui.TableSetupColumn(translator.T(fieldKey.Replace(".label", ".col_item", StringComparison.Ordinal)));
                    ImGui.TableSetupColumn(translator.T(fieldKey.Replace(".label", ".col_keep", StringComparison.Ordinal)), ImGuiTableColumnFlags.WidthFixed, 70f);
                    ImGui.TableSetupColumn(translator.T(fieldKey.Replace(".label", ".col_buy", StringComparison.Ordinal)), ImGuiTableColumnFlags.WidthFixed, 70f);
                    ImGui.TableSetupColumn(translator.T(fieldKey.Replace(".label", ".col_keep_buying", StringComparison.Ordinal)), ImGuiTableColumnFlags.WidthFixed, 90f);
                    ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 34f);
                    ImGui.TableHeadersRow();

                    for (var display = 0; display < activeIndices.Count; display++)
                    {
                        var i = activeIndices[display];
                        var t = targets[i];

                        ImGui.PushID($"shop-target-{i}");
                        ImGui.TableNextRow();

                        // Order: up/down buttons.
                        ImGui.TableSetColumnIndex(0);
                        Vector2 iconSize = new(ImGui.GetFrameHeight(), ImGui.GetFrameHeight());
                        using (ImRaii.Disabled(display <= 0))
                        {
                            if (ImGui.Button($"{FontAwesomeIcon.AngleUp.ToIconString()}##up", iconSize) && display > 0)
                            {
                                (targets[activeIndices[display - 1]], targets[i]) = (targets[i], targets[activeIndices[display - 1]]);
                                NormalizePriorities(targets);
                                changed = true;
                            }
                        }

                        ImGui.SameLine();
                        using (ImRaii.Disabled(display >= activeIndices.Count - 1))
                        {
                            if (ImGui.Button($"{FontAwesomeIcon.AngleDown.ToIconString()}##down", iconSize) && display < activeIndices.Count - 1)
                            {
                                (targets[activeIndices[display + 1]], targets[i]) = (targets[i], targets[activeIndices[display + 1]]);
                                NormalizePriorities(targets);
                                changed = true;
                            }
                        }

                        // Item name.
                        ImGui.TableSetColumnIndex(1);
                        ImGui.TextUnformatted(ItemName(t));

                        // Keep.
                        ImGui.TableSetColumnIndex(2);
                        int keep = t.KeepAmount;
                        ImGui.SetNextItemWidth(-1f);
                        if (ImGui.InputInt("##keep", ref keep) && keep >= 0 && keep != t.KeepAmount)
                        {
                            t.KeepAmount = keep;
                            changed = true;
                        }

                        // Buy.
                        ImGui.TableSetColumnIndex(3);
                        int buy = t.BuyAmount;
                        ImGui.SetNextItemWidth(-1f);
                        if (ImGui.InputInt("##buy", ref buy) && buy >= 0 && buy != t.BuyAmount)
                        {
                            t.BuyAmount = buy;
                            changed = true;
                        }

                        // Keep Buying — only one per territory, like AOCCH.
                        ImGui.TableSetColumnIndex(4);
                        bool keepBuying = t.KeepBuying;
                        if (ImGui.Checkbox("##keepbuying", ref keepBuying) && keepBuying != t.KeepBuying)
                        {
                            if (keepBuying)
                            {
                                foreach (var other in targets.Where(o => string.Equals(o.TerritoryKey, territoryKey, StringComparison.OrdinalIgnoreCase)))
                                {
                                    other.KeepBuying = false;
                                }
                            }

                            t.KeepBuying = keepBuying;
                            changed = true;
                        }

                        // Remove.
                        ImGui.TableSetColumnIndex(5);
                        if (ImGui.Button($"{FontAwesomeIcon.Trash.ToIconString()}##rm", iconSize))
                        {
                            targets.RemoveAt(i);
                            NormalizePriorities(targets);
                            changed = true;
                            ImGui.PopID();
                            break;
                        }

                        ImGui.PopID();
                    }
                }
            }
        }

        return changed;
    }

    /// <summary>Reserve + threshold inputs per currency of this territory.</summary>
    private bool DrawCurrencyTable(
        ITranslator translator, string fieldKey, ShoppingConfig config, string territoryKey, List<ShopPageDefinition> pages)
    {
        bool changed = false;
        var currencies = pages
            .GroupBy(p => p.CurrencyItemId)
            .Select(g => g.First())
            .ToList();
        if (currencies.Count == 0)
        {
            return false;
        }

        ImGui.TextUnformatted(translator.T(fieldKey.Replace(".label", ".currency", StringComparison.Ordinal)));

        using (var table = ImRaii.Table("##shop-currency-table", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
        {
            if (table)
            {
                ImGui.TableSetupColumn(translator.T(fieldKey.Replace(".label", ".col_currency", StringComparison.Ordinal)));
                ImGui.TableSetupColumn(translator.T(fieldKey.Replace(".label", ".col_reserved", StringComparison.Ordinal)), ImGuiTableColumnFlags.WidthFixed, 90f);
                ImGui.TableSetupColumn(translator.T(fieldKey.Replace(".label", ".col_threshold", StringComparison.Ordinal)), ImGuiTableColumnFlags.WidthFixed, 90f);
                ImGui.TableHeadersRow();

                foreach (var currency in currencies)
                {
                    var currencyItemId = currency.CurrencyItemId;
                    var reserveSetting = config.Reserves.FirstOrDefault(r =>
                        string.Equals(r.TerritoryKey, territoryKey, StringComparison.OrdinalIgnoreCase) && r.CurrencyItemId == currencyItemId);
                    var thresholdSetting = config.Thresholds.FirstOrDefault(r =>
                        string.Equals(r.TerritoryKey, territoryKey, StringComparison.OrdinalIgnoreCase) && r.CurrencyItemId == currencyItemId);

                    ImGui.PushID($"shop-cur-{currencyItemId}");
                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextUnformatted(currency.CurrencyName);

                    ImGui.TableNextColumn();
                    int reserve = reserveSetting?.ReserveAmount ?? 0;
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.InputInt("##reserve", ref reserve))
                    {
                        reserve = Math.Clamp(reserve, 0, 9999);
                        if (reserveSetting == null)
                        {
                            reserveSetting = new CurrencyShopReserveSetting
                            {
                                TerritoryKey = territoryKey,
                                CurrencyItemId = currencyItemId,
                                ReserveAmount = reserve,
                            };
                            config.Reserves.Add(reserveSetting);
                        }

                        if (reserveSetting != null && reserveSetting.ReserveAmount != reserve)
                        {
                            reserveSetting.ReserveAmount = reserve;
                            changed = true;
                        }
                    }

                    ImGui.TableNextColumn();
                    int threshold = thresholdSetting?.StartThreshold ?? 0;
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.InputInt("##threshold", ref threshold))
                    {
                        threshold = Math.Clamp(threshold, 0, 9999);
                        if (thresholdSetting == null)
                        {
                            thresholdSetting = new CurrencyShopThresholdSetting
                            {
                                TerritoryKey = territoryKey,
                                CurrencyItemId = currencyItemId,
                                StartThreshold = threshold,
                            };
                            config.Thresholds.Add(thresholdSetting);
                        }

                        if (thresholdSetting != null && thresholdSetting.StartThreshold != threshold)
                        {
                            thresholdSetting.StartThreshold = threshold;
                            changed = true;
                        }
                    }

                    ImGui.PopID();
                }
            }
        }

        return changed;
    }

    private static string ItemName(CurrencyShopTarget t) =>
        ShopCatalog.TryGet(t.ItemId, out var def) ? def.Name : $"[{t.TerritoryKey}] Item {t.ItemId}";

    private static void NormalizePriorities(List<CurrencyShopTarget> targets)
    {
        foreach (var territory in targets.Select(t => t.TerritoryKey).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var priority = 0;
            foreach (var t in targets.Where(t => string.Equals(t.TerritoryKey, territory, StringComparison.OrdinalIgnoreCase)))
            {
                t.Priority = priority++;
            }
        }
    }

    private string TerritoryKey()
    {
        try
        {
            var zoneId = zones.GetZone().ZoneId;
            // Outside Occult Crescent the provider returns Unknown. The catalog is shared by
            // both horns, so default the editor to SouthHorn — otherwise targets added in town
            // get keyed "Unknown" and silently disappear from both the list and the buyer.
            return zoneId == ZoneId.Unknown ? "SouthHorn" : zoneId.ToString();
        }
        catch
        {
            return "SouthHorn";
        }
    }
}
