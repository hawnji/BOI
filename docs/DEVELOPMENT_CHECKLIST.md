# Development checklist

Status values (from the master spec): **Not started**, **In progress**, **Implemented**, **Integrated**, **Tested**, **Complete**.

- *Implemented* = the code exists and works in isolation. "(unit-tested)" means it is covered by passing headless NUnit tests.
- *Integrated* = wired into real gameplay in Unity scenes.
- *Tested* = verified in play (the dedicated QA phase), *Complete* = done including polish.
- Nothing is marked Integrated, Tested or Complete until it has been run in the Unity editor. **As of the last update
  the Unity editor has not been available to the development environment**, so no Unity-side code has been compiled
  by Unity or run.

Last updated: 2026-11-22.

## Phase status

| Phase | Status | Notes |
|---|---|---|
| 1 Project foundation | In progress | Core architecture, item engine, procgen, saves, input abstraction, content pipeline, editor tools written; core passes 172 headless tests. Pending: open in Unity 6000.6.9f1, confirm package versions and URP 2D renderer assets, compile Runtime/Editor in Unity. |
| 2 Core player gameplay | In progress | Full gameplay loop runs headless (ADR-009): movement, shooting, enemies, bombs, pickups, doors, rooms, floors, boss exit; unit-tested incl. determinism and random-play soak. Unity presentation layer written (placeholder art, IMGUI HUD), not yet compiled in Unity. |
| 3 Combat and interaction foundations | In progress | Pure parts exist and are unit-tested: damage info, status controller, health pool, enemy AI brain, attack patterns, difficulty scaling. Unity enemy components not started. |
| 4 Item interaction engine | In progress | Engine core done early (spec: "design early"). Remaining: integration with real projectiles/companions/enemies, in-game debug overlay. |
| 5 Procedural generation | In progress | Generator core done early. Remaining: full template library (250), room instantiation in Unity, editor template editor. |
| 6 Enemies and bosses | In progress | Boss phase controller and data format implemented and unit-tested; 2 bosses + 1 miniboss authored. Unity boss arenas, health bar and intros not started. |
| 7 Content expansion | Not started | |
| 8 Progression and game modes | In progress | Characters, achievements, unlocks, completion marks, challenges, daily/practice/custom modes, run recording into the profile: implemented and unit-tested. Placeholder IMGUI front end (controller-navigable) type-checked only. Endings not started. |
| 9 Art and presentation | Not started | |
| 10 Integration completion | Not started | |
| 11 QA and balancing | Not started | |
| 12 Release preparation | Not started | |

## Master feature checklist (systems)

| System | Status | Location / notes |
|---|---|---|
| Unity project version pin 6000.6.9f1 | Implemented | `ProjectSettings/ProjectVersion.txt` |
| Package manifest (URP, Input System, 2D, Newtonsoft, Test Framework) | In progress | Versions unverified without the editor (ADR-008) |
| URP 2D renderer + pipeline assets | Not started | Must be created in the editor (Universal 2D template or Create > Rendering > URP 2D) |
| Folder structure, assemblies, conventions | Implemented | `docs/CONVENTIONS.md` |
| Bootstrap and service hub | Implemented | `Runtime/Bootstrap`; type-checked against stubs only |
| Event bus | Implemented (unit-tested) | duplicate/recursion/exception guards |
| Seeded RNG, run seeds, daily seed | Implemented (unit-tested) | `Core/Rng` |
| Stat system (additive, percent, multiplicative, caps, floors/ceilings, overrides, conditional, dynamic) | Implemented (unit-tested) | `Core/Stats` |
| Health (Vigor/Ward/Umbral, invulnerability, defense, lethal-hit prevention, death) | Implemented (unit-tested) | `Core/Combat/HealthPool.cs` |
| Status effects (DoT, slow, freeze, vulnerability, stacking modes, cancels, boss immunity) | Implemented (unit-tested) | `Core/Combat/StatusEffects.cs` |
| Projectile spec (behaviours, delivery priority, generation limit, sanitizing) | Implemented (unit-tested) | Runtime projectile simulation not started |
| Item interaction engine (hooks, pipeline, stacking, conditions, synergies, suppression, transformations, safeguards, snapshot) | Implemented (unit-tested) | `Core/Items` |
| Built-in effect library | Implemented (partly unit-tested) | 60 effect types; not all individually tested yet |
| Active item charge/use/swap | Implemented (unit-tested) | Rooms / seconds / kills / one-shot |
| Trinket slots | Implemented (unit-tested) | |
| Companion hooks (spawn per stack, inherit player shots, boost) and companion AI (8 behaviours: shooter, mimic, orbital, chaser, collector, bomber, provider, guardian) | Implemented (headless-tested, CompanionTests) | Companion sprites are placeholder shapes; not seen in Unity |
| Room template model + validator + duplicate detection | Implemented (unit-tested) | |
| Floor graph, room types, secrets, loops, door kinds | Implemented (unit-tested) | 300 seeds x 2 floors validated in tests |
| Template selection with anti-repetition | Implemented (unit-tested) | |
| Encounter population (budget, compositions, elites, bosses) | Implemented (unit-tested) | Pool ids are validated against enemy/boss definitions |
| Floor validator | Implemented (unit-tested) | |
| Save system (envelope, checksum, backups, recovery, migration, schema guard) | Implemented (unit-tested) | |
| Desktop file storage (atomic writes) | Implemented | type-checked only |
| Settings model (audio, graphics, accessibility, input) | Implemented | Settings UI not started |
| Input abstraction + shoot modes | Implemented (unit-tested) | |
| Input System bindings (KB/M + gamepad), last-used device | Implemented | type-checked only |
| Rebinding + persistence | Implemented | type-checked only; UI not started |
| Content loading + cross-validation | Implemented (unit-tested) | |
| Editor: content validation menu | Implemented | type-checked only |
| Editor: floor previewer + batch seed tester | Implemented | type-checked only |
| Editor: synergy debugger (live + sandbox) | Implemented | type-checked only |
| Editor: room template editor | Not started | Templates are text; previewer shows them |
| Platform services abstraction | Implemented | |
| Movement model (8-dir/analog, accel/decel, turn boost, knockback, roots, input locks, surfaces) | Implemented (unit-tested) | `Core/Movement` |
| Fire controller (fire rate, charge shots, volley spread) | Implemented (unit-tested) | `Core/Combat/FireController.cs` |
| Projectile simulation (homing, accel, wavy, boomerang, lobbed, range, pierce, bounce, spectral, split, chain, explode) | Implemented (unit-tested) | `Core/Combat/ProjectileSimulation.cs` |
| Floor/room state (combat door locks, keys, secret reveal, challenge/curse doors, map reveal) | Implemented (unit-tested) | `Core/Rooms/RoomState.cs` |
| Room simulation (tile collision, player move/fire/bomb/active, enemy AI execution, shots, explosions, destructible tiles, hazards, pickups, doorways) | Implemented (unit-tested) | `Core/Simulation/RoomSimulation.cs`, ADR-009 |
| Run simulation (floor sequence, transitions, room population, treasure/shop/secret/healing/reward rooms, clear rewards, boss exit, victory/defeat, teleports, companions) | Implemented (unit-tested) | `Core/Simulation/RunSimulation.cs`; consumables granted when content defines them |
| Unity presentation (room texture, pooled actor sprites, camera follow + shake, placeholder art/audio hooks, IMGUI HUD with health/resources/active/minimap/boss bar/pause/run end, auto-start run) | Implemented (type-checked only) | `Runtime/Presentation`; not compiled or run in Unity |
| Player controller, projectile objects, camera, room instantiation, door transitions (Unity) | Not started | Phase 2 Unity side |
| Enemy framework and AI (data-driven movement/attacks, telegraphs, cooldowns, waves, dash, summon caps, retreat, aggro, difficulty gating, deterministic) | Implemented (unit-tested) | `Core/Enemies`; Unity enemy component not started |
| Bosses / minibosses (ordered phase transitions, transition invulnerability, on-enter actions) | Implemented (unit-tested) | `Core/Enemies/BossController.cs` |
| Item pools (seeded, rarity/quality/luck weighted, unlock-aware, no repeats per run) | Implemented (unit-tested) | `Core/Economy/ItemPools.cs` |
| Shop inventory generation, pricing, discounts, purchases | Implemented (unit-tested) | `Core/Economy/ShopInventory.cs`; shop room/UI not started |
| Drop tables (room clear, enemy, prop, chest; luck and item modifiers) | Implemented (unit-tested) | `Core/Economy/DropTables.cs` |
| Pickups, bombs, consumables (sigils + unidentified draughts with per-run colours, slots, swap) | Implemented (unit-tested in Core; Unity drawing type-checked only) | `Core/Consumables`, `RunSimulation.Consumables.cs` |
| Floor hexes (curses), shrine events | Implemented (unit-tested) | 5 hexes (map hide, larger floor, veiled pedestals, darkness, stat), 4 shrines with costs/outcomes/ambush |
| Room modifiers (elite guard, frenzy, iron hide, swarming, volatile, mending, darkness, giants, bounty, glass ranks) | Implemented (headless-tested, RoomModifierTests) | Rolled per combat room from the difficulty's chance (+1% per floor), deterministic per seed; HUD popup and darkness hook type-checked only |
| Difficulty system | In progress | Story/Normal/Hard/Nightmare presets drive enemy scaling, attack gating, damage bonus and hex chance; no selection UI yet |
| Game modes (standard, challenge, daily, practice, custom) | Implemented (unit-tested) | `Progression/RunSetupFactory.cs`; practice records nothing, custom records statistics only, daily keeps best score per date |
| Unlocks, achievements, completion marks, statistics | Implemented (unit-tested) | `Progression/ProgressionService.cs`; statistics/achievement screens are placeholder IMGUI; endings not started |
| UI (menus, HUD, minimap, settings, inventory) | In progress | Placeholder IMGUI title/mode/character/challenge/stats/summary screens and HUD with minimap (type-checked only); menu navigation logic unit-tested; settings and inventory screens not started; final uGUI not started |
| Audio (music, SFX, mixing) | Not started | |
| Art, animation, lighting, VFX | Not started | |
| Object pooling | Implemented (type-checked only) | Actor sprites pooled by id (`ActorViews`); simulation actors are plain objects |
| Windows build | Not started | |

## Content production checklist

Counts are actual shipped JSON entries that pass validation. Starter entries exist to exercise systems; they still
count only if they are distinct, functional content.

| Category | Target | Current | Status |
|---|---:|---:|---|
| Playable characters | 12 | 12 | Implemented (data + rules; no sprites) |
| Collectible items (total) | 300 | 301 | Data complete; every item passes the headless smoke test (held, fired, actives used). Balance untested in play |
| Consumables (sigils + draughts) | - | 14 | In progress |
| Floor hexes | - | 5 | In progress |
| Shrine events | - | 4 | In progress |
| Passive items | 180 | 196 | Data complete |
| Active items | 45 | 45 | Data complete |
| Trinkets | 60 | 60 | Data complete |
| Unique enemies | 120 | 120 | Data complete (8 per floor, archetype-based); every enemy fought headless. No sprites, balance untested in play |
| Major bosses | 30 | 30 | Data complete (2 per floor, 2-3 phases); every floor's boss beaten headless. No art, balance untested |
| Minibosses | 15 | 15 | Data complete (1 per floor). No art |
| Room templates | 250 | 250 | Data complete: 24 hand-drawn + 226 generated by Tools/ContentGen/gen_rooms.py (14 themed combat rooms per floor + 16 special). All pass RoomTemplateValidator. Generated layouts are not hand-tuned |
| Floor environments | 15 | 15 | Data complete; every floor generates and is walked headless. No art/audio |
| Transformations | 12 | 12 | Data complete; every required tag has enough items |
| Companions | 30 | 30 | Data complete (Companions/companions.json), validated; balance untested in play |
| Challenges | 30 | 30 | Data complete; 14 are unlocked by defeating a floor boss. Every challenge starts and plays headless |
| Achievements | 100 | 113 | Data complete (boss, floor-depth, transformation and challenge achievements added); unlock chain validated |
| Synergies (no minimum set) | - | 10 | In progress |
| Status effects | - | 8 | In progress |

## System integration checklist

| Integration | Status |
|---|---|
| Bootstrap loads content + saves + input in Unity | Not verified in Unity |
| Item engine <-> player shooting | Done headless (RoomSimulation builds shots through the engine; SimulationTests, every-item smoke test). Unity side not verified |
| Item engine <-> enemies (hit/kill events) | Done headless (DamageEnemy/Kill notify the engine; tested). Unity side not verified |
| Item engine <-> companions | Done headless (CompanionTests). Unity side not verified |
| Item engine <-> bombs / pickups / shops | Done headless (SimulationTests). Unity side not verified |
| Floor generator <-> room instantiation / transitions | Done headless (doors, floors, exits tested). Unity drawing type-checked only |
| Saves <-> progression / achievements | Done headless (ProgressionTests). Unity file IO not verified |
| Input <-> UI navigation | MenuNavigator tested headless; Unity input bindings type-checked only |
| Event bus <-> HUD / audio | SimEvents drained by RunDirector/HUD; type-checked only, not run in Unity |
