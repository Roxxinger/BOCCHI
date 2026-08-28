using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.EventDrops;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using Dalamud.Bindings.ImGui;
using Ocelot.Services.Translation;
using Ocelot.Windows;

namespace BOCCHI.Fates;

public class FatesRenderer
(
    IFateRepository fates,
    IFateScorer fateScorer,
    IActivityNavigation navigation,
    IZoneProvider zones,
    UIConfig uiConfig,
    EventDropIconRenderer eventDrops,
    ITranslator<MainWindow> translator
) : IDynamicRenderer
{
    public MainWindowSection Section => MainWindowSection.World;

    public string? SubsectionTitle => translator.T(".world.fates.title");

    public void Render()
    {
        List<Fate> snapshots = fates.Snapshot().ToList();
        if (snapshots.Count == 0)
        {
            BocchiUi.MutedText(translator.T(".world.fates.none"));
            return;
        }

        ZoneId zoneId = zones.GetZone().ZoneId;
        bool showDrops = zones.GetZone().IsOccultCrescentZone() && uiConfig.AnyEventDropsEnabled;
        float dropExtra = EventDropIconRenderer.ListRowExtra(showDrops);
        float maxHeight = EventDropIconRenderer.ListMaxHeight(showDrops);

        using ImGuiSectionHelper.BoundedListScope list =
            ImGuiSectionHelper.BoundedList("##fates_list", snapshots.Count, maxHeight, dropExtra);
        if (!list.IsOpen)
        {
            return;
        }

        foreach (Fate fate in snapshots)
        {
            FateScore score = fateScorer.Score(fate);
            string details =
                $"Score {score:F1} · {fate.State} {fate.Progress}% · #{fate.Id.Value} · {fate.Position:f0} · r{fate.Radius}";

            ActivitySnapshotRenderer.RenderCompactWithActions(
                navigation,
                fate.Name,
                details,
                fate.Position,
                $"fate_{fate.Id.Value}");

            if (fate.Progress is > 0 and < 100)
            {
                BocchiUi.DrawPercentBar(
                    fate.Progress / 100f,
                    Math.Min(180f, ImGui.GetContentRegionAvail().X),
                    $"{fate.Progress}%");
            }

            if (FieldNoteTargets.TryGetDropsForFate(zoneId, fate.Id.Value, out EventDropInfo drops))
            {
                eventDrops.Render(fate.Id.Value, drops);
            }
        }
    }

    public bool ShouldRender() => uiConfig.ShowWorldSection;
}
