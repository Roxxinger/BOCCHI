# 4.1.0.7

### Features
- Mob Farmer: dedicated BossMod presets **BOCCHI AR MOB** / **BOCCHI AI MOB** so pack farming uses open-world targeting instead of FATE AR.
- Full AR stock presets (**BOCCHI AR FATE/CE/MOB**) enable job AOE by default, with optimized Veyn WAR tracks. Recreate presets or use Update presets under Combat.
- Mob Farmer pull buffs: optional **Counterstance** (Fleetfooted), applied late so it covers the start of the pull.

### Fixes
- Mob Farmer: auto-target only while pulling; once fighting with combat AI on, targeting is left to the AI.
- Mob Farmer: Ringing Respite waits out the shared cooldown after Quickstep instead of skipping.
- Dying in a CE no longer drops the encounter and resumes travel before you accept a raise.
- Pot chests: long / second-chance hops use Return + aethernet when the destination is off the path map; after opening a pot chest, only second-chance pads are searched.
- Wrath: recreates the lease after a job change (no more Invalid lease spam leaving autorotation off); only Occult Elixir is force-disabled; only Healer Targeting Mode stays locked.
- Auto treasure hunt without Treasure Sight pauses the map route for a FATE/CE, then resumes the same hunt afterward.
- Eternal Watch-class CEs no longer use absurd combat radii that stop approach on the wrong wall.
- Pot cycle sync only uploads when the pot or spawn time changes.
- Triage / phantom job swaps wait a few seconds after combat before changing jobs, so Chemist / White Mage are less often rejected with "unable to change phantom jobs".
