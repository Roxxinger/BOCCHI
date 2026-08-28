using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Ocelot.Config.Renderers;
using Ocelot.Services.Translation;
using System.Reflection;
using XIVDynamicEvent = Lumina.Excel.Sheets.DynamicEvent;

namespace BOCCHI.Common.Config.Renderers;

public class DisabledCriticalEncounterIdsRenderer(IZoneProvider zones, IDataManager data)
    : IFieldRenderer<DisabledCriticalEncounterIdsAttribute>
{
    public bool Render(object target, PropertyInfo prop, DisabledCriticalEncounterIdsAttribute attr, Type owner, ITranslator translator)
    {
        ExcelSheet<XIVDynamicEvent> sheet = data.GetExcelSheet<XIVDynamicEvent>();
        List<ActivityData> criticalEncounters = zones.GetZone()
            .GetCriticalEncounterData()
            .OrderBy(ce => ce.Id)
            .ToList();

        return DisabledActivityIdsHelper.Render(
            target,
            prop,
            owner,
            translator,
            nameof(DisabledCriticalEncounterIdsRenderer),
            criticalEncounters,
            id =>
            {
                string name = sheet.GetRow(id).Name.ToString();
                return string.IsNullOrWhiteSpace(name) ? $"Critical Encounter #{id}" : name;
            });
    }
}
