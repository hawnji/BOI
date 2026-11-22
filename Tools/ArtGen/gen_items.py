"""Renders item icons (all items in content) and the built-in world objects (pickups, bombs, shots, pedestal,
exits, hazards, shrines).

Usage: python Tools/ArtGen/gen_items.py [--builtins-only] [item_id ...]
Output: Assets/Resources/Sprites/item_<id>.png (96 px) and built-in keys (pickup_coin.png, pedestal.png, ...).
An item's model is picked from the words in its name (ring, key, jar, bell, boots, feather, lantern...), its
colours from its name and tags (ember -> fire glow, frost -> ice, gilded -> gold...). Seeded by id.
"""
import glob
import json
import math
import os
import sys
import zlib

sys.path.insert(0, os.path.dirname(__file__))
import hc_common as hc  # noqa: E402

CONTENT = os.path.join(hc.REPO, "Assets", "StreamingAssets", "Content")
R90 = math.radians(90)

# --------------------------------------------------------------------------- palette


def palette(words, tags, seed):
    r = hc.rnd(seed)
    w = set(words) | set(tags)
    base, glow, metal = "#8a6a4a", "#ffd27a", "#b8a070"
    def has(*ks):
        return any(k in w for k in ks)
    if has("ember", "cinder", "fire", "pyre", "ash", "ashen", "burning", "burnt", "brazier", "bonfire", "napalm", "kindling", "coal"):
        base, glow = "#5a3020", "#ff7a1a"
    if has("frost", "frozen", "rime", "winter", "hoarfrost", "glacier", "cold", "ice", "snow"):
        base, glow, metal = "#a8d0e8", "#bff4ff", "#c8d8e8"
    if has("rot", "plague", "venom", "toxic", "poison", "bile", "nettle", "spore"):
        base, glow = "#4a6a2a", "#b6ff5a"
    if has("storm", "thunder", "lightning", "static", "spark", "battery"):
        base, glow, metal = "#3a4a6a", "#ffe85a", "#9aa8c0"
    if has("blood", "heart", "vampire", "leech", "red"):
        base, glow = "#8a1a22", "#ff5a6a"
    if has("void", "shadow", "night", "dread", "nightmare", "umbral", "gloom", "echo", "wraith", "ghost"):
        base, glow, metal = "#3a2a52", "#c07aff", "#6a5a8a"
    if has("holy", "blessed", "sacred", "saint", "halo", "dawn", "sunlight", "prayer", "guardian", "ward"):
        base, glow, metal = "#e8dcc0", "#fff2b0", "#d8b050"
    if has("gold", "gilded", "coin", "miser", "penny", "purse", "wallet", "tithe", "merchant", "allowance", "lucky", "gambler", "bargain", "haggler"):
        metal, glow = "#e0b040", "#ffe27a"
    if has("iron", "anvil", "lead", "nail", "pewter", "tin", "brass", "copper"):
        metal = {"brass": "#c8a050", "copper": "#c87a4a", "pewter": "#a8a8a0", "tin": "#b8b8c0"}.get(next((k for k in ("brass", "copper", "pewter", "tin") if k in w), ""), "#8a8a92")
    if has("bone", "skeleton", "rattle", "marrow", "ossuary", "tooth", "fang", "grave", "rabbit", "crow"):
        base = "#e2d6bc"
    if has("glass", "mirror", "prism", "lens", "crystal", "shard", "marble"):
        base = "#9fd8e0"
    if has("powder", "bomb", "keg", "fuse", "blasting", "demolition", "cluster", "mortar", "firecracker", "sapper"):
        base, glow = "#2a2a30", "#ffb040"
    if has("thorn", "bramble", "bark", "seed", "root", "moss", "herb", "twig", "splinter", "verdant", "heartroot", "four-leaf", "toad"):
        base = "#4a6a32" if not has("bark", "twig", "splinter") else "#6a4a2a"
    import colorsys
    def jitter(hexv, amt=0.04):
        rr, gg, bb = (int(hexv[i:i + 2], 16) / 255 for i in (1, 3, 5))
        h, s, v = colorsys.rgb_to_hsv(rr, gg, bb)
        h = (h + r.uniform(-amt, amt)) % 1
        rr, gg, bb = colorsys.hsv_to_rgb(h, s, min(1, v * r.uniform(0.9, 1.1)))
        return "#%02x%02x%02x" % (int(rr * 255), int(gg * 255), int(bb * 255))
    base = jitter(base)
    return dict(
        base=hc.mat("base", hc.hexcol(base), rough=0.55, noise=0.2, bump=0.25, scale=r.uniform(12, 30), sss=0.05),
        metal=hc.mat("metal", hc.hexcol(metal), rough=0.25, metal=1.0, noise=0.1, bump=0.08),
        glow=hc.glow_mat("glow", hc.hexcol(glow), 2.5),
        glass=glass_mat(hc.hexcol(base)),
        cloth=hc.mat("cloth", hc.hexcol(base), rough=0.9, noise=0.25, bump=0.4, scale=60),
        wood=hc.mat("wood", hc.hexcol("#7a5432"), rough=0.7, noise=0.35, bump=0.3, scale=40),
        bone=hc.mat("bone", hc.hexcol("#e8dcc2"), rough=0.55, noise=0.15, bump=0.2),
        dark=hc.mat("dark", hc.hexcol("#1a161c"), rough=0.4, noise=0.1, bump=0.05),
        paper=hc.mat("paper", hc.hexcol("#e0d2a8"), rough=0.9, noise=0.15, bump=0.1, scale=40),
        rng=r,
    )


def glass_mat(color):
    m = hc.bpy.data.materials.new("glass")
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = list(color) + [1]
    p.inputs["Roughness"].default_value = 0.05
    p.inputs["Transmission Weight"].default_value = 0.9
    p.inputs["IOR"].default_value = 1.45
    return m


# --------------------------------------------------------------------------- models (all fit in ~0.4 radius)


def m_ring(p):
    hc.torus((0, 0, 0.12), 0.2, 0.05, p["metal"], rot=(math.radians(70), 0, 0))
    hc.ico((0, -0.06, 0.34), 0.08, p["glow"], subdiv=1, smooth=False)
    hc.cyl((0, -0.05, 0.29), 0.06, 0.04, p["metal"], verts=6)


def m_amulet(p):
    hc.torus((0, 0.05, 0.35), 0.2, 0.012, p["metal"], rot=(math.radians(60), 0, 0))
    hc.cyl((0, -0.05, 0.12), 0.15, 0.04, p["metal"], verts=24, rot=(math.radians(70), 0, 0))
    hc.ico((0, -0.09, 0.13), 0.08, p["glow"], subdiv=1, smooth=False)


def m_key(p):
    hc.torus((-0.18, 0, 0.1), 0.1, 0.03, p["metal"], rot=(0, 0, 0))
    hc.cyl((0.08, 0, 0.1), 0.025, 0.36, p["metal"], rot=(0, R90, 0))
    hc.box((0.2, -0.05, 0.1), (0.04, 0.1, 0.03), p["metal"], bevel=0.008)
    hc.box((0.12, -0.04, 0.1), (0.03, 0.08, 0.03), p["metal"], bevel=0.008)


def m_coin(p):
    for i, (x, y) in enumerate(((0, 0), (0.1, 0.06), (-0.08, 0.08))):
        hc.cyl((x, y, 0.02 + 0.04 * i), 0.15, 0.035, p["metal"], verts=32, bevel=0.008)
    hc.cyl((-0.08, 0.08, 0.1), 0.08, 0.01, p["glow"], verts=6)


def m_bottle(p):
    hc.sphere((0, 0, 0.18), 0.18, p["glass"])
    hc.sphere((0, 0, 0.16), 0.15, p["glow"], scale=(1, 1, 0.8))
    hc.cyl((0, 0, 0.4), 0.05, 0.14, p["glass"])
    hc.cyl((0, 0, 0.5), 0.055, 0.06, p["wood"])


def m_jar(p):
    hc.cyl((0, 0, 0.2), 0.2, 0.36, p["glass"], verts=32)
    hc.sphere((0, 0, 0.17), 0.15, p["base"], scale=(1, 1, 0.9))
    hc.cyl((0, 0, 0.4), 0.21, 0.05, p["metal"], verts=32, bevel=0.01)


def m_bell(p):
    hc.cone((0, 0, 0.22), 0.22, 0.32, p["metal"], verts=32, r2=0.09)
    hc.torus((0, 0, 0.07), 0.21, 0.025, p["metal"])
    hc.sphere((0, 0, 0.05), 0.05, p["dark"])
    hc.torus((0, 0, 0.42), 0.04, 0.015, p["metal"], rot=(R90, 0, 0))


def m_drum(p):
    hc.cyl((0, 0, 0.14), 0.22, 0.24, p["base"], verts=32)
    hc.cyl((0, 0, 0.265), 0.21, 0.01, p["paper"], verts=32)
    hc.torus((0, 0, 0.26), 0.22, 0.02, p["metal"])
    hc.torus((0, 0, 0.03), 0.22, 0.02, p["metal"])
    hc.cyl((0.12, -0.12, 0.34), 0.015, 0.26, p["wood"], rot=(0, math.radians(50), 0))


def m_boots(p):
    for sx in (-0.11, 0.11):
        hc.box((sx, 0.03, 0.15), (0.12, 0.12, 0.26), p["cloth"], bevel=0.04)
        hc.box((sx, -0.06, 0.05), (0.13, 0.24, 0.09), p["cloth"], bevel=0.04)
        hc.box((sx, -0.06, 0.005), (0.14, 0.25, 0.02), p["dark"], bevel=0.008)


def m_cloth(p):
    o = hc.ico((0, 0, 0.12), 0.3, p["cloth"], scale=(1, 0.8, 0.35), subdiv=4, displace=0.06, seed=3)
    hc.torus((0, 0.05, 0.2), 0.12, 0.02, p["metal"], rot=(math.radians(20), 0, 0))
    hc.ico((0, -0.05, 0.24), 0.04, p["glow"], subdiv=1, smooth=False)


def m_mask(p):
    hc.sphere((0, 0, 0.25), 0.25, p["base"], scale=(1, 0.35, 1.1))
    for sx in (-0.09, 0.09):
        hc.sphere((sx, -0.08, 0.3), 0.05, p["glow"], scale=(1.3, 0.5, 0.7))
    hc.box((0, -0.09, 0.14), (0.1, 0.02, 0.02), p["dark"], bevel=0.008)


def m_feather(p):
    hc.cyl((0, 0, 0.22), 0.012, 0.5, p["bone"], rot=(0, math.radians(35), 0))
    hc.sphere((0.05, 0, 0.27), 0.13, p["cloth"], scale=(0.5, 0.15, 1.8))
    hc.bpy.context.active_object.rotation_euler = (0, math.radians(35), 0)


def m_bone(p):
    hc.cyl((0, 0, 0.1), 0.045, 0.4, p["bone"], rot=(0, R90, math.radians(20)))
    for s in (-1, 1):
        for d in (-0.05, 0.05):
            hc.sphere((s * 0.19, d + s * 0.07, 0.1), 0.06, p["bone"])


def m_tooth(p):
    hc.cone((0, 0, 0.2), 0.12, 0.36, p["bone"], verts=20, r2=0.02, rot=(math.radians(180), 0, 0))
    hc.sphere((0, 0, 0.36), 0.13, p["bone"], scale=(1, 1, 0.5))


def m_shell(p):
    for i in range(14):
        t = i / 13
        a = t * 4 * math.pi
        r = 0.04 + 0.16 * (1 - t)
        hc.sphere((r * math.cos(a), r * math.sin(a), 0.12 + 0.12 * t), 0.13 * (1 - t) + 0.03, p["base"])


def m_heart(p, half=False):
    """Heart lying flat so its outline reads from the high game camera."""
    lobes = (-0.11,) if half else (-0.11, 0.11)
    for x in lobes:
        hc.sphere((x, 0.08, 0.12), 0.15, p["base"], scale=(1, 1, 0.7))
    o = hc.cone((-0.055 if half else 0, -0.1, 0.12), 0.21, 0.28, p["base"], verts=32, rot=(math.radians(90), 0, 0))
    o.scale = (0.5 if half else 1, 1, 0.7)
    hc.sphere((-0.12, 0.08, 0.22), 0.035, p["glow"])


def m_stone(p):
    hc.ico((0, 0, 0.16), 0.2, p["base"], scale=(1.1, 1, 0.8), subdiv=3, displace=0.04, seed=5)
    hc.box((0, -0.17, 0.18), (0.1, 0.01, 0.02), p["glow"], bevel=0)
    hc.box((0, -0.17, 0.18), (0.02, 0.01, 0.1), p["glow"], bevel=0)


def m_orb(p):
    hc.sphere((0, 0, 0.2), 0.2, p["glass"])
    hc.sphere((0, 0, 0.2), 0.1, p["glow"])
    hc.cyl((0, 0, 0.02), 0.13, 0.05, p["metal"], verts=24, r2=0.1)


def m_plant(p):
    hc.sphere((0, 0, 0.12), 0.15, p["base"], scale=(1, 1, 1.2))
    for i in range(5):
        a = i * 2 * math.pi / 5
        hc.sphere((0.16 * math.cos(a), 0.16 * math.sin(a), 0.25), 0.08, p["cloth"], scale=(1.8, 0.6, 0.25))
    hc.sphere((0, 0, 0.3), 0.05, p["glow"])


def m_crystal(p):
    for i, (x, y, h, t) in enumerate(((0, 0, 0.45, 0), (0.1, 0.05, 0.3, 20), (-0.1, 0.04, 0.32, -25))):
        hc.cone((x, y, h / 2), 0.08, h, p["glass"], verts=6, r2=0.0, rot=(0, math.radians(t), 0))
    hc.sphere((0, 0, 0.12), 0.06, p["glow"])


def m_lantern(p):
    hc.cyl((0, 0, 0.2), 0.14, 0.28, p["glow"], verts=6)
    for i in range(6):
        a = i * math.pi / 3
        hc.box((0.14 * math.cos(a), 0.14 * math.sin(a), 0.2), (0.02, 0.02, 0.3), p["metal"], bevel=0.003)
    hc.cyl((0, 0, 0.38), 0.17, 0.05, p["metal"], verts=6, r2=0.06)
    hc.cyl((0, 0, 0.04), 0.17, 0.04, p["metal"], verts=6)
    hc.torus((0, 0, 0.45), 0.05, 0.012, p["metal"], rot=(R90, 0, 0))


def m_candle(p):
    hc.cyl((0, 0, 0.15), 0.08, 0.28, p["paper"], verts=24)
    hc.cyl((0, 0, 0.02), 0.16, 0.03, p["metal"], verts=24)
    hc.cone((0, 0, 0.35), 0.035, 0.1, p["glow"], verts=10)


def m_book(p):
    hc.box((0, 0, 0.08), (0.34, 0.42, 0.13), p["paper"], bevel=0.01)
    hc.box((0, 0, 0.15), (0.37, 0.45, 0.02), p["base"], bevel=0.008)
    hc.box((0, 0, 0.01), (0.37, 0.45, 0.02), p["base"], bevel=0.008)
    hc.box((-0.18, 0, 0.08), (0.03, 0.45, 0.16), p["base"], bevel=0.01)
    hc.cyl((0.03, 0, 0.165), 0.06, 0.01, p["glow"], verts=6)


def m_scroll(p):
    hc.box((0, 0, 0.02), (0.36, 0.3, 0.01), p["paper"], bevel=0.003)
    for sy in (-0.16, 0.16):
        hc.cyl((0, sy, 0.04), 0.04, 0.4, p["paper"], rot=(0, R90, 0))
    hc.box((0.05, 0.02, 0.03), (0.12, 0.01, 0.002), p["glow"], bevel=0)
    hc.box((-0.05, -0.04, 0.03), (0.12, 0.01, 0.002), p["dark"], bevel=0)


def m_blade(p):
    hc.box((0.04, 0, 0.12), (0.4, 0.06, 0.015), p["metal"], bevel=0.005, rot=(0, 0, math.radians(30)))
    hc.box((-0.17, -0.12, 0.12), (0.03, 0.16, 0.04), p["metal"], bevel=0.008, rot=(0, 0, math.radians(30)))
    hc.cyl((-0.22, -0.15, 0.12), 0.025, 0.14, p["wood"], rot=(0, R90, math.radians(30)))


def m_needles(p):
    for i in range(5):
        a = -0.6 + 0.3 * i
        hc.cone((0.1 * math.sin(a), 0, 0.2), 0.02, 0.38, p["metal"], verts=8, r2=0.0, rot=(0, a, 0))


def m_bow(p):
    hc.torus((0, 0, 0.2), 0.24, 0.025, p["wood"], rot=(R90, 0, 0))
    hc.box((0, 0, 0.2), (0.48, 0.01, 0.01), p["dark"], bevel=0)


def m_eye(p):
    hc.sphere((0, 0, 0.2), 0.2, p["bone"])
    hc.sphere((0, -0.16, 0.24), 0.09, p["glow"], scale=(1, 0.4, 1))
    hc.sphere((0, -0.2, 0.25), 0.04, p["dark"], scale=(1, 0.4, 1))


def m_crown(p):
    hc.cyl((0, 0, 0.1), 0.2, 0.14, p["metal"], verts=32)
    for i in range(6):
        a = i * math.pi / 3
        hc.cone((0.19 * math.cos(a), 0.19 * math.sin(a), 0.24), 0.05, 0.16, p["metal"], verts=8)
        hc.sphere((0.2 * math.cos(a), 0.2 * math.sin(a), 0.1), 0.025, p["glow"])


def m_halo(p):
    hc.torus((0, 0, 0.2), 0.2, 0.03, p["glow"], rot=(math.radians(60), 0, 0))
    hc.torus((0, 0, 0.2), 0.2, 0.04, p["metal"], rot=(math.radians(60), 0, 0))


def m_bomb(p):
    hc.sphere((0, 0, 0.18), 0.2, p["base"])
    hc.cyl((0, 0, 0.38), 0.06, 0.06, p["metal"])
    hc.cyl((0.03, 0, 0.45), 0.015, 0.1, p["paper"], rot=(0, math.radians(20), 0))
    hc.sphere((0.05, 0, 0.5), 0.03, p["glow"])


def m_keg(p):
    hc.cyl((0, 0, 0.2), 0.2, 0.36, p["wood"], verts=32)
    hc.torus((0, 0, 0.08), 0.2, 0.015, p["metal"])
    hc.torus((0, 0, 0.32), 0.2, 0.015, p["metal"])
    hc.box((0, -0.2, 0.2), (0.12, 0.01, 0.12), p["glow"], bevel=0)


def m_coil(p):
    for i in range(7):
        hc.torus((0, 0, 0.06 + i * 0.05), 0.12, 0.02, p["metal"])
    hc.sphere((0, 0, 0.45), 0.06, p["glow"])


def m_bag(p):
    hc.sphere((0, 0, 0.17), 0.2, p["cloth"], scale=(1, 0.9, 0.95))
    hc.cone((0, 0, 0.38), 0.06, 0.1, p["cloth"], verts=16, r2=0.1)
    hc.torus((0, 0, 0.34), 0.07, 0.015, p["metal"])
    hc.cyl((0, -0.19, 0.17), 0.04, 0.01, p["glow"], verts=6, rot=(R90, 0, 0))


def m_box(p):
    hc.box((0, 0, 0.13), (0.4, 0.3, 0.24), p["wood"], bevel=0.02)
    hc.box((0, 0, 0.26), (0.42, 0.32, 0.03), p["metal"], bevel=0.008)
    hc.box((0, -0.155, 0.17), (0.06, 0.01, 0.07), p["glow"], bevel=0)


def m_horn(p):
    for i in range(10):
        t = i / 9
        a = t * math.pi * 0.8
        hc.sphere((-0.2 + 0.4 * t, 0, 0.1 + 0.15 * math.sin(a)), 0.03 + 0.08 * t, p["bone"])


def m_glove(p):
    hc.sphere((0, 0, 0.12), 0.16, p["cloth"], scale=(1, 1.2, 0.7))
    for i in range(4):
        hc.cyl((-0.09 + 0.06 * i, -0.2, 0.14), 0.03, 0.14, p["cloth"], rot=(R90, 0, 0))
    hc.cyl((0, 0.17, 0.12), 0.15, 0.12, p["metal"], rot=(R90, 0, 0))


def m_critter(p):
    hc.sphere((0, 0, 0.18), 0.18, p["base"], scale=(1, 1.2, 0.9))
    hc.sphere((0, -0.18, 0.26), 0.11, p["base"])
    for sx in (-0.05, 0.05):
        hc.sphere((sx, -0.28, 0.29), 0.025, p["glow"])
    hc.sphere((-0.17, 0.05, 0.26), 0.1, p["cloth"], scale=(1.4, 0.6, 0.15))
    hc.sphere((0.17, 0.05, 0.26), 0.1, p["cloth"], scale=(1.4, 0.6, 0.15))


def m_die(p):
    hc.box((0, 0, 0.14), (0.24, 0.24, 0.24), p["bone"], bevel=0.04, rot=(0, 0, math.radians(25)))
    for (x, y, z) in ((0, 0, 0.265), (-0.06, -0.12, 0.2), (0.07, -0.1, 0.08)):
        hc.sphere((x, y, z), 0.025, p["dark"])


def m_hourglass(p):
    hc.cyl((0, 0, 0.02), 0.16, 0.03, p["wood"])
    hc.cyl((0, 0, 0.44), 0.16, 0.03, p["wood"])
    hc.cone((0, 0, 0.13), 0.13, 0.2, p["glass"], r2=0.02)
    hc.cone((0, 0, 0.33), 0.02, 0.2, p["glass"], r2=0.13)
    hc.cone((0, 0, 0.09), 0.1, 0.1, p["glow"], r2=0.02)


def m_food(p):
    crust = hc.mat("crust", hc.hexcol("#a8743a"), rough=0.7, noise=0.3, bump=0.5, scale=25)
    hc.sphere((0, 0, 0.14), 0.24, crust, scale=(1.2, 0.8, 0.6))
    for i in range(3):
        hc.box((-0.12 + 0.12 * i, 0, 0.27), (0.03, 0.2, 0.02), p["base"], bevel=0.01, rot=(0, 0, math.radians(20)))


VOCAB = [
    (m_food, ["bread", "pork", "stew", "meat", "apple"]),
    (m_ring, ["ring", "band", "knuckle"]), (m_key, ["key", "keymaker"]), (m_coin, ["coin", "penny", "allowance", "tally", "flip"]),
    (m_bottle, ["vial", "flask", "tonic", "draught", "water", "wine", "salve", "sap", "honey", "potion"]),
    (m_jar, ["jar", "bowl", "cup", "bucket", "socket", "censer", "pot"]), (m_bell, ["bell"]), (m_drum, ["drum"]),
    (m_boots, ["boots", "heel", "wrap", "quickstep", "runner"]), (m_cloth, ["cloak", "mantle", "shroud", "veil", "rag", "bandage", "hood", "lint", "pocket", "pockets", "vest", "mail", "lining"]),
    (m_mask, ["mask", "visor", "face"]), (m_feather, ["feather", "quill"]), (m_tooth, ["tooth", "fang", "claw"]),
    (m_bone, ["bone", "rattle", "flute", "wishing"]), (m_shell, ["shell", "snail", "tortoise"]), (m_heart, ["heart", "locket", "heartroot", "pride"]),
    (m_orb, ["pearl", "marble", "orbit", "moon", "core", "eye of", "scrying"]), (m_eye, ["eye", "lens", "spyglass", "sight", "sentinel"]),
    (m_crystal, ["shard", "chip", "prism", "glass", "crystal", "rime", "frozen", "splinter"]), (m_lantern, ["lantern", "lamp"]),
    (m_candle, ["candle", "wick", "kindling"]), (m_book, ["book", "hymnal", "ledger", "tome"]), (m_scroll, ["map", "slip", "seal", "ink", "scrap"]),
    (m_blade, ["blade", "knife", "knives", "sickle", "edge", "lance", "whetstone", "flint", "whetted", "mark"]), (m_needles, ["needle", "nail", "thorn", "thorns", "pin", "spite"]),
    (m_bow, ["bow"]), (m_crown, ["crown"]), (m_halo, ["halo"]), (m_bomb, ["bomb", "bombs", "powder", "firecracker", "fuse", "cap", "charge", "shot", "burst"]),
    (m_keg, ["keg", "satchel", "vest", "corpse"]), (m_coil, ["coil", "spring", "wire", "rod", "plug", "battery"]),
    (m_bag, ["bag", "sack", "pouch", "purse", "wallet", "kit"]), (m_box, ["box", "chest"]), (m_horn, ["horn", "whistle"]),
    (m_glove, ["glove", "knuckles", "arm", "fist"]), (m_die, ["die", "dice", "loaded"]), (m_hourglass, ["glass sand", "sand", "hourglass"]),
    (m_stone, ["stone", "pebble", "bead", "rune", "sigil", "charm", "amulet", "tear", "salt", "ash"]),
    (m_plant, ["seed", "pod", "bloom", "herb", "root", "bark", "twig", "moss", "nettle", "stool", "puffball", "four-leaf", "spore", "sac", "gland"]),
    (m_critter, ["moth", "rat", "crow", "sparrow", "beetle", "tick", "leech", "wisp", "hound", "magpie", "imp", "goblin", "mole", "wren", "flock", "mite", "hive", "swarm", "toad", "owl", "raven"]),
]


def pick_model(name, rng):
    words = name.lower().replace("'s", "").split()
    for word in reversed(words):  # last word is usually the object ("Ember Sigil" -> sigil)
        for fn, keys in VOCAB:
            if word in keys:
                return fn
    low = name.lower()
    for fn, keys in VOCAB:
        if any(k in low for k in keys):
            return fn
    return rng.choice([m_amulet, m_stone, m_orb, m_ring])


def setup(size_scale=1.0, tilt=50):
    hc.reset(samples=28)
    hc.camera(top_down=False, ortho_scale=0.78 * size_scale, tilt_deg=tilt)
    hc.bpy.context.scene.camera.location.z += 0.17
    hc.lights(key=2.2)


def render_item(it):
    seed = zlib.crc32(it["id"].encode())
    words = it["name"].lower().replace("'s", "").split()
    setup()
    p = palette(words, it.get("tags", []), seed)
    pick_model(it["name"], p["rng"])(p)
    key = it.get("iconKey") or "item_" + it["id"]
    hc.render(os.path.join(hc.SPRITES, key + ".png"), 96)


# --------------------------------------------------------------------------- built-in world objects


def builtins():
    def go(key, words, fn, size=96, scale=1.0, tilt=50):
        setup(scale, tilt)
        p = palette(words, [], zlib.crc32(key.encode()))
        fn(p)
        hc.render(os.path.join(hc.SPRITES, key + ".png"), size)
        print("builtin", key, flush=True)

    go("pickup_coin", ["gold", "coin"], lambda p: (hc.cyl((0, 0, 0.1), 0.3, 0.07, p["metal"], verts=40, bevel=0.015, rot=(math.radians(60), 0, 0)),
                                                   hc.cyl((0, -0.04, 0.1), 0.12, 0.08, p["glow"], verts=6, rot=(math.radians(60), 0, 0))))
    go("pickup_key", ["iron"], m_key)
    go("pickup_bomb", ["bomb"], m_bomb)
    go("pickup_vigorfull", ["blood"], m_heart, tilt=20)
    go("pickup_vigorhalf", ["blood"], lambda p: m_heart(p, half=True), tilt=20)
    go("pickup_ward", ["frost"], m_heart, tilt=20)
    go("pickup_umbral", ["void", "umbral"], m_heart, tilt=20)
    go("pickup_ember", ["ember"], m_crystal)
    go("pickup_consumable", ["rune"], m_stone)
    go("pickup_trinket", ["charm"], m_amulet)
    go("pickup_item", ["gold"], m_box)
    def pedestal(p):
        stone = hc.mat("ped", hc.hexcol("#8a8078"), rough=0.8, noise=0.2, bump=0.4)
        hc.cyl((0, 0, 0.05), 0.42, 0.1, stone, verts=8, bevel=0.02)
        hc.cyl((0, 0, 0.18), 0.3, 0.18, stone, verts=8, r2=0.26, bevel=0.02)
        hc.cyl((0, 0, 0.3), 0.36, 0.06, stone, verts=8, bevel=0.02)
        hc.cyl((0, 0, 0.335), 0.2, 0.01, p["glow"], verts=32)
    go("pedestal", ["holy"], pedestal, size=128)
    def bomb_lit(p):
        m_bomb(p)
        hc.sphere((0.05, 0, 0.5), 0.06, p["glow"])
    go("bomb", ["bomb"], bomb_lit)
    go("bomb_companion", ["bomb", "storm"], bomb_lit)
    def trapdoor(p):
        wood = p["wood"]
        hc.box((0, 0, 0.01), (0.96, 0.96, 0.02), p["dark"], bevel=0)
        hc.box((0, 0, -0.3), (0.8, 0.8, 0.5), p["dark"], bevel=0)
        for i in range(4):
            a = i * math.pi / 2
            hc.box((0.44 * math.cos(a), 0.44 * math.sin(a), 0.04), (0.12 if i % 2 == 0 else 0.96, 0.96 if i % 2 == 0 else 0.12, 0.06), wood, bevel=0.015)
        hc.box((0.0, 0.3, 0.05), (0.6, 0.05, 0.04), p["metal"], bevel=0.008)
    go("exit_trapdoor", ["iron"], trapdoor, size=128, scale=1.15, tilt=20)
    def light_beam(p):
        hc.cyl((0, 0, 0.02), 0.45, 0.04, p["metal"], verts=32)
        hc.cyl((0, 0, 0.4), 0.35, 0.8, hc.glow_mat("beam", hc.hexcol("#ffe9a0"), 0.9), verts=32, r2=0.2)
    go("exit_victory", ["holy"], light_beam, size=128, scale=1.3)
    def shot(color, strength=1.5):
        def f(p):
            hc.sphere((0, 0, 0.2), 0.2, hc.glow_mat("core", hc.hexcol(color), strength))
            hc.sphere((0, 0, 0.2), 0.34, glass_mat(hc.hexcol(color)))
            hc.sphere((-0.1, -0.14, 0.36), 0.06, hc.glow_mat("hi", (1, 1, 1), 2))
        return f
    go("shot_player", [], shot("#8fd0ff"), size=64)
    go("shot_enemy", [], shot("#ff5a4a"), size=64)
    def puddle(color, strength):
        def f(p):
            o = hc.ico((0, 0, 0.0), 0.46, hc.glow_mat("pud", hc.hexcol(color), strength), scale=(1, 1, 0.06), subdiv=4, displace=0.06, seed=2)
        return f
    go("hazard_fire", [], lambda p: ([hc.cone((0.25 * math.cos(a), 0.25 * math.sin(a), 0.15), 0.12, 0.3, p["glow"], verts=10) for a in (0, 2.1, 4.2)],
                                     puddle("#c83a08", 1.0)(p), hc.cone((0, 0, 0.25), 0.18, 0.5, hc.glow_mat("f2", hc.hexcol("#ffb030"), 1.6), verts=12)), size=96, tilt=30)
    go("hazard_acid", [], puddle("#5aa81a", 0.8), size=96, tilt=30)
    go("hazard_spikes", ["iron"], lambda p: [hc.cone((x * 0.25, y * 0.25, 0.12), 0.06, 0.26, p["metal"], verts=10) for x in (-1, 0, 1) for y in (-1, 0, 1)], size=96, tilt=30)
    def shrine(color, top):
        def f(p):
            stone = hc.mat("st", hc.hexcol("#7a7268"), rough=0.85, noise=0.25, bump=0.5)
            hc.box((0, 0, 0.06), (0.7, 0.5, 0.12), stone, bevel=0.03)
            hc.box((0, 0.05, 0.35), (0.4, 0.3, 0.5), stone, bevel=0.04)
            g = hc.glow_mat("sg", hc.hexcol(color), 4)
            top(p, g, stone)
        return f
    go("shrine_shrine_blood_font", ["blood"], shrine("#ff3a4a", lambda p, g, s: (hc.cyl((0, 0.05, 0.62), 0.22, 0.08, s, verts=24), hc.cyl((0, 0.05, 0.665), 0.18, 0.02, g, verts=24))), size=128)
    go("shrine_shrine_crossroads", ["stone"], shrine("#9ad0ff", lambda p, g, s: (hc.box((0, 0.05, 0.75), (0.08, 0.08, 0.3), s), hc.box((0, 0.05, 0.8), (0.3, 0.06, 0.06), s), hc.sphere((0, -0.12, 0.45), 0.05, g))), size=128)
    go("shrine_shrine_gilded_idol", ["gold"], shrine("#ffd040", lambda p, g, s: (hc.sphere((0, 0.05, 0.75), 0.15, p["metal"]), hc.sphere((0, -0.08, 0.78), 0.04, g))), size=128)
    go("shrine_shrine_reliquary_lock", ["iron", "holy"], shrine("#c0a0ff", lambda p, g, s: (hc.box((0, 0.05, 0.72), (0.3, 0.2, 0.2), p["metal"], bevel=0.02), hc.torus((0, -0.06, 0.72), 0.06, 0.015, g, rot=(R90, 0, 0)))), size=128)


def main():
    args = sys.argv[1:]
    if args[:1] == ["--builtins-only"]:
        builtins()
        return
    if not args:
        builtins()
    for f in glob.glob(os.path.join(CONTENT, "Items", "*.json")):
        for it in json.load(open(f))["items"]:
            if args and it["id"] not in args:
                continue
            render_item(it)
            print("item", it["id"], flush=True)


if __name__ == "__main__":
    main()
