using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Ocelot.Services.Translation;
using Ocelot.Windows;
using XIVFate = Lumina.Excel.Sheets.Fate;

namespace BOCCHI.Common.UI;

/// <summary>
///     Local pot FATE countdown(s) from <see cref="IPotCycleTracker"/>.
///     South Horn and North Horn are tracked separately and stay visible after you leave the zone.
/// </summary>
public static class PotTimerUi
{
    public static void Draw(
        IPotCycleTracker potCycle,
        IZoneProvider zones,
        IDataManager data,
        ITranslator<MainWindow> translator)
    {
        ExcelSheet<XIVFate> sheet = data.GetExcelSheet<XIVFate>();
        IReadOnlyList<PotCycleSnapshot> known = potCycle.KnownCycles;

        if (known.Count == 0)
        {
            if (!zones.GetZone().IsOccultCrescentZone())
            {
                return;
            }

            BocchiUi.MutedText(translator.T(".pot_timer.unknown"));
            return;
        }

        foreach (PotCycleSnapshot snap in known)
        {
            DrawOne(snap, sheet, translator);
        }
    }

    /// <summary>One-line status text for all known zone cycles, or null when nothing to show.</summary>
    public static string? FormatCompact(
        IPotCycleTracker potCycle,
        IDataManager data,
        ITranslator<MainWindow> translator)
    {
        ExcelSheet<XIVFate> sheet = data.GetExcelSheet<XIVFate>();
        IReadOnlyList<PotCycleSnapshot> known = potCycle.KnownCycles;
        if (known.Count == 0)
        {
            return null;
        }

        List<string> parts = [];
        foreach (PotCycleSnapshot snap in known)
        {
            string zone = ZoneShort(snap.TerritoryTypeId, translator);
            if (snap.CurrentActivePotFateId != 0)
            {
                parts.Add($"{zone} {translator.T(".pot_timer.chip_active")}");
                continue;
            }

            if (!snap.HasPredictedNextPot)
            {
                continue;
            }

            parts.Add($"{zone} {FormatClock(Remaining(snap))}");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static void DrawOne(
        PotCycleSnapshot snap,
        ExcelSheet<XIVFate> sheet,
        ITranslator<MainWindow> translator)
    {
        string zone = ZoneLabel(snap.TerritoryTypeId, translator);

        if (snap.CurrentActivePotFateId != 0)
        {
            BocchiUi.LabelledValue(
                $"{zone} — {translator.T(".pot_timer.active")}",
                FateName(sheet, snap.CurrentActivePotFateId));
            return;
        }

        if (!snap.HasPredictedNextPot)
        {
            return;
        }

        BocchiUi.LabelledValue(
            $"{zone} — {translator.T(".pot_timer.next")}",
            $"{FateName(sheet, snap.PredictedNextPotFateId)} · {FormatClock(Remaining(snap))}");
    }

    private static TimeSpan Remaining(PotCycleSnapshot snap)
    {
        TimeSpan remaining = snap.PredictedNextSpawnAt - DateTimeOffset.UtcNow;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private static string ZoneLabel(ushort territoryTypeId, ITranslator<MainWindow> translator) =>
        (ZoneId)territoryTypeId switch
        {
            ZoneId.SouthHorn => translator.T(".pot_timer.south_horn"),
            ZoneId.NorthHorn => translator.T(".pot_timer.north_horn"),
            _ => $"#{territoryTypeId}",
        };

    private static string ZoneShort(ushort territoryTypeId, ITranslator<MainWindow> translator) =>
        (ZoneId)territoryTypeId switch
        {
            ZoneId.SouthHorn => translator.T(".pot_timer.south_horn_short"),
            ZoneId.NorthHorn => translator.T(".pot_timer.north_horn_short"),
            _ => $"#{territoryTypeId}",
        };

    private static string FateName(ExcelSheet<XIVFate> sheet, int fateId)
    {
        try
        {
            string name = sheet.GetRow((uint)fateId).Name.ToString();
            return string.IsNullOrWhiteSpace(name) ? $"#{fateId}" : name;
        }
        catch
        {
            return $"#{fateId}";
        }
    }

    private static string FormatClock(TimeSpan value)
    {
        if (value.TotalHours >= 1)
        {
            return $"{(int)value.TotalHours}:{value.Minutes:D2}:{value.Seconds:D2}";
        }

        return $"{value.Minutes:D2}:{value.Seconds:D2}";
    }
}
