using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Extensions;
using Ocelot.Ipc.Lifestream;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.Pathfinding;
using System.Numerics;

namespace BOCCHI.Common.Data.Aethernet;

public static class AetheryteApproach
{
    private static readonly TimeSpan ApproachTimeout = TimeSpan.FromSeconds(45);

    public static IChain BuildApproachChain(
        IChainFactory chains,
        IZone zone,
        IObjectTable objects,
        IPathfinder pathfinder,
        IVNavmeshIpc vnav,
        ILifestreamIpc lifestream,
        ICondition conditions,
        MovementConfig movement,
        string chainName,
        bool sprintEnabled = true)
    {
        if (objects.LocalPlayer is null)
        {
            return chains.Create(chainName)
                .Then(_ => StepResult.Failure("No local player."), $"{chainName}::NoPlayer");
        }

        // Position may have changed since compose.
        return chains.Create(chainName)
            .Then(_ =>
                {
                    pathfinder.Stop();
                    vnav.Stop();
                    return StepResult.Success();
                }, $"{chainName}::StopMovement")
            .Then((Func<IChainContext, Task<StepResult>>)(async ctx =>
                {
                    if (objects.LocalPlayer is not { } current)
                    {
                        return StepResult.Failure("No local player.");
                    }

                    // Use distance to the cyan ring, not Lifestream IPC alone.
                    if (zone.IsWithinLifestreamRange(current.Position))
                    {
                        return StepResult.Success();
                    }

                    AethernetData? nearest = zone.EnumerateAetherytes()
                        .OrderBy(aetheryte => current.Position.Distance2D(aetheryte.Position))
                        .FirstOrDefault();

                    if (nearest == null)
                    {
                        return StepResult.Failure("No aetheryte nearby.");
                    }

                    Vector3 target = nearest.GetCampStandOffPosition(current.Position);

                    SprintAssist.MaybeCast(sprintEnabled, zone.IsInBasecamp());
                    MaybeMountToward(zone, objects, conditions, movement, target);
                    StartApproachPath(pathfinder, target);

                    ChainResult result = await chains.Create($"{chainName}::CloseInWait")
                        .WaitUntil(
                            _ =>
                            {
                                if (objects.LocalPlayer is not { } p)
                                {
                                    return ValueTask.FromResult(false);
                                }

                                if (zone.IsWithinLifestreamRange(p.Position))
                                {
                                    pathfinder.Stop();
                                    vnav.Stop();
                                    return ValueTask.FromResult(true);
                                }

                                Vector3 retryTarget = nearest.GetCampStandOffPosition(p.Position);
                                MaybeMountToward(zone, objects, conditions, movement, retryTarget);

                                // Re-issue only if idle and still meaningfully short — not every tick
                                // when already parked on the stand-off tile (vnav Idle + same poly).
                                if (pathfinder.GetState() == PathfindingState.Idle
                                    && p.Position.Distance2D(retryTarget) > AethernetNavigation.PathfindArrivalRadius)
                                {
                                    StartApproachPath(pathfinder, retryTarget);
                                }

                                return ValueTask.FromResult(false);
                            },
                            ApproachTimeout,
                            TimeSpan.FromMilliseconds(150),
                            $"{chainName}::WaitInRange")
                        .ExecuteAsync(ctx);

                    pathfinder.Stop();
                    vnav.Stop();

                    if (result.IsCanceled)
                    {
                        return StepResult.Canceled();
                    }

                    if (objects.LocalPlayer is { } after
                        && zone.IsWithinLifestreamRange(after.Position))
                    {
                        return StepResult.Success();
                    }

                    return StepResult.Failure("Could not approach within Lifestream range.");
                }), $"{chainName}::CloseIn");
    }

    private static void MaybeMountToward(
        IZone zone,
        IObjectTable objects,
        ICondition conditions,
        MovementConfig movement,
        Vector3 target)
    {
        MountWait.TryCastIfNeeded(
            conditions,
            objects,
            target,
            movement.ShouldAutoMount,
            movement.PreferredMountId,
            zone.IsInBasecamp(),
            zone);
    }

    private static void StartApproachPath(IPathfinder pathfinder, Vector3 target)
    {
        pathfinder.PathfindAndMoveTo(new PathfinderConfig(target)
        {
            DistanceThreshold = AethernetNavigation.PathfindArrivalRadius,
            ShouldSnapToFloor = false,
        });
    }

    /// <summary>
    ///     Ready to open Lifestream: must be inside the magenta body ring.
    ///     Distance is authoritative — Lifestream IPC can be non-zero while still outside cyan.
    /// </summary>
    public static bool IsReadyForLifestream(IZone zone, ILifestreamIpc _, Vector3 position) =>
        zone.IsWithinLifestreamRange(position);

    /// <summary>
    ///     True when we have arrived at / are standing on this shard.
    ///     Wider than Lifestream interact range — post-TP landings and menu-open range
    ///     are often 4–10y from the crystal; a 3.5y check caused re-TP loops to the same id.
    /// </summary>
    public static bool IsAlreadyAtAetheryte(AethernetData? aetheryte, Vector3 position)
    {
        if (aetheryte == null)
        {
            return false;
        }

        const float arrivedRadius = 12f;
        if (position.Distance2D(aetheryte.Position) <= arrivedRadius)
        {
            return true;
        }

        Vector3 interact = aetheryte.GetInteractPosition();
        return position.Distance2D(interact) <= arrivedRadius;
    }

    /// <summary>Nearest authored aetheryte matches <paramref name="placeNameId"/> and we're close to it.</summary>
    public static bool IsAtPlaceName(IZone zone, uint placeNameId, Vector3 position)
    {
        if (IsAlreadyAtAetheryte(zone.FindAetheryte(placeNameId), position))
        {
            return true;
        }

        AethernetData? nearest = zone.EnumerateAetherytes()
            .OrderBy(aetheryte => position.Distance2D(aetheryte.Position))
            .FirstOrDefault();

        return nearest != null
               && nearest.Id == placeNameId
               && position.Distance2D(nearest.Position) <= 25f;
    }
}
