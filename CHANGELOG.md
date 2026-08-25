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
