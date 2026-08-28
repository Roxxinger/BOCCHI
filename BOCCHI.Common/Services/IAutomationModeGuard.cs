namespace BOCCHI.Common.Services;

public enum AutomationMode
{
    None = 0,
    IllegalMode = 1,
    PotsAndTreasure = 2,
    MobFarmer = 3,
    TreasureHunt = 4,
    CarrotHunt = 5,
    Completionist = 6,
    Shopping = 7
}

/// <summary>Ensures only one primary automation mode runs at a time.</summary>
public interface IAutomationModeGuard
{
    /// <summary>Stop every other mode before starting <paramref name="mode"/>.</summary>
    void EnsureExclusive(AutomationMode mode);

    /// <summary>Resume Illegal Mode after a treasure hunt if it was soft-paused.</summary>
    void NotifyTreasureHuntEnded();

    /// <summary>Resume automation after Occult Crescent shopping finishes.</summary>
    void NotifyShoppingEnded();

    /// <summary>Stop all modes, buffs, pathfinding, and chains.</summary>
    void EmergencyStop();
}
