using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using System.Numerics;

namespace BOCCHI.CriticalEncounters.Data;

public interface ICriticalEncounterFactory
{
    CriticalEncounter Create(DynamicEvent ev);
}

public class CriticalEncounterFactory(IZoneProvider zones, CriticalEncounterGeometry geometry) : ICriticalEncounterFactory
{
    public CriticalEncounter Create(DynamicEvent ev)
    {
        CriticalEncounterId id = new(ev.DynamicEventId);
        IZone zone = zones.GetZone();
        ActivityData? authored = zone.GetCriticalEncounterData()
            .FirstOrDefault(a => a.Id == ev.DynamicEventId);
        Vector3 fallback = authored?.Position ?? Vector3.NaN;
        ActivityAreaShape shape = authored?.AreaShape ?? ActivityAreaShape.Circle;

        CriticalEncounter created = new(id, ev, 0, fallback, shape);
        if (geometry.TryResolveForAuthored(
                ev.DynamicEventId,
                fallback,
                out _) is { Radius: > 0 } area)
        {
            shape = NavigationConstants.ResolveCriticalEncounterShape(authored, area.IsSquare);
            created.ApplyCombatGeometry(area.Radius, shape, area.Center, authored?.CombatRadius);
            zone.ApplyCriticalEncounterCombat(ev.DynamicEventId, created.UnpaddedCombatRadius, shape);
        }

        return created;
    }
}
