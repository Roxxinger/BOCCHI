using System.Globalization;
using Ocelot.Config.Renderers.Enum;
using Ocelot.Rotation.Services;

namespace BOCCHI.Common.Config;

public enum BossModOverdodge
{
    None,
    Small,
    Medium,
    Large,
}

public enum BossModMovementDelay
{
    None,
    Short,
    Long,
}

public class BossModOverdodgeDisplay : IEnumDisplay<BossModOverdodge>
{
    public string Display(BossModOverdodge value) => value switch
    {
        BossModOverdodge.Small => "Small",
        BossModOverdodge.Medium => "Medium",
        BossModOverdodge.Large => "Large",
        _ => "None",
    };
}

public static class BossModMovement
{
    public const float MinRange = 1.1f;

    public const float MaxRange = 30f;

    /// <summary>Sage — Phlegma / Dyskrasia need short range; OnHitbox is tighter than needed.</summary>
    public const uint SageJobId = 40;

    public const float SageRange = 5f;

    /// <summary>Dancer — Finish PBAoE needs mid range; OnHitbox / melee slider are too close.</summary>
    public const uint DancerJobId = 38;

    public const float DancerRange = 10f;

    public static BossModMovementSettings From(AutomatorConfig config, bool isMelee, uint? classJobId = null)
    {
        string range;
        if (!config.BossModMaxDistanceByRole)
        {
            range = FormatRange(config.BossModMaxDistance);
        }
        else if (TryJobOverrideRange(classJobId, out string jobRange))
        {
            range = jobRange;
        }
        else if (isMelee && config.BossModMeleeOnHitbox)
        {
            range = "OnHitbox";
        }
        else if (isMelee)
        {
            range = FormatRange(config.BossModMaxDistanceMelee);
        }
        else
        {
            range = FormatRange(config.BossModMaxDistanceRanged);
        }

        return new(
            range,
            config.BossModOverdodge.ToString(),
            config.BossModMovementDelay.ToString(),
            config.BossModSeparateDodgeDelay ? "Enabled" : "Disabled",
            config.BossModSeparateDodgeDelay ? config.BossModDodgeMovementDelay.ToString() : "None");
    }

    /// <summary>
    ///     Jobs treated as melee for closeness, but with a fixed standoff instead of OnHitbox /
    ///     the shared melee slider when distance-by-role is on.
    /// </summary>
    public static bool TryJobOverrideRange(uint? classJobId, out string range)
    {
        range = "";
        if (classJobId is not uint id)
        {
            return false;
        }

        if (id == SageJobId)
        {
            range = FormatRange(SageRange);
            return true;
        }

        if (id == DancerJobId)
        {
            range = FormatRange(DancerRange);
            return true;
        }

        return false;
    }

    public static string FormatRange(float yards)
    {
        float clamped = Math.Clamp(MathF.Round(yards, 1), MinRange, MaxRange);
        return clamped.ToString(CultureInfo.InvariantCulture);
    }
}
