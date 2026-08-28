using Ocelot.Config.Renderers.Enum;

namespace BOCCHI.Common.Config;

/// <summary>How Illegal Mode repairs gear at base camp.</summary>
public enum AutoRepairMethod
{
    /// <summary>Repair general action (needs crafter levels + dark matter).</summary>
    SelfRepair = 0,

    /// <summary>Talk to a nearby mender NPC (no crafter required).</summary>
    MenderNpc = 1,

    /// <summary>Use a nearby mender when available; otherwise self-repair.</summary>
    PreferMender = 2,
}

public sealed class AutoRepairMethodDisplay : IEnumDisplay<AutoRepairMethod>
{
    public string Display(AutoRepairMethod value) => value switch
    {
        AutoRepairMethod.MenderNpc => "Mender NPC",
        AutoRepairMethod.PreferMender => "Prefer mender NPC",
        _ => "Self-repair",
    };
}
