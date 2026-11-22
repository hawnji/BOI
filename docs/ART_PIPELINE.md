# Art pipeline (Blender, pre-rendered)

All world art is made from **procedural Blender models rendered to sprites**. The game stays 2D: models are
rendered once, offline, and the PNGs are committed under `Assets/Resources`.

| Script | Renders | Output | Size |
|---|---|---|---|
| `Tools/ArtGen/gen_tiles.py` | 13 tile kinds x 15 floor themes, straight top-down | `Resources/Tiles/<theme>/<kind>.png` | 64 px |
| `Tools/ArtGen/gen_characters.py` | 12 playable characters (+ `player`), high 3/4 view | `Resources/Sprites/character_<id>.png` | 128 px |
| `Tools/ArtGen/gen_creatures.py` | every enemy, miniboss, boss and companion in the content JSON | `Resources/Sprites/<spriteKey>.png` | 128 / 192 / 256 / 96 px |
| `Tools/ArtGen/gen_items.py` | every item icon, plus pickups, bombs, shots, pedestal, exits, hazards, shrines | `Resources/Sprites/item_<id>.png` etc. | 96 / 128 px |

Shared helpers (scene, camera, lights, materials, primitives) live in `Tools/ArtGen/hc_common.py`.
`Tools/ArtGen/contact_sheet.py` makes a scaled preview sheet of any set of PNGs.

## Running

```
pip install bpy pillow        # Blender as a Python module (5.x), CPU Cycles, no GPU needed
python Tools/ArtGen/gen_tiles.py            # or: gen_tiles.py crypt forge   (only those themes)
python Tools/ArtGen/gen_characters.py       # or: gen_characters.py wanderer
python Tools/ArtGen/gen_creatures.py        # or: --only boss, or a list of ids
python Tools/ArtGen/gen_items.py            # or: --builtins-only, or a list of item ids
```

The whole set takes a few minutes on 4 CPU cores. Output is deterministic (seeded by id).

## How models are chosen

- **Tiles:** real geometry (slab floors, brick walls, rocks, braziers, barrels, spikes, door frames) built so edges
  meet the tile border and tile seamlessly; colours from the per-theme palette in `gen_tiles.THEMES`.
- **Creatures:** a body plan (eye, turret, lantern, flyer, wisp, blob, hopper, crawler, brute, humanoid) picked from
  the creature's name, description, tags and movement; palette from the floor theme it appears on, shifted by
  element (fire, frost, rot, storm, bone). Bosses and minibosses add a crown, glowing gem and back spikes.
- **Items:** an object model (ring, key, jar, bell, boots, feather, lantern, book, blade, bomb, critter, ...) picked
  from the words of the item's name; colours from name and tags (ember, frost, gilded, bone, void...).

To restyle something, edit the script and rerun it; to hand-replace one asset, drop a PNG at the same path.

## In the game

- `PlaceholderArt.LoadArt` wraps any sprite so its width is one world unit (actors are scaled to their radius).
- `RoomRenderer` composites tiles at 64 px per tile; door art is turned a quarter for side walls.
- `Editor/ArtImportSettings.cs` sets import settings (sprites smooth and high quality; tiles readable).

## Not done yet

Animation, VFX, achievement / boss intro / HUD icons, and hand-made hero art. This first pass has not been seen in
a running build yet.
