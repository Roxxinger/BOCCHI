using Dalamud.Game.ClientState.Objects.Types;

namespace BOCCHI.MobFarmer.Services;

public interface IMobScanner
{
    IReadOnlyList<IBattleNpc> Mobs { get; }

    IReadOnlyList<IBattleNpc> InCombat { get; }

    IReadOnlyList<IBattleNpc> NotInCombat { get; }

    /// <summary>Selected enemies that have a target that is not the local player.</summary>
    IReadOnlyList<IBattleNpc> Contested { get; }

    void Update();
}
