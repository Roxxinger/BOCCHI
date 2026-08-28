using BOCCHI.Common.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace BOCCHI.Common.UI;

public static class ActivitySnapshotRenderer
{
    public static void RenderCompact(string title, string details)
    {
        ImGui.TextColored(BocchiUi.Header, title);
        ImGui.SameLine(0f, 6f);
        ImGui.TextColored(BocchiUi.Muted, details);
    }

    public static void RenderCompactWithActions(
        IActivityNavigation navigation,
        string title,
        string details,
        Vector3 destination,
        string actionId,
        bool includeTeleport = true
    )
    {
        RenderCompact(title, details);

        ImGui.Indent(12f);
        DrawActionButtons(navigation, destination, title, actionId, includeTeleport);
        ImGui.Unindent(12f);
    }

    public static void Render(
        string title,
        string? titleSuffix,
        params (string Label, object Value)[] fields
    )
    {
        ImGui.TextColored(BocchiUi.Header, title);

        if (!string.IsNullOrEmpty(titleSuffix))
        {
            ImGui.SameLine();
            BocchiUi.MutedText(titleSuffix);
        }

        ImGui.Indent(32);

        foreach ((var label, var value) in fields)
        {
            BocchiUi.LabelledValue(label, value);
        }

        ImGui.Unindent(32);
    }

    private static void DrawActionButtons(
        IActivityNavigation navigation,
        Vector3 destination,
        string title,
        string actionId,
        bool includeTeleport
    )
    {
        if (!navigation.CanPathfind)
        {
            return;
        }

        if (IconButton(FontAwesomeIcon.Running, $"path_{actionId}"))
        {
            navigation.PathTo(destination, title, actionId);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"Path to {title} (aethernet when at a shard, then walk)");
        }

        if (!includeTeleport)
        {
            return;
        }

        ImGui.SameLine(0f, 4f);

        bool canTeleport = navigation.CanTeleport(destination, out string? disabledReason);
        using (ImRaii.Disabled(!canTeleport))
        {
            if (IconButton(FontAwesomeIcon.LocationArrow, $"tp_{actionId}"))
            {
                navigation.TeleportToward(destination, title, actionId);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(canTeleport
                ? $"Teleport toward {title} (aethernet only — does not walk)"
                : disabledReason ?? "Teleport unavailable");
        }
    }

    private static bool IconButton(FontAwesomeIcon icon, string id)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            return ImGui.Button($"{icon.ToIconString()}##{id}");
        }
    }
}
