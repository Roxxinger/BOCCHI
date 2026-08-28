using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Ocelot.Config.Renderers;
using Ocelot.Services.Translation;
using System.Reflection;
using XIVFate = Lumina.Excel.Sheets.Fate;

namespace BOCCHI.Common.Config.Renderers;

public class DisabledFateIdsRenderer(
    IZoneProvider zones,
    IDataManager data,
    AutomatorConfig automator
) : IFieldRenderer<DisabledFateIdsAttribute>
{
    public bool Render(object target, PropertyInfo prop, DisabledFateIdsAttribute attr, Type owner, ITranslator translator)
    {
        ExcelSheet<XIVFate> fateSheet = data.GetExcelSheet<XIVFate>();
        IZone zone = zones.GetZone();
        List<ActivityData> fates = zone.GetNormalFateData()
            .Concat(zone.GetPotFateData())
            .OrderBy(f => f.Id)
            .ToList();

        string? ForcedOnReason(uint id)
        {
            if (!automator.PreferPotFates || !zone.IsPotFate((int)id))
            {
                return null;
            }

            return translator.T("config.fates.fields.disabled_fate_ids.forced_by_prefer");
        }

        string? note = automator.PreferPotFates
            ? translator.T("config.fates.fields.disabled_fate_ids.forced_note")
            : null;
        string? suffix = automator.PreferPotFates
            ? translator.T("config.fates.fields.disabled_fate_ids.forced_suffix")
            : null;

        return DisabledActivityIdsHelper.Render(
            target,
            prop,
            owner,
            translator,
            nameof(DisabledFateIdsRenderer),
            fates,
            id => fateSheet.GetRow(id).Name.ToString(),
            ForcedOnReason,
            note,
            suffix);
    }
}
