namespace BOCCHI.MobFarmer.Services;

/// <summary>
///     Tracks when the Mob Farmer main-window section last rendered so the scanner can idle
///     when the panel is collapsed and the farmer is stopped.
/// </summary>
public sealed class MobFarmerPanelState
{
    private static readonly TimeSpan VisibleGrace = TimeSpan.FromSeconds(2);

    private DateTime lastRenderedUtc = DateTime.MinValue;

    public void MarkRendered() => lastRenderedUtc = DateTime.UtcNow;

    public bool RecentlyVisible => DateTime.UtcNow - lastRenderedUtc <= VisibleGrace;
}
