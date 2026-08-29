using BOCCHI.Automator.Data;

namespace BOCCHI.Automator.Services;

public interface IAutomator
{
    /// <summary>Illegal Mode is on (not Pots & Treasure / Completionist).</summary>
    bool Enabled { get; }

    /// <summary>Automator pipeline is active for Illegal Mode, Completionist, or Pots & Treasure.</summary>
    bool IsActive { get; }

    bool IsPotsAndTreasure { get; }

    bool IsCompletionist { get; }

    /// <summary>Treasure hunt owns vnav (Illegal/Completionist soft-pause or Pots & Treasure filler).</summary>
    bool SuspendedForTreasure { get; }

    /// <summary>Shopping owns vnav (Illegal/Completionist / Pots & Treasure soft-pause).</summary>
    bool SuspendedForShopping { get; }

    /// <summary>Illegal Mode is on (including while suspended for treasure).</summary>
    bool IsIllegalMode { get; }

    /// <summary>Suspend or resume the automator pipeline so treasure hunt can own vnav.</summary>
    void SetSuspendedForTreasure(bool suspended);

    /// <summary>Suspend or resume the automator pipeline so shopping can own vnav.</summary>
    void SetSuspendedForShopping(bool suspended);

    /// <summary>Stop current pathfinding without clearing goals or run mode (soft pause).</summary>
    void SoftStopPathfinding();

    AutomatorState? CurrentState { get; }

    void Toggle();

    void TogglePotsAndTreasure();

    void ToggleCompletionist();

    /// <summary>Drop the current route and replan from the player's position (keeps the goal).</summary>
    void RefreshPathfinding();

    /// <summary>
    ///     Delete this zone's saved path map and reload (bundled map, or a fresh vnav build if needed).
    /// </summary>
    void RebuildPathMap();

    void Render();
}