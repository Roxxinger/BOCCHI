# 4.2.0.4

### Treasure Hunt
- Starting at South Horn base again uses aethernet to the first pad instead of walking across the map
- Treasure Sight mid-route no longer times out when a mount is still finishing: waits through the mount animation, then dismounts and casts
- Approaching a coffer no longer pathfinds back and forth between the map pad and the live chest every tick
- Approach / open stops at interact range instead of walking into the chest when the mesh path ends inside it
- No longer re-queues the same pad move every tick when standing just outside the arrival radius

### Mob Farmer
- Treasure Sight restores your phantom job again if the cast fails or Phantom Action II is still on cooldown (for example after a buff run)
- Pull buffs no longer spam Counterstance every GCD when Fleetfooted does not stick