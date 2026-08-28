using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.Mobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.UI;
using Dalamud.Plugin.Services;
using Ocelot.Config.Renderers;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using System.Reflection;

namespace BOCCHI.Common.Config.Renderers;

public class MobMultiSelectRenderer(IZoneProvider zones, IDataManager data, IUIService ui) : IFieldRenderer<MobMultiSelectAttribute>
{
    private string search = string.Empty;

    private int zoneFilterIndex = -1;

    private ZoneId lastZoneId = ZoneId.Unknown;

    public bool Render(object target, PropertyInfo prop, MobMultiSelectAttribute attr, Type owner, ITranslator translator)
    {
        if (prop.PropertyType != typeof(List<Mob>))
        {
            throw new InvalidOperationException(
                $"[MobMultiSelectRenderer] must be used on List<{nameof(Mob)}> properties. " +
                $"{prop.DeclaringType?.Name}.{prop.Name} is {prop.PropertyType.Name}.");
        }

        List<Mob> mobs = (List<Mob>?)prop.GetValue(target) ?? [];
        if (prop.GetValue(target) == null)
        {
            prop.SetValue(target, mobs);
        }

        BocchiUi.SectionTitle(prop.Label(owner, translator));
        prop.Tooltip(owner, translator);

        ZoneId currentZone = zones.GetZone().ZoneId;
        if (zoneFilterIndex < 0 || currentZone != lastZoneId)
        {
            zoneFilterIndex = MobPickerHelper.ZoneFilterIndexFor(currentZone);
            lastZoneId = currentZone;
        }

        string fieldKey = prop.GetFieldLabelKey(owner);
        string searchHintKey = fieldKey.Replace(".label", ".search_hint", StringComparison.Ordinal);
        string selectedKey = fieldKey.Replace(".label", ".selected", StringComparison.Ordinal);
        string zoneFilterKey = fieldKey.Replace(".label", ".zone_filter", StringComparison.Ordinal);
        string allZonesKey = fieldKey.Replace(".label", ".zone_all", StringComparison.Ordinal);
        string southHornKey = fieldKey.Replace(".label", ".zone_south_horn", StringComparison.Ordinal);
        string northHornKey = fieldKey.Replace(".label", ".zone_north_horn", StringComparison.Ordinal);

        BocchiUi.PushFieldStyle();
        try
        {
            bool changed = MobPickerHelper.Draw(
                mobs,
                data,
                ui,
                ref search,
                ref zoneFilterIndex,
                translator.T(searchHintKey),
                translator.T(selectedKey),
                translator.T(zoneFilterKey),
                translator.T(allZonesKey),
                translator.T(southHornKey),
                translator.T(northHornKey),
                "##config_mob_picker_list",
                220f);

            if (changed)
            {
                prop.SetValue(target, mobs);
            }

            return changed;
        }
        finally
        {
            BocchiUi.PopFieldStyle();
        }
    }
}
