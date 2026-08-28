using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.UI;
using Dalamud.Bindings.ImGui;
using Ocelot.Config.Renderers;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using System.Reflection;

namespace BOCCHI.Common.Config.Renderers;

public sealed class TriageRaiseJobRenderer : IFieldRenderer<TriageRaiseJobAttribute>
{
    private const string ChemistKey = "config.automator.fields.preferred_triage_raise_job.chemist";

    private const string WhiteMageKey = "config.automator.fields.preferred_triage_raise_job.white_mage";

    public bool Render(object target, PropertyInfo prop, TriageRaiseJobAttribute attr, Type owner, ITranslator translator)
    {
        if (prop.PropertyType != typeof(TriageRaiseJobPreference))
        {
            throw new InvalidOperationException(
                $"[TriageRaiseJobRenderer] must be used on {nameof(TriageRaiseJobPreference)} properties. "
                + $"{prop.DeclaringType?.Name}.{prop.Name} is {prop.PropertyType.Name}.");
        }

        var value = (TriageRaiseJobPreference)(prop.GetValue(target) ?? TriageRaiseJobPreference.PhantomChemist);
        bool changed = false;

        BocchiUi.PushFieldStyle();
        try
        {
            if (ImGui.RadioButton(translator.T(ChemistKey), value == TriageRaiseJobPreference.PhantomChemist))
            {
                value = TriageRaiseJobPreference.PhantomChemist;
                changed = true;
            }

            prop.Tooltip(owner, translator);

            ImGui.SameLine();
            if (ImGui.RadioButton(translator.T(WhiteMageKey), value == TriageRaiseJobPreference.PhantomWhiteMage))
            {
                value = TriageRaiseJobPreference.PhantomWhiteMage;
                changed = true;
            }

            prop.Tooltip(owner, translator);
        }
        finally
        {
            BocchiUi.PopFieldStyle();
        }

        if (changed)
        {
            prop.SetValue(target, value);
        }

        return changed;
    }
}
