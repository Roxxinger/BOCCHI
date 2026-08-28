using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.MobFarmer;
using BOCCHI.Common.UI;
using Dalamud.Bindings.ImGui;
using Ocelot.Config.Renderers;
using Ocelot.Extensions;
using Ocelot.Services.PlayerState;
using Ocelot.Services.Translation;
using System.Numerics;
using System.Reflection;

namespace BOCCHI.Common.Config.Renderers;

public sealed class FarmSpotListRenderer(IPlayer player) : IFieldRenderer<FarmSpotListAttribute>
{
    public bool Render(object target, PropertyInfo prop, FarmSpotListAttribute attr, Type owner, ITranslator translator)
    {
        if (prop.PropertyType != typeof(List<FarmSpot>))
        {
            throw new InvalidOperationException(
                $"[FarmSpotListRenderer] must be used on List<{nameof(FarmSpot)}> properties. "
                + $"{prop.DeclaringType?.Name}.{prop.Name} is {prop.PropertyType.Name}.");
        }

        List<FarmSpot> spots = (List<FarmSpot>?)prop.GetValue(target) ?? [];
        if (prop.GetValue(target) == null)
        {
            prop.SetValue(target, spots);
        }

        string fieldKey = prop.GetFieldLabelKey(owner);
        BocchiUi.SectionTitle(translator.T(fieldKey));
        prop.Tooltip(owner, translator);

        bool changed = false;
        BocchiUi.PushFieldStyle();
        try
        {
            if (spots.Count == 0)
            {
                BocchiUi.MutedText(T(translator, fieldKey, "empty"));
            }

            for (int i = 0; i < spots.Count; i++)
            {
                FarmSpot spot = spots[i];
                ImGui.PushID(i);
                if (ImGui.CollapsingHeader($"{spot.Name}##spot{i}"))
                {
                    changed |= DrawSpot(spot, translator, fieldKey);
                    if (ImGui.Button(T(translator, fieldKey, "remove")))
                    {
                        spots.RemoveAt(i);
                        changed = true;
                        ImGui.PopID();
                        break;
                    }
                }

                ImGui.PopID();
            }

            if (ImGui.Button(T(translator, fieldKey, "add")))
            {
                FarmSpot spot = new()
                {
                    Name = $"Spot {spots.Count + 1}",
                    Priority = spots.Count == 0 ? 0 : spots.Max(s => s.Priority) + 1,
                };
                spot.SetOrigin(player.Position);
                spots.Add(spot);
                changed = true;
            }
        }
        finally
        {
            BocchiUi.PopFieldStyle();
        }

        if (changed)
        {
            prop.SetValue(target, spots);
        }

        return changed;
    }

    private bool DrawSpot(FarmSpot spot, ITranslator translator, string fieldKey)
    {
        bool changed = false;
        string name = spot.Name;
        if (ImGui.InputText(T(translator, fieldKey, "name"), ref name, 64) && name != spot.Name)
        {
            spot.Name = string.IsNullOrWhiteSpace(name) ? "Spot" : name;
            changed = true;
        }

        bool enabled = spot.Enabled;
        if (ImGui.Checkbox(T(translator, fieldKey, "enabled"), ref enabled) && enabled != spot.Enabled)
        {
            spot.Enabled = enabled;
            changed = true;
        }

        int priority = spot.Priority;
        if (ImGui.InputInt(T(translator, fieldKey, "priority"), ref priority) && priority != spot.Priority)
        {
            spot.Priority = priority;
            changed = true;
        }

        BocchiUi.MutedText($"{T(translator, fieldKey, "origin")}: {Format(spot.Origin)}");
        if (ImGui.Button(T(translator, fieldKey, "set_origin")))
        {
            spot.SetOrigin(player.Position);
            changed = true;
        }

        bool useStack = spot.UseStackPoint;
        if (ImGui.Checkbox(T(translator, fieldKey, "use_stack"), ref useStack) && useStack != spot.UseStackPoint)
        {
            spot.UseStackPoint = useStack;
            if (useStack && spot.StackPoint is null)
            {
                spot.SetStackPoint(player.Position);
            }

            changed = true;
        }

        if (spot.UseStackPoint)
        {
            BocchiUi.MutedText($"{T(translator, fieldKey, "stack")}: {Format(spot.StackPoint ?? Vector3.Zero)}");
            if (ImGui.Button(T(translator, fieldKey, "set_stack")))
            {
                spot.SetStackPoint(player.Position);
                changed = true;
            }
        }

        int minFight = spot.MinimumMobsToStartFight;
        if (ImGui.InputInt(T(translator, fieldKey, "min_fight"), ref minFight))
        {
            minFight = Math.Clamp(minFight, 0, 20);
            if (minFight != spot.MinimumMobsToStartFight)
            {
                spot.MinimumMobsToStartFight = minFight;
                changed = true;
            }
        }

        return changed;
    }

    private static string Format(Vector3 p) => $"{p.X:0.0}, {p.Y:0.0}, {p.Z:0.0}";

    private static string T(ITranslator translator, string fieldKey, string suffix)
    {
        string key = fieldKey.Replace(".label", $".{suffix}", StringComparison.Ordinal);
        return translator.T(key);
    }
}
