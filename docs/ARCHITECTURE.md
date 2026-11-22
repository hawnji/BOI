# Architecture

## Goals

1. A genuinely extensible **item interaction and synergy engine**.
2. **Hybrid procedural generation**: handcrafted room templates assembled by a seeded generator.
3. **Offline, single-player, PC-first** with platform specifics isolated so Linux/macOS/Steam Deck need no rewrite.

## Layers and assemblies

| Assembly | References | Purpose |
|---|---|---|
| `Hollowcrawl.Core` | Newtonsoft.Json only (`noEngineReferences: true`) | All rules and data: RNG, events, stats, health, statuses, projectile specs, item engine, procgen, saves, content, input model. Deterministic and unit-tested headless. |
| `Hollowcrawl.Runtime` | Core, Unity.InputSystem | MonoBehaviours and platform adapters: bootstrap, input source/rebinding, file storage, (later) player, enemies, rooms, UI, audio, VFX. |
| `Hollowcrawl.Editor` | Core, Runtime | Editor windows and validation menus. |
| `Hollowcrawl.Core.Tests` | Core, NUnit | EditMode tests; also compiled by `Tests/CoreTests` with dotnet. |

Rule: gameplay *decisions* live in Core; Unity code *executes* them (spawning, physics, rendering, audio).
Core talks to the world only through interfaces such as `IGameWorld`, `ISaveStorage`, `IPlayerInputSource`, `IPlatformServices`.

## Services and bootstrap

`GameBootstrap` is created before the first scene loads (`RuntimeInitializeOnLoadMethod`), lives in `DontDestroyOnLoad`,
and wires: platform services, `SaveService`, settings, profile, `ContentDatabase` + `RoomTemplateLibrary`, effect and
condition registries, Input System actions, rebinder. They are exposed through `GameServices` (static hub, reset on
play-mode entry so it works with domain reload disabled) and a `ServiceRegistry`. Cross-system notifications
(UI, audio, achievements) use the typed `EventBus` (struct events, duplicate-subscription and recursion guards).

## Determinism and seeds

- `SeededRandom` is PCG32 (integer-only, platform-identical). `string.GetHashCode` is never used for seeding (`StableHash` instead).
- Every system forks a named stream from the run seed (`Fork("floor", n)`, then `"layout"`, `"templates"`, `"encounters"`; items use the engine's own stream), so adding a roll in one system never changes another system's results.
- Seeds display as 8-character codes without ambiguous characters (`RunSeed.ToCode`, `TryParse`). Daily mode derives the seed from the date offline (`RunSeed.ForDate`).

## Item interaction engine (`Core/Items`)

```
ItemDefinition (data) --effects--> EffectSpec { effect:"proj_explode", params..., condition, id }
                                        |
                       EffectRegistry: type key -> factory -> IEffect instance
                                        |
InteractionEngine: owns EffectRecords (effect + context + condition + suppression state)
   discovers hook interfaces per effect -> sorted hook caches
   dispatch: BuildShot, ModifyOutgoing/IncomingDamage, NotifyHit/Kill/PlayerDamaged/RoomEntered/
             RoomCleared/FloorStarted/BombExploded/Pickup, ModifyBomb, GetShopPrice, GetDropChance, Tick, UseActive
```

- **Sources**: items (passive, active, trinket), synergies, transformations, character kits, curses, temporary buffs all attach the same way (`AttachSource`). Adding an item never touches combat code.
- **Hooks**: an effect implements any of `IProjectileModifier`, `IOnHitHook`, `IOnKillHook`, `IBombModifier`, `ICompanionShotModifier`, `IShopPriceModifier`, `ITickHook`, `IActiveEffect`, ... (see `EffectInterfaces.cs`).
- **Projectile pipeline**: `ProjectileSpec` is filled from stats, then every enabled `IProjectileModifier` in `Order` (delivery 0, shape 100, behaviour 200, element 300, scaling 400, synergy 500, visual 900), ties broken by registration order. Delivery (bolt/beam/lobbed/...) is exclusive with priority-based conflict resolution; behaviours stack.
- **Stacking**: items declare `Unique`, `Stack`, `StackCapped`; effects receive the stack count. Stat sheet caps modifiers per stat. Stat formula: `(base + flat) * (1 + Σpercent) * Πmultiply`, clamped by global caps, floors and ceilings.
- **Conditions**: data expressions such as `health_full`, `has_tag:fire:2 & luck_at_least:2`, `!has_temp_health` (registry is extensible). Conditional stat modifiers and hooks re-check automatically.
- **Synergies**: requirement sets (item ids and/or tag counts) plus optional condition; evaluated whenever inventory changes. Priority order, exclusive groups (only the best of a group applies), and **suppression** of ingredient effects (`"needle_quill/proj_pierce"`, `"*/proj_visual"`, `"synergy:<id>"`) let a combination *replace* behaviour instead of just adding to it. Hidden synergies are flagged for the codex.
- **Transformations**: count items collected during the run (not just held); permanent once triggered; exclusive groups replace older ones.
- **Safeguards**: proc recursion depth cap (4), per-frame reactive spawn budget, projectile generation limit (3), `ProjectileSpec.Sanitize()` caps (volley 16, split 8, chain 10, bounce 12), idempotent attach, exceptions in an effect are logged and isolated, stat modifier cap.
- **Debugging**: `InteractionEngine.GetSnapshot()` (items, effects with enabled/condition/suppression, synergies, transformations, pipeline order, ring-buffer log incl. guard trips). Editor: *Hollowcrawl > Synergy Debugger* (live run or sandbox).

## Procedural generation (`Core/ProcGen`)

1. **Graph**: rooms grow on a 13x13 cell grid from the start room. Shapes: 1x1, 2x1, 1x2, 2x2 and four L shapes. A new room may only touch its parent, so the base graph is a tree; deliberate loops between combat rooms are added afterwards (`LoopChance`).
2. **Types**: boss = farthest dead end with a boss template (distance >= 2); main path marked; special rooms (treasure, shop, miniboss, challenge, event, reward, healing, curse) claim remaining dead ends by data rules; locked rooms are always dead ends.
3. **Secrets**: secret room in the empty cell bordering the most rooms (never the boss); hidden vault borders exactly one combat room far from start. Secret doors only.
4. **Templates**: per room, candidates filtered by shape, type, floor theme, required open door sockets and difficulty band, weighted by template weight x anti-repetition factor (`GenerationHistory`: used this floor, run usage count, recent window). Relaxation passes before failing.
5. **Encounters**: per combat room budget = floor base + floor index x 2 + template difficulty; a lead enemy defines the composition (recent leads down-weighted), supports fill remaining spawn markers; elites by chance; boss chosen avoiding bosses already seen this run.
6. **Validation** (`FloorValidator`): single start/boss, every non-secret room reachable, boss reachable without keys/bombs/sacrifice, door pairs symmetric and facing correctly, every door socket open in its template. Failed attempts retry deterministically (up to 60).

Templates are validated on load (`RoomTemplateValidator`): size per shape, known tiles, doors reachable from each other without bombs/flight, markers reachable, type-specific rules, and duplicate/mirrored layout detection to prevent content inflation.

## Saves (`Core/Save`)

`SaveManager` writes JSON envelopes `{format, kind, schema, savedUtc, checksum(SHA-256), payload}` through `ISaveStorage`
(desktop: atomic temp-file replace under `persistentDataPath/Saves`). Three rotating backups; on load: current -> backups,
corrupt files preserved as `.corrupt`, main file healed from backup, forward migrations by schema, newer schemas refused.
`SaveService` exposes settings (device-wide, includes input binding overrides) and three profile slots (unlocks,
achievements, completion marks, discoveries, lifetime statistics, daily results).

## Input

Core defines `PlayerInputFrame`, `IPlayerInputSource`, `IInputRebinder` and `ShootingIntent` (hold, toggle auto-fire,
aim-to-fire). Runtime builds the Input System asset in code (`GameInputActions`: KeyboardMouse and Gamepad schemes,
WASD + mouse aim + arrow-key aim, sticks), reads it in `UnityPlayerInputSource` (last-used device wins) and rebinds via
`UnityInputRebinder` (overrides saved in settings).

## Content

All content is JSON under `Assets/StreamingAssets/Content` (one or many files per category, additive), loaded by
`ContentDirectoryLoader` into `ContentDatabase` and cross-checked by `ContentValidator` (unknown effects, conditions,
statuses, items, impossible requirements, items without effects). See `CONTENT_AUTHORING.md`.

## Performance notes (current)

- Hook caches are arrays rebuilt only when effects change; dispatch loops avoid LINQ and closures on hot paths (`Tick`, `NotifyHit`, `BuildShot`).
- `ProjectileSpec` is a reusable class (`Reset`/`CopyTo`) intended for pooling.
- Status effects tick from a single controller per entity.
- Pending: object pools, physics layer matrix, AI update scheduling (Phases 2-3).
