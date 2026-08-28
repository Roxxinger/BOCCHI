using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;

namespace BOCCHI.Debug.Panels;

public sealed class FatesDebugPanel(IFateRepository fates) : IDebugPanel
{
    public string Name => "Fates";

    public void Render()
    {
        foreach (Fate fate in fates.Snapshot())
        {
            ActivitySnapshotRenderer.Render(
                fate.Name,
                null,
                ("Id", fate.Id),
                ("Position", fate.Position.ToString("f2")),
                ("State", fate.State),
                ("Progress", fate.Progress),
                ("Radius", fate.Radius)
            );
        }
    }
}
