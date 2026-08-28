using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Ipc.RotationSolverReborn;
using Ocelot.Rotation.Services;
using Ocelot.Services.PlayerState;
using Ocelot.Services.PluginStatus;

namespace BOCCHI.MobFarmer.Services;

/// <summary>
///     Mob Farmer combat session: same backends as Illegal Mode, but never FATE/CE-syncs.
///     Rotation is armed only while Fighting.
/// </summary>
public sealed class FarmerCombatController(
    ICombatRotationSession session,
    AutomatorConfig automatorConfig,
    UIConfig uiConfig,
    IPlayer player,
    IChatGui chat,
    IPluginStatus pluginStatus,
    IRotationSolverRebornIpc rsr,
    ISupportJobFactory supportJobs
) : IFarmerCombatController
{
    public void Prepare()
    {
        session.OverwriteBossModPresets = automatorConfig.UpdateBossModPresetsAutomatically;
        session.MovementSettings = BossModMovement.From(automatorConfig, player.IsMelee(), player.GetClassJob()?.RowId);
        if (!automatorConfig.CombatAutorotation.UsesCombatAutomation() || !ValidatePluginsForConfig())
        {
            return;
        }

        session.Prepare(ToRecipe(automatorConfig.CombatAutorotation));
        session.Disable();
    }

    public void EnableFighting() => session.Enable(CombatActivity.MobFarm);

    public void Disable() => session.Disable();

    public void Tick()
    {
        session.OverwriteBossModPresets = automatorConfig.UpdateBossModPresetsAutomatically;
        session.MovementSettings = BossModMovement.From(automatorConfig, player.IsMelee(), player.GetClassJob()?.RowId);
        session.Tick(supportJobs.TryGetCurrent(out SupportJob current) ? current.Id.RowId() : null);
    }

    public void Teardown() => session.Teardown();

    private static CombatRotationRecipe ToRecipe(CombatAutorotation value) => value switch
    {
        CombatAutorotation.WrathCombo => new(
            JobRotationBackendKind.Wrath,
            CombatAiKind.MiscAi,
            ManualTargeting: true),
        CombatAutorotation.RotationSolverReborn => new(
            JobRotationBackendKind.RotationSolverReborn,
            CombatAiKind.MiscAi,
            ManualTargeting: true),
        CombatAutorotation.BossMod => new(JobRotationBackendKind.BossMod, CombatAiKind.None),
        CombatAutorotation.BossModReborn => new(JobRotationBackendKind.BossModReborn, CombatAiKind.None),
        _ => CombatRotationRecipe.None,
    };

    private bool ValidatePluginsForConfig()
    {
        switch (automatorConfig.CombatAutorotation)
        {
            case CombatAutorotation.WrathCombo:
                if (!pluginStatus.IsLoaded(JobRotationBackendKeys.Wrath))
                {
                    PrintJobProviderMissing("Wrath Combo");
                    return false;
                }

                WarnIfBossModMissing();
                return true;

            case CombatAutorotation.RotationSolverReborn:
                if (!CombatPluginPresence.RotationSolverReborn(pluginStatus, rsr))
                {
                    PrintJobProviderMissing("Rotation Solver Reborn");
                    return false;
                }

                WarnIfBossModMissing();
                return true;

            case CombatAutorotation.BossMod:
                return ValidateBossModFork(
                    required: JobRotationBackendKeys.BossMod,
                    other: JobRotationBackendKeys.BossModReborn,
                    requiredLabel: "BossMod",
                    otherLabel: "BossMod Reborn");

            case CombatAutorotation.BossModReborn:
                return ValidateBossModFork(
                    required: JobRotationBackendKeys.BossModReborn,
                    other: JobRotationBackendKeys.BossMod,
                    requiredLabel: "BossMod Reborn",
                    otherLabel: "BossMod");

            default:
                return false;
        }
    }

    private bool ValidateBossModFork(string required, string other, string requiredLabel, string otherLabel)
    {
        if (pluginStatus.IsLoaded(required))
        {
            return true;
        }

        if (pluginStatus.IsLoaded(other))
        {
            BocchiChat.PrintError(
                chat,
                uiConfig,
                $"Combat autorotation is set to {requiredLabel}, but only {otherLabel} is loaded.");
        }
        else
        {
            PrintJobProviderMissing(requiredLabel);
        }

        return false;
    }

    private void WarnIfBossModMissing()
    {
        if (!pluginStatus.IsLoaded(JobRotationBackendKeys.BossMod)
            && !pluginStatus.IsLoaded(JobRotationBackendKeys.BossModReborn))
        {
            var job = player.GetClassJob();
            BocchiChat.PrintError(
                chat,
                uiConfig,
                $"BOCCHI AI / BossMod autorotation not ready (is BossMod / BMR loaded?). "
                + $"job={job?.Abbreviation.ToString() ?? "?"} melee={player.IsMelee()}");
        }
    }

    private void PrintJobProviderMissing(string name)
    {
        BocchiChat.PrintError(
            chat,
            uiConfig,
            $"Combat autorotation needs {name}, but that plugin is not loaded.");
    }
}
