using System.Numerics;
using BOCCHI.Common.Data.Zones;

namespace BOCCHI.Common.Data.CriticalEncounters;

/// <summary>
///     Manual walk vias for CEs where the registration centre sits on a separate navmesh island
///     (Eternal Watch platform at Y~122 vs Eldergrowth at Y~108).
/// </summary>
public static class CriticalEncounterPathOverrides
{
    /// <summary>Same floor guard as treasure pad snap — do not insert vias when already on the CE storey.</summary>
    private const float VerticalDisconnectThreshold = 25f;

    private static readonly Dictionary<(ZoneId Zone, int CeId), Vector3[]> ApproachVias = new()
    {
        // Eternal Watch — Eldergrowth-side approach before the platform stand.
        [(ZoneId.SouthHorn, 46)] =
        [
            new(606.4641f, 108.07402f, 184.8517f),
        ],
    };

    /// <summary>
    ///     Ordered vias to insert before the final CE pathfind when the player is on a higher mesh.
    /// </summary>
    public static bool TryGetApproachVias(
        ZoneId zone,
        int ceId,
        Vector3 player,
        Vector3 ceStaging,
        out IReadOnlyList<Vector3> vias)
    {
        if (MathF.Abs(player.Y - ceStaging.Y) <= VerticalDisconnectThreshold
            || !ApproachVias.TryGetValue((zone, ceId), out Vector3[]? points))
        {
            vias = [];
            return false;
        }

        vias = points;
        return true;
    }
}
