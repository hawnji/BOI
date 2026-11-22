"""Renders every dungeon tile (13 kinds x 15 themes) as a 64x64 top-down PNG.

Usage: python Tools/ArtGen/gen_tiles.py [theme ...]   (needs the `bpy` module: pip install bpy)
Output: Assets/Resources/Tiles/<theme>/<kind>.png
Tiles are modelled as real geometry (slabs, bricks, rocks, braziers...) whose edges meet the tile border,
so they tile seamlessly. Obstacles sit on that theme's floor so each tile is a complete, opaque square.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import hc_common as hc  # noqa: E402

SIZE = 64

# stone, mortar, accent (metal/wood), glow, liquid
THEMES = {
    "crypt":   dict(stone="#6b625a", mortar="#211c1a", accent="#7a5230", glow="#ffb24a", liquid="#2f4f6e"),
    "ossuary": dict(stone="#b9a98c", mortar="#3b3127", accent="#6e5a3c", glow="#ffd27a", liquid="#4d5e52"),
    "mire":    dict(stone="#4f5a3a", mortar="#1d2214", accent="#5b4429", glow="#b6ff5a", liquid="#3d5a2b"),
    "forge":   dict(stone="#3d3836", mortar="#120e0d", accent="#58565a", glow="#ff6a1a", liquid="#ff5a10"),
    "frost":   dict(stone="#9fb6c9", mortar="#3a4a5a", accent="#5f7487", glow="#9ee8ff", liquid="#6fb4e0"),
    "garden":  dict(stone="#5f6b4b", mortar="#24301b", accent="#6b4b2a", glow="#f5ff8a", liquid="#2f5d55"),
    "belfry":  dict(stone="#5d6470", mortar="#1e2128", accent="#8a6a3a", glow="#ffe38a", liquid="#33465e"),
    "chapel":  dict(stone="#8792a0", mortar="#2b3038", accent="#4a5d75", glow="#a9d4ff", liquid="#2b5878"),
    "orchard": dict(stone="#8a6a4b", mortar="#2e2116", accent="#5c3d22", glow="#ffc06a", liquid="#4a5a2a"),
    "hive":    dict(stone="#a87a2e", mortar="#3a2508", accent="#6b4310", glow="#ffd23a", liquid="#d99a1a"),
    "glass":   dict(stone="#5f8f92", mortar="#16302f", accent="#9fd8d6", glow="#c8fffb", liquid="#3fa5b0"),
    "void":    dict(stone="#3a2f4f", mortar="#0d0914", accent="#5a3f7a", glow="#c07aff", liquid="#2a1a45"),
    "throne":  dict(stone="#6a2f30", mortar="#1f0b0c", accent="#c79a3a", glow="#ffcf5a", liquid="#5a1a20"),
    "heart":   dict(stone="#8a3b3f", mortar="#2a0d10", accent="#5a1e22", glow="#ff6a7a", liquid="#8a0f1e"),
    "last":    dict(stone="#2c2b30", mortar="#08080a", accent="#d8d4c8", glow="#f0ecff", liquid="#141418"),
}

KINDS = ["floor", "rock", "block", "pit", "spikes", "prop", "marked_rock", "fire", "water", "explosive",
         "wall", "door_open", "door_closed"]


def materials(t, theme):
    c = {k: hc.hexcol(v) for k, v in t.items()}
    return dict(
        slab=hc.mat("slab", c["stone"], rough=0.85, noise=0.22, bump=0.35, scale=18),
        slab2=hc.mat("slab2", tuple(v * 0.9 for v in c["stone"]), rough=0.85, noise=0.25, bump=0.35, scale=22),
        mortar=hc.mat("mortar", c["mortar"], rough=1.0, noise=0.1, bump=0.1),
        rock=hc.mat("rock", tuple(min(1, v * 1.15) for v in c["stone"]), rough=0.9, noise=0.3, bump=0.6, scale=10),
        block=hc.mat("block", tuple(v * 0.75 for v in c["stone"]), rough=0.7, noise=0.15, bump=0.3, scale=6),
        metal=hc.mat("metal", c["accent"], rough=0.35, metal=0.9, noise=0.15, bump=0.15),
        wood=hc.mat("wood", c["accent"], rough=0.7, noise=0.35, bump=0.3, scale=30),
        glow=hc.glow_mat("glow", c["glow"], 2.5),
        flame=hc.glow_mat("flame", (1.0, 0.35, 0.05), 3.0),
        dark=hc.mat("dark", (0.01, 0.01, 0.012), rough=1.0, noise=0.0, bump=0.0),
        liquid=hc.mat("liquid", c["liquid"], rough=0.05, noise=0.2, bump=0.15, scale=5),
        red=hc.mat("red", (0.6, 0.08, 0.06), rough=0.5, noise=0.2, bump=0.2),
        spike=hc.mat("spike", (0.75, 0.75, 0.8), rough=0.25, metal=1.0, noise=0.1, bump=0.05),
    )


def floor(m, seed, holes=()):
    """2x2 bevelled slabs per tile on a mortar bed; slab edges stop exactly at the tile border."""
    r = hc.rnd(seed)
    hc.box((0, 0, -0.05), (1, 1, 0.1), m["mortar"], bevel=0)
    for ix in range(2):
        for iy in range(2):
            if (ix, iy) in holes:
                continue
            h = 0.04 + r.uniform(-0.008, 0.008)
            hc.box((-0.25 + ix * 0.5, -0.25 + iy * 0.5, h / 2), (0.47, 0.47, h), m["slab" if (ix + iy) % 2 == 0 else "slab2"], bevel=0.02)


def build(kind, m, theme):
    seed = hash((kind, theme)) & 0xffff
    r = hc.rnd(seed)
    if kind == "floor":
        floor(m, seed)
    elif kind == "rock":
        floor(m, seed)
        hc.ico((0, 0, 0.18), 0.4, m["rock"], scale=(1, 0.95, 0.7), subdiv=4, displace=0.12, seed=seed)
        for _ in range(3):
            hc.ico((r.uniform(-0.35, 0.35), r.uniform(-0.35, 0.35), 0.05), 0.06, m["rock"], subdiv=2, displace=0.02, seed=seed + 1)
    elif kind == "marked_rock":
        floor(m, seed)
        hc.ico((0, 0, 0.18), 0.4, m["rock"], scale=(1, 0.95, 0.7), subdiv=4, displace=0.1, seed=seed)
        hc.box((0, 0, 0.47), (0.22, 0.05, 0.02), m["glow"], bevel=0, rot=(0, 0, math.radians(45)))
        hc.box((0, 0, 0.47), (0.22, 0.05, 0.02), m["glow"], bevel=0, rot=(0, 0, math.radians(-45)))
    elif kind == "block":
        floor(m, seed)
        hc.box((0, 0, 0.25), (0.92, 0.92, 0.5), m["block"], bevel=0.06)
        hc.box((0, 0, 0.51), (0.6, 0.6, 0.03), m["metal"], bevel=0.01)
    elif kind == "pit":
        # a deep shaft: black bottom far below, shadowed inner walls, broken slab lip around the edge
        hc.box((0, 0, -2.0), (1, 1, 0.1), m["dark"], bevel=0)
        for i in range(4):
            a = i * math.pi / 2
            ox, oy = 0.5 * math.cos(a), 0.5 * math.sin(a)
            hc.box((ox, oy, -1.0), (0.12 if i % 2 == 0 else 1.0, 1.0 if i % 2 == 0 else 0.12, 2.0), m["mortar"], bevel=0)
        for i in range(4):
            a = i * math.pi / 2
            for j in range(3):
                t = -0.33 + j * 0.33
                x = 0.43 * math.cos(a) + (t if i % 2 else 0)
                y = 0.43 * math.sin(a) + (0 if i % 2 else t)
                hc.box((x, y, 0.02), (0.14 if i % 2 == 0 else 0.3, 0.3 if i % 2 == 0 else 0.14, 0.04), m["slab2"], bevel=0.015)
    elif kind == "spikes":
        floor(m, seed)
        for ix in range(3):
            for iy in range(3):
                x, y = -0.3 + ix * 0.3, -0.3 + iy * 0.3
                hc.cyl((x, y, 0.045), 0.1, 0.01, m["dark"])
                hc.cone((x, y, 0.15), 0.07, 0.22, m["spike"], verts=12)
    elif kind == "prop":
        floor(m, seed)
        hc.cyl((0, 0, 0.22), 0.3, 0.44, m["wood"], verts=32, r2=0.26)
        hc.torus((0, 0, 0.12), 0.29, 0.025, m["metal"])
        hc.torus((0, 0, 0.34), 0.275, 0.025, m["metal"])
        hc.cyl((0, 0, 0.445), 0.24, 0.02, m["wood"])
    elif kind == "explosive":
        floor(m, seed)
        hc.cyl((0, 0, 0.22), 0.3, 0.44, m["red"], verts=32, r2=0.27)
        hc.torus((0, 0, 0.12), 0.295, 0.025, m["metal"])
        hc.torus((0, 0, 0.34), 0.28, 0.025, m["metal"])
        hc.cyl((0, 0, 0.445), 0.25, 0.02, m["red"])
        hc.box((0, 0, 0.46), (0.22, 0.06, 0.01), m["glow"], bevel=0)
        hc.box((0, 0, 0.46), (0.06, 0.22, 0.01), m["glow"], bevel=0)
    elif kind == "fire":
        floor(m, seed)
        hc.cyl((0, 0, 0.12), 0.32, 0.24, m["metal"], verts=8, r2=0.38)
        hc.cyl((0, 0, 0.25), 0.3, 0.04, m["dark"], verts=8)
        for i in range(5):
            a = i * 2 * math.pi / 5
            hc.cone((0.13 * math.cos(a), 0.13 * math.sin(a), 0.36), 0.1, 0.26, m["flame"], verts=10)
        hc.cone((0, 0, 0.42), 0.12, 0.36, m["glow"], verts=10)
    elif kind == "water":
        hc.box((0, 0, -0.2), (1, 1, 0.1), m["mortar"], bevel=0)
        p = hc.plane((0, 0, -0.02), (1, 1), m["liquid"])
        wv = p.modifiers.new("Sub", "SUBSURF")
        wv.levels = wv.render_levels = 4
        wv.subdivision_type = "SIMPLE"
        t = hc.bpy.data.textures.new("waves", "CLOUDS")
        t.noise_scale = 0.18
        d = p.modifiers.new("Wave", "DISPLACE")
        d.texture = t
        d.strength = 0.012
    elif kind == "wall":
        hc.box((0, 0, 0.0), (1, 1, 0.1), m["mortar"], bevel=0)
        rows = 4
        for row in range(rows):
            off = 0.0 if row % 2 == 0 else 0.25
            y = -0.5 + (row + 0.5) / rows
            x = -0.5 - off
            while x < 0.5:
                x0, x1 = max(x, -0.5), min(x + 0.5, 0.5)
                if x1 - x0 > 0.05:
                    h = 0.6 + r.uniform(-0.03, 0.03)
                    hc.box(((x0 + x1) / 2, y, h / 2), (x1 - x0 - 0.025, 1 / rows - 0.025, h), m["block"], bevel=0.015)
                x += 0.5
    elif kind in ("door_open", "door_closed"):
        floor(m, seed)
        # stone frame on the left/right edges, lintel stones on top
        for sx in (-0.42, 0.42):
            hc.box((sx, 0, 0.35), (0.16, 1.0, 0.7), m["block"], bevel=0.02)
        if kind == "door_closed":
            for i in range(4):
                hc.box((-0.255 + i * 0.17, 0, 0.12), (0.16, 0.95, 0.06), m["wood"], bevel=0.01)
            hc.box((0, 0.25, 0.16), (0.66, 0.06, 0.02), m["metal"], bevel=0.005)
            hc.box((0, -0.25, 0.16), (0.66, 0.06, 0.02), m["metal"], bevel=0.005)
            hc.cyl((0.2, 0, 0.17), 0.04, 0.03, m["metal"])
        else:
            hc.box((0, 0, 0.005), (0.68, 1.0, 0.01), m["dark"], bevel=0)


def main():
    themes = sys.argv[1:] or list(THEMES)
    for theme in themes:
        for kind in KINDS:
            hc.reset(samples=20)
            hc.camera(top_down=True, ortho_scale=1.0)
            hc.lights()
            m = materials(THEMES[theme], theme)
            build(kind, m, theme)
            hc.bpy.context.scene.render.film_transparent = False
            hc.render(os.path.join(hc.TILES, theme, kind + ".png"), SIZE)
            print("tile", theme, kind, flush=True)


if __name__ == "__main__":
    main()
