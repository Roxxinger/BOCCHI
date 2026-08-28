# 4.1.0.26

### Path Conflict Re-routing Improvements
- **Standby path pre-computed every frame** (not just during conflict check) — ready for instant swap
- **Seamless swap uses ** instead of  — no movement pause during handoff
- **Stale path detection** — if player moved >10m from where standby path was computed, it's discarded and recomputed
- **Reordered execution**: TickStandbyPath runs BEFORE CheckPathConflict so alternate route is ready on first conflict frame

### Config (user-adjustable in  → Movement → Conflict)
| Setting | Default | Effect |
|---------|---------|--------|
| PathConflictCheckIntervalSeconds | 3s | How often to scan for players on path |
| PathConflictDistanceThreshold | 5m | Max distance from any waypoint to count as 'on path' |
| PathConflictAheadThreshold | 2m | How much closer to dest other player must be to trigger

For busy hubs, consider: Interval=5s, Distance=8m, Ahead=3m.

# 4.1.0.25

### Bug Fix
- **Shopping matcher was never initialized** — `ShopPageMatcher` field was `readonly` but never assigned in constructor, causing `NullReferenceException` on every navigation tick. Fixed by adding `this.matcher = new ShopPageMatcher();`.

# 4.1.0.24

### Features
- **Seamless path-conflict re-routing** — while a route is active, one alternate route to the same destination is pre-computed in the background (pure `vnavmesh.Nav.Pathfind`, no movement impact). When a path conflict triggers, the bot now swaps instantly onto the standby route (`Path.Stop` + `Path.MoveTo` handoff) instead of stopping and recalculating from scratch — no more micro-stutters. If no standby is ready yet, it falls back to the old replan.

# 4.1.0.23

### Fixes
- **Opened the wrong shop page then aborted** — the initial page choice picked the first *affordable* menu entry instead of the page holding the configured targets. It now opens the first page that actually has an actionable target (priority order), so the run proceeds to buy instead of stopping with "No actionable targets".

# 4.1.0.22

### Fixes
- NullReferenceException spam in TickNavigate while the vendor menu was open: menu-entry selection now guards a missing addon / out-of-range entry index, and any unexpected navigation error stops the run with a clear status instead of erroring every frame.

# 4.1.0.21

### Fixes
- **Stuck at "Approaching vendor (3,9y)" forever** — the approach aimed at a point 2,5y from the vendor with a 1,5y stop range, so the closest it could ever get was ~4y, just outside the 3,25y interaction range. The approach now drives to the vendor itself and stops 2y away.

# 4.1.0.20

### Features
- New **Shopping** panel in the debug window (`/debug`): live phase + status, trigger reason, whether the vendor menu / shop window is detected (with live tab, currency and rows), purchase-controller state with the last result, and configured targets vs what the shop actually shows. The fastest way to see why a run is stuck.

# 4.1.0.19

### Fixes
- Shopping travel prefers the **Return spell** (Rückkehr) straight to base camp instead of walking to an aetheryte for a Lifestream hop. Falls back to the aethernet hop only while Return is on cooldown.
- At base camp with the vendor not yet visible it now waits and retries instead of aborting the run.

# 4.1.0.18

### Fixes
- Unchecking **Enable auto shopping** now aborts a run in progress immediately (it used to keep walking to the vendor until the run finished). Start() also refuses to begin while the toggle is off.

# 4.1.0.17

### Fixes
- Shopping runs **only while Illegal Mode is active** — no standalone triggering anymore.
- While shopping runs, Illegal Mode's pipeline is soft-paused (same contract as treasure hunts): it stops pathing/fighting for goals and no longer fights the shopping movement (the spinning-in-place bug). When the run finishes, Illegal Mode resumes exactly where it was.

# 4.1.0.16

### Fixes
- Shopping now **teleports to base camp** (Lifestream aethernet hop, same chain as Treasure Hunt) instead of running across the map when the vendor is out of reach.
- Vendor interaction sets the NPC as target before interacting — standing in front of the Antiquarian doing nothing should be gone.

# 4.1.0.15

### Fixes
- **Shopping never started** — the service was registered but nothing called Start(). Auto-shopping now kicks off by itself when the currency threshold is met and the player is idle.
- Auto-start now yields to other modes: Illegal Mode / Pots & Treasure / Mob Farmer / Treasure Hunter / Carrot Hunter running, combat or busy states all block the start (AOCCH parity).

# 4.1.0.14

### Fixes
- Shopping config: the page dropdown now only lists the shop pages of the horn you are currently in — South Horn shows the 5 piece pages, North Horn the 3 obol pages. No more scrolling past the other horn's catalog.

# 4.1.0.13

### Features
- Shopping catalog: **North Horn added** — all 3 obol pages from AOCCH's dataset: Silver Obol IL 780 armor (35 Phantom Vision pieces), Silver Obol Other (Tule set, soul shards, North Horn Riding Map, housing, materia, **Final Final Fixative**), and Gold Obol Exchange (Torna/Carwen set, soul shards, fixative). 98 items, costs and rows verified against AOCCH's OccultCrescentData.json.

# 4.1.0.12

### Fixes
- Shopping targets added outside the Occult Crescent (or with no zone loaded) were keyed "Unknown" — they vanished from the priority list and were never bought. Editor and buyer now fall back to SouthHorn outside the crescent, and existing "Unknown" entries migrate to SouthHorn on load.
- Catalog audit: all 5 shop pages verified byte-identical to AOCCH (110 items, costs and rows included) — nothing was actually missing from the catalog itself.

# 4.1.0.11

### Fixes
- Shopping config: page/tab/item dropdowns now keep their selection — the target editor renderer was rebuilt every frame because it wasn't registered in DI, so dropdown clicks snapped back instantly.

# 4.1.0.10

### Features
- Shopping config page rebuilt AOCCH-style: add items via **page → tab → item dropdowns** (full catalog browse), then manage a **priority table** with move up/down ordering, per-item **Keep** / **Buy** inputs, single-slot **Keep Buying**, and remove.
- Currency table with per-currency **Reserved** (never spend below) and **Threshold** (auto-start trigger) inputs for the current territory.

# 4.1.0.9

### Features
- Shopping: full automatic Antiquarian currency shopping (AOCCH parity). New **Shopping** config page with structured targets — per item Keep amount, Buy amount, Keep Buying and priority; evaluated Keep → Buy → Keep Buying in priority order.
- Shopping: travels to the Expedition Antiquarian on its own when a currency threshold is met (per territory + currency), opens the right vendor menu entry and tab — verified live against the shop UI before buying.
- Shopping: per-territory currency reserves (never spend below) and start thresholds. Purchases are verified via inventory changes; game-log failures are classified so a blocked item is skipped instead of blocking the run.

# 4.1.0.7

### Movement
- Jump-when-stuck stops after a few failed hops and cancels pathing instead of jumping forever
- Auto-buff walk-in no longer loops pathfinding next to a knowledge crystal (vnav “Queueing move-to … within 1y” spam)

### Carrot Hunt
- Pads on a higher shelf no longer take a direct cliff walk (Return / aethernet instead)

### Mob Farmer
- Treasure Sight at the farm spot casts on its interval even with no Spots list and no prior Sight reading

### BossMod autorotation
- Phantom Chemist / WHM raises are on in BOCCHI AR presets
- Healer AI: Swiftcast raises (any dead player), heal, esuna, stay near party, and OOC tank predictive heals — Update presets if auto-update is off
