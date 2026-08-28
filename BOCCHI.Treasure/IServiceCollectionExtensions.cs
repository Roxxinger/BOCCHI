using BOCCHI.Common;
using BOCCHI.Treasure.ChainRecipes;
using BOCCHI.Treasure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BOCCHI.Treasure;

public static class IServiceCollectionExtensions
{
    public static void LoadTreasureModule(this IServiceCollection services)
    {
        services.AddSingleton<CarrotLocationSyncService>();
        services.AddSingleton<CofferLocationSyncService>();
        services.AddSingleton<ITreasureTracker, TreasureTracker>();
        services.AddSingleton<ICarrotTracker, CarrotTracker>();
        services.AddSingleton<ITreasureHunter, TreasureHunterService>();
        services.AddSingleton<ICarrotHunter, CarrotHunterService>();
        services.AddSingleton<NinjaHideAssist>();
        services.AddSingleton<Func<ITreasureHunter>>(sp => () => sp.GetRequiredService<ITreasureHunter>());
        services.AddSingleton<Func<ICarrotHunter>>(sp => () => sp.GetRequiredService<ICarrotHunter>());
        services.AddSingleton<IDynamicRenderer, TreasureRenderer>();
        services.AddSingleton<TreasureRadarDrawer>();
        services.AddSingleton<OpenTreasureCofferChain>();
        services.AddSingleton<HuntTreasureSightChain>();
        services.AddSingleton<PandoraAutoOpenHold>();
    }
}
