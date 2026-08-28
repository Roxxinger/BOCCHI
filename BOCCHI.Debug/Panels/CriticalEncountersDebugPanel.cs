using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace BOCCHI.Debug.Panels;

public sealed class CriticalEncountersDebugPanel(ICriticalEncounterRepository criticalEncounters) : IDebugPanel
{
    public string Name => "Critical Encounters";

    public void Render()
    {
        foreach (CriticalEncounter encounter in criticalEncounters.Snapshot())
        {
            ActivitySnapshotRenderer.Render(
                encounter.Name,
                FormatState(encounter.State, encounter.Progress),
                ("Id", encounter.Id),
                ("Position", encounter.Position.ToString("f2")),
                ("Radius", encounter.Radius)
            );
        }
    }

    private static string FormatState(DynamicEventState state, byte progress)
    {
        return state switch
        {
            DynamicEventState.Inactive => "(Inactive)",
            DynamicEventState.Register => "(Preparing)",
            DynamicEventState.Warmup => "(Starting)",
            DynamicEventState.Battle => $"({progress}%)",
            var _ => $"({state})"
        };
    }
}
