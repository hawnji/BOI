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
| 1 Project foundation | In progress | Core architecture, item engine, procgen, saves, input abstraction, content pipeline, editor tools written; core passes 79 headless tests. Pending: open in Unity 6000.6.9f1, confirm package versions and URP 2D renderer assets, compile Runtime/Editor in Unity. |
| 2 Core player gameplay | In progress | Engine-free movement model, fire controller, projectile simulation and room/door state machine implemented and unit-tested. Unity player/projectile/room scene code not started. |
| 3 Combat and interaction foundations | Not started | Pure parts already exist (damage info, status controller, health pool). |
| 4 Item interaction engine | In progress | Engine core done early (spec: "design early"). Remaining: integration with real projectiles/companions/enemies, in-game debug overlay. |
| 5 Procedural generation | In progress | Generator core done early. Remaining: full template library (250), room instantiation in Unity, editor template editor. |
| 6 Enemies and bosses | Not started | |
| 7 Content expansion | Not started | |
| 8 Progression and game modes | Not started | Save models for unlocks/achievements/marks/stats exist. |
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
| Companion hooks (spawn per stack, inherit player shots, boost) | Implemented (unit-tested) | Companion AI not started |
| Room template model + validator + duplicate detection | Implemented (unit-tested) | |
| Floor graph, room types, secrets, loops, door kinds | Implemented (unit-tested) | 300 seeds x 2 floors validated in tests |
| Template selection with anti-repetition | Implemented (unit-tested) | |
| Encounter population (budget, compositions, elites, bosses) | Implemented (unit-tested) | Enemy definitions not started; pools reference ids only |
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
| Player controller, projectile objects, camera, room instantiation, door transitions (Unity) | Not started | Phase 2 Unity side |
| Enemy framework and AI | Not started | Phase 3 |
| Bosses / minibosses | Not started | Phase 6 |
| Pickups, bombs, consumables, economy/shops | Not started | Data hooks exist (drop chance, shop price, bomb spec) |
| Curses, random events, room modifiers | Not started | Engine supports curse sources and `curse:` conditions |
| Difficulty system | Not started | |
| Game modes (standard, challenge, daily, practice, custom) | Not started | Daily seed function exists |
| Unlocks, achievements, completion marks, endings, statistics UI | Not started | Save models exist |
| UI (menus, HUD, minimap, settings, inventory) | Not started | |
| Audio (music, SFX, mixing) | Not started | |
| Art, animation, lighting, VFX | Not started | |
| Object pooling | Not started | |
| Windows build | Not started | |

## Content production checklist

Counts are actual shipped JSON entries that pass validation. Starter entries exist to exercise systems; they still
count only if they are distinct, functional content.

| Category | Target | Current | Status |
|---|---:|---:|---|
| Playable characters | 12 | 0 | Not started |
| Collectible items (total) | 300 | 21 | In progress |
| Passive items | 180 | 17 | In progress |
| Active items | 45 | 2 | In progress |
| Trinkets | 60 | 2 | In progress |
| Unique enemies | 120 | 0 | Not started (5 ids referenced by the starter floor pool, no definitions) |
| Major bosses | 30 | 0 | Not started (2 ids referenced) |
| Minibosses | 15 | 0 | Not started (1 id referenced) |
| Handcrafted room templates | 250 | 24 | In progress |
| Floor environments | 15 | 1 | In progress (data only, no art/audio) |
| Transformations | 12 | 2 | In progress |
| Companions | 30 | 0 | Not started (ids referenced: wisp, bone_sparrow, drone_mite) |
| Challenges | 30 | 0 | Not started |
| Achievements | 100 | 0 | Not started |
| Synergies (no minimum set) | - | 4 | In progress |
| Status effects | - | 8 | In progress |

## System integration checklist

| Integration | Status |
|---|---|
| Bootstrap loads content + saves + input in Unity | Not verified in Unity |
| Item engine <-> player shooting | Not started |
| Item engine <-> enemies (hit/kill events) | Not started |
| Item engine <-> companions | Not started |
| Item engine <-> bombs / pickups / shops | Not started |
| Floor generator <-> room instantiation / transitions | Not started |
| Saves <-> progression / achievements | Not started |
| Input <-> UI navigation | Not started |
| Event bus <-> HUD / audio | Not started |
