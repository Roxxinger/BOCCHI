using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.Shopping;
using Dalamud.Bindings.ImGui;
using Ocelot.Config.Renderers;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using System.Reflection;

namespace BOCCHI.Common.Config.Renderers;

/// <summary>
///     Edits the structured shopping target list: Keep/Buy amounts, Keep Buying flag and
///     priority per target. Mirrors the FarmSpotList pattern.
/// </summary>
public sealed class ShopTargetListRenderer : IFieldRenderer<ShopTargetListAttribute>
{
    private int newItemId;

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

        for (int i = 0; i < targets.Count; i++)
        {
            CurrencyShopTarget t = targets[i];
            ImGui.PushID(i);

            string name = ShopCatalog.TryGet(t.ItemId, out var def) ? def.Name : $"Item {t.ItemId}";
            bool open = ImGui.CollapsingHeader($"{name}##target{i}");
            if (open)
            {
                changed |= DrawTarget(t);
                if (ImGui.Button(translator.T(fieldKey.Replace(".label", ".remove", StringComparison.Ordinal))))
                {
                    targets.RemoveAt(i);
                    changed = true;
                    ImGui.PopID();
                    break;
                }
            }

            ImGui.PopID();
        }

        // Add-row: catalog item id input.
        ImGui.TextUnformatted(translator.T(fieldKey.Replace(".label", ".add_hint", StringComparison.Ordinal)));
        ImGui.InputInt("##shop_new_item_id", ref newItemId);
        if (ImGui.Button(translator.T(fieldKey.Replace(".label", ".add", StringComparison.Ordinal))))
        {
            var id = (uint)Math.Max(0, newItemId);
            if (ShopCatalog.TryGet(id, out var def))
            {
                targets.Add(new CurrencyShopTarget
                {
                    ItemId = def.ItemId,
                    MenuIndex = (int)MenuIndexOf(def.ItemId),
                    TabId = TabIdOf(def.ItemId),
                    Priority = targets.Count == 0 ? 0 : targets.Max(x => x.Priority) + 1,
                });
                newItemId = 0;
                changed = true;
            }
        }

        if (changed)
        {
            prop.SetValue(target, targets);
        }

        return changed;
    }

    private static bool DrawTarget(CurrencyShopTarget t)
    {
        bool changed = false;

        int keep = t.KeepAmount;
        if (ImGui.InputInt(Label("keep"), ref keep) && keep >= 0 && keep != t.KeepAmount)
        {
            t.KeepAmount = keep;
            changed = true;
        }

        int buy = t.BuyAmount;
        if (ImGui.InputInt(Label("buy_amount"), ref buy) && buy >= 0 && buy != t.BuyAmount)
        {
            t.BuyAmount = buy;
            changed = true;
        }

        bool keepBuying = t.KeepBuying;
        if (ImGui.Checkbox(Label("keep_buying"), ref keepBuying) && keepBuying != t.KeepBuying)
        {
            t.KeepBuying = keepBuying;
            changed = true;
        }

        int priority = t.Priority;
        if (ImGui.InputInt(Label("priority"), ref priority) && priority != t.Priority)
        {
            t.Priority = priority;
            changed = true;
        }

        return changed;
    }

    // DrawTarget has no translator context; labels fall back to plain text suffixes which
    // the translator resolves for the default language. Kept simple like FarmSpotList's spot rows.
    private static string Label(string suffix) => suffix;

    private static uint MenuIndexOf(uint itemId)
    {
        foreach (var page in ShopCatalog.Pages)
        {
            foreach (var tab in page.Tabs)
            {
                if (tab.Items.Any(i => i.ItemId == itemId))
                {
                    return (uint)page.MenuIndex;
                }
            }
        }

        return 0;
    }

    private static int TabIdOf(uint itemId)
    {
        foreach (var page in ShopCatalog.Pages)
        {
            foreach (var tab in page.Tabs)
            {
                if (tab.Items.Any(i => i.ItemId == itemId))
                {
                    return tab.TabId;
                }
            }
        }

        return -1;
    }
}
