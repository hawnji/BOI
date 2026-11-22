# Content authoring

All content lives in `Assets/StreamingAssets/Content/**/*.json`. Files are additive: any file may contain any of the
top-level arrays `items`, `synergies`, `transformations`, `statuses`, `rooms`, `floors`. Run
**Hollowcrawl > Validate All Content** (or the headless tests) after editing.

## Items

```json
{ "id": "ember_sigil", "name": "Ember Sigil", "description": "Shots may set foes alight",
  "kind": "Passive", "rarity": "Uncommon", "quality": 2, "pools": ["treasure"], "tags": ["fire"],
  "stacking": "Unique",
  "effects": [ { "effect": "proj_element", "type": "Fire", "status": "burn", "chance": 0.35, "duration": 3 } ] }
```

- `kind`: `Passive | Active | Trinket | Special`. Actives add `"active": { "chargeType": "Rooms|Seconds|Kills|OneShot", "maxCharge": 3, "onUse": [ ...effects ] }`.
- `stacking`: `Unique` (default), `Stack`, `StackCapped` + `maxStacks`.
- `tags` drive tag synergies and transformations (`fire`, `companion`, `poison`, ...).
- Each effect: `effect` (handler key), optional `condition`, optional `id` (for targeted suppression); all other keys are parameters.
- Items must have at least one effect, a name, a description and (normally) a pool.

## Effect reference (built-in)

The authoritative list with parameters is `Assets/_Project/Scripts/Core/Items/Effects/BuiltInEffects.cs`
(each registration includes its parameter documentation; the Synergy Debugger shows them in action).

| Group | Effects |
|---|---|
| Stats | `stat`, `stat_scaling` |
| Projectile | `proj_pierce`, `proj_homing`, `proj_bounce`, `proj_split`, `proj_explode`, `proj_chain`, `proj_element`, `proj_size`, `proj_volley`, `proj_delivery`, `proj_spectral`, `proj_charge`, `proj_accelerate`, `proj_wavy`, `proj_damage_mult`, `proj_visual`, `proj_convert_status` |
| Combat triggers | `onhit_explode`, `onhit_status_aoe`, `onhit_burst`, `onkill_drop`, `onkill_heal`, `onkill_explode`, `onkill_spread_status`, `ondamaged_burst`, `ondamaged_ward`, `ondamaged_charge`, `invuln_on_hit`, `outgoing_mult`, `incoming_immunity`, `incoming_block_chance` |
| Rooms / economy | `roomclear_drop`, `roomclear_heal`, `roomclear_charge`, `roomenter_status`, `floorstart_resource`, `grant_resource`, `grant_health`, `pickup_bonus`, `shop_discount`, `drop_chance`, `trinket_slots` |
| Bombs | `bomb_mod`, `bomb_explode_shots` |
| Companions | `companion`, `companion_inherit`, `companion_boost` |
| Over time | `regen`, `aura_status`, `active_used_status` |
| Active use | `active_room_damage`, `active_heal`, `active_temp_stat`, `active_projectile_ring`, `active_teleport`, `active_spawn_pickups`, `active_companion_room`, `active_explosions` |

## Conditions

`always`, `health_full`, `health_at_most:N`, `total_health_at_most:N`, `has_temp_health`, `no_containers`,
`has_item:ID`, `has_tag:TAG[:N]`, `character:ID`, `curse:ID`, `in_combat`, `room_type:TYPE`, `luck_at_least:N`,
`stat_at_least:STAT:N`, `stat_below:STAT:N`, `coins_at_least:N`, `bombs_at_least:N`, `keys_at_least:N`,
`stacks_at_least:N`, `floor_at_least:N`, `companions_at_least:N`, `transformed[:ID]`.
Combine with `&` (and), `|` (or, lower precedence) and `!` (not).

## Synergies and transformations

```json
{ "id": "sunlance", "name": "Sunlance", "description": "...", "priority": 20, "exclusiveGroup": "delivery_fire", "hidden": false,
  "requires": [ { "itemId": "ember_sigil" }, { "itemId": "needle_quill" } ],
  "condition": null,
  "effects": [ { "effect": "proj_delivery", "delivery": "Beam", "priority": 20 } ],
  "suppresses": [ "needle_quill/proj_pierce" ] }
```

Requirements use `itemId` (with optional `count` = stacks) or `tag` + `count`. Synergies use *held* items;
transformations use items *collected this run* and are permanent once triggered. Suppression patterns:
`itemId/effectType`, `itemId/effectId`, `*/effectType` (any item), `synergy:<id>` (a lower-priority synergy).

## Status effects

`{ "id": "burn", "damageType": "Fire", "tickInterval": 0.5, "damagePerTick": 1.5, "moveMultiplier": 1, "damageTakenMultiplier": 1, "preventsAttacking": false, "canAffectBosses": true, "stacking": "Refresh|Intensify|Extend", "maxStacks": 1, "maxDuration": 6, "cancels": ["chill"] }`

## Room templates

Each cell of a room is **15 x 9 tiles**. Shapes: `Single` (15x9), `Wide` (30x9), `Tall` (15x18), `Large` (30x18),
`LNoNE`, `LNoNW`, `LNoSE`, `LNoSW` (30x18 with one quadrant made of spaces).

| Char | Meaning |
|---|---|
| `.` | floor |
| ` ` | void (outside an L shape only) |
| `#` | rock: blocks movement and shots, destroyed by bombs |
| `X` | indestructible pillar |
| `P` | pit: blocks walking, not flying or shots |
| `S` | spikes (hazard) |
| `F` | fire hazard |
| `W` | shallow water (slows) |
| `B` | destructible prop (barrel/crate/urn) |
| `K` | explosive prop |
| `T` | marked rock with a guaranteed reward |
| `E` | enemy spawn (boss spawn in boss rooms) |
| `C` | pickup spot |
| `I` | interaction point (pedestal, shop slot, shrine, chest) |

Doors sit in the middle of each outer wall of each cell: interior tiles `(7,0)` N, `(7,8)` S, `(0,4)` W, `(14,4)` E
(offset by 15/9 per cell). Door sockets are named `"x,y:N|E|S|W"` by local cell; list walled-off ones in `blockedDoors`.
Fields: `id`, `shape`, `types` (RoomType names), `themes` (floor theme ids or `*`), `difficulty` 1-10, `weight`, `tags`
(`flyers` allows enemy spawns unreachable on foot), `blockedDoors`, `layout` (rows north to south).

The validator rejects: wrong sizes, unknown tiles, blocked door entrances, doors that cannot reach each other without
bombs/flight, unreachable interaction points, combat rooms without spawns, shops with fewer than 2 slots, hazards in
start rooms, and flags duplicate or mirrored layouts.

## Floors

See `starter_floors.json`: room count range, template difficulty band, enemy budget, large-room and loop chances,
optional `specialRooms` rules (defaults: treasure + shop always, locked from the second floor; others by chance),
enemy pool (`enemyId`, `cost`, `weight`, `tags`, `maxPerRoom`), boss and miniboss pools, secret chances, elite chance,
music/ambience keys and hazards.

## Enemies and bosses (`Content/Enemies/*.json`)

Top-level arrays `enemies` and `bosses`. Enums are written by name (`"Chase"`, `"Spread"`).

**Enemy**: `id`, `name`, `description`, `spriteKey`, `health`, `healthPerFloor` (fraction added per floor index),
`contactDamage` (half points), `radius`, `knockbackResistance` (0..1), `aggroRange`, `thinkInterval`,
`retreatBelowHealth` (switch to KeepDistance under this health fraction), `tags`, `statusImmunities`,
`movement`, `attacks`, `onDeath`, `lootMultiplier` (0 = never drops).

**movement**: `kind` (Idle, Wander, Chase, KeepDistance, Orbit, Hop, Strafe, Burrow, Patrol), `speed`,
`preferredDistance`, `pauseTime`, `hopDistance`, `flying`.

**attack**: `id`, `kind` (Projectile, Dash, Summon, Slam, Explode, Hazard, Teleport, Beam), `weight`, `cooldown`,
`telegraph` (seconds of warning; the validator warns under 0.15), `duration`, `recovery`, `minRange`, `maxRange`,
`requiresLineOfSight`, `pattern` (Aimed, Spread, Ring, Spiral, Cross, X, RandomBurst, Wall), `count`, `spread`,
`projectileSpeed`, `damage`, `waves` (volleys spread over `duration`), `spiralStep`, `dashSpeed`, `radius`,
`hazardId`, `summonId`/`summonCount`/`maxAlive`, `minDifficulty` (0 story, 1 normal, 2 hard, 3 nightmare),
`telegraphKey`, `audioKey`. The first use of each attack waits half its cooldown.

**onDeath**: `kind` (Explode, SpawnEnemies, ProjectileRing, Hazard, Split), `enemyId`, `count`, `radius`,
`damage`, `projectileSpeed`, `hazardId`.

**Boss**: `id`, `name`, `title`, `tier` (Early, Mid, Late, Secret, Optional, Challenge, Final, Miniboss),
`body` (an enemy definition with `isBoss: true`), `phases`, `rewardPool`, `unlockOnDefeat`, `musicKey`, `introKey`.
A phase has `healthAtOrBelow` (fraction), optional `movement`, `attacks`, `invulnerableOnEnter` (seconds) and
`onEnter` attacks fired once when the phase starts. Phases always play in order, even if one hit skips past two
thresholds. Major bosses need at least two phases (validator warning).

Sprites: put art at `Resources/Sprites/<spriteKey>`; until then a coloured placeholder shape is drawn.
Audio: `Resources/Audio/<audioKey>` clips play for events when they exist.

## Consumables, hexes and shrines (`Content/Consumables/*.json`)

**consumables**: `id`, `name`, `description`, `kind` (Sigil = effect printed on it; Draught = unidentified, shown by a
per-run random colour until first drunk), `weight`, `harmful`, `minFloor`, `unlockId`, `onUse` (active effects only:
`active_heal`, `active_room_damage`, `active_teleport`, `active_spawn_pickups`, `active_projectile_ring`,
`active_temp_stat`, `active_perm_stat`, `active_resource`, `active_hurt_self` (never lethal), `active_reveal_map`, ...).

**hexes** (one may roll per floor after the first, chance from the difficulty): `id`, `name`, `description`, `weight`,
`minFloor`, flags `hideMap`, `hideRoomIcons`, `floorSizeMultiplier` (1..2), `veilPedestals`, `visionRadius`, and
ordinary `effects` attached as a curse for the floor (so `curse:<id>` conditions work).

**shrines** (event rooms): `id`, `name`, `description`, `spriteKey`, `weight`, `minFloor`, `options`. Each option has
`label`, `cost` (None, Coins, Health, Keys, Bombs) + `amount`, `maxUses`, `firstOutcomeWeightPerUse`, and weighted
`outcomes` of `kind` Nothing, Pickup (`pickup`, `count`), Coins (`count`), Item (`pool`), Heal/Damage (`count` half
points, damage never lethal), Hex (`contentId`), Consumable (`contentId` or random), Stat (`effect`: a passive
effect kept for the run), Enemies (`contentId`, `count`; relocks the room). `message` is shown to the player.
Health costs are refused if they would take the last half point.

## Companions (`Content/Companions/*.json`)

Items spawn companions with the `companion` effect (`"companion": "<id>"`); the id must exist here.
Fields: `id`, `name`, `description`, `spriteKey`, `behavior`, plus tuning:

| Behavior | What it does | Fields that matter |
|---|---|---|
| Shooter | trails you, shoots the nearest enemy in `range` | `fireInterval`, `damageScale`, `range`, `shotStatus`, `shotStatusChance` |
| Mimic | fires along your aim whenever you fire | `fireInterval`, `damageScale`, `shotStatus` |
| Orbital | circles you, contact damage | `orbitRadius`, `orbitSpeed`, `contactDps`, `blocksShots` |
| Chaser | hunts the nearest enemy | `speed`, `contactDps`, `range` |
| Collector | fetches loose pickups (never shop goods or pedestals) | `speed`, `range` |
| Bomber | drops player-safe bombs on enemies | `fireInterval`, `bombDamage`, `bombRadius`, `range` |
| Provider | spawns `providesPickup` every `provideEveryRooms` cleared rooms | `providesPickup`, `provideEveryRooms` |
| Guardian | steps between you and the nearest enemy shot and blocks it | `speed`, `radius` |

Companion shot damage is the player's built shot times `damageScale`; contact and bomb damage scale with the
player's Damage stat. The validator rejects shooters without a fire interval, contact companions that neither hurt
nor block, and providers with `provideEveryRooms` below 1.

## Characters, achievements and challenges (`Content/Progression/*.json`)

**characters** (listed in menus in file order): `id`, `name`, `title`, `description`, `trait`, `spriteKey`,
`unlockId` + `unlockHint` (omit for starting characters), `containers`, `ward`, `umbral`, `noContainers` (container
gains become Umbral), `coins`, `bombs`, `keys`, `consumableSlots`, `trinketSlots`, `startingItems`,
`startingConsumable`, `effects` (permanent character effects, e.g. `{ "effect": "stat", "stat": "Damage", "value": -0.3 }`).

**achievements**: `id`, `name`, `description`, `hidden`, `trigger`, `key`, `target`, `unlocks`. Triggers: Counter
(`key` = runs, wins, deaths, kills, bosses, items, floors, secrets, bombs, coins, challenges, streak, or an open
counter such as shrines_used, consumables_used, rooms_cleared), BossDefeated, WinWithCharacter, ReachFloor,
Transformation, ChallengeComplete, DiscoverItems, DiscoverSynergies, RunCondition (`key` = flawless, no_items, speed,
ascetic, hard, nightmare), AllCharacterWins. Every `unlockId` used anywhere must be granted by an achievement or a
challenge `rewardUnlock` (validator error otherwise).

**challenges**: `id`, `name`, `description`, `unlockId`, `characterId`, `startingItems`, `difficultyId`,
`floorCount`, `goal` (DefeatFinalBoss | ReachFloor + `goalFloor`), `noTreasureRooms`, `noShops`, `noHealthPickups`,
`hexChance`, `forcedHex`, `startingCoins/Bombs/Keys`, `effects`, `rewardUnlock`.

Practice runs record nothing. Custom-seed runs record statistics only. Daily runs use the date's seed, the Wanderer
and ignore personal unlocks so everyone plays the same run.

## Generated content files

Large batches are written by scripts in `Tools/ContentGen/` (run from the repo root with `python3`). Edit the
script, not the generated JSON, then run it and `dotnet test Tests/CoreTests`:

| Script | Output |
|---|---|
| `gen_items.py` | `Content/Items/expansion_items.json` |
| `gen_transformations.py` | `Content/Synergies/expansion_transformations.json` |

`ContentTargetTests` guards the counts reached so far and runs every item through a short headless fight.
