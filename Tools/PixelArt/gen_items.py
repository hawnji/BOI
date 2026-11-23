"""Pixel-art item icons (24x24) and built-in world objects (pickups, bombs, shots, pedestal, exits, hazards,
shrines), several of them animated.

Usage: python Tools/PixelArt/gen_items.py [--builtins-only] [item_id ...]
An item's object comes from the words in its name (ring, key, jar, bell, boots, feather, lantern...), its colours
from its name and tags (ember -> fire, frost -> ice, gilded -> gold...). Deterministic per id.
"""
import glob
import json
import math
import os
import sys
import zlib

sys.path.insert(0, os.path.dirname(__file__))
import px  # noqa: E402

CONTENT = os.path.join(px.REPO, "Assets", "StreamingAssets", "Content")
I = 24  # icon size
GOLD, IRON, WOOD, BONE = "#e2b040", "#9aa0aa", "#8a5c34", "#e8dcc0"


def palette(words, tags):
    w = set(words) | set(tags)
    base, glow, metal = "#9a6a48", "#ffd27a", "#c8a060"
    def has(*ks):
        return any(k in w for k in ks)
    if has("ember", "cinder", "fire", "pyre", "ash", "ashen", "burning", "burnt", "brazier", "bonfire", "napalm", "kindling", "coal", "comet"):
        base, glow = "#6a3424", "#ff8a2a"
    if has("frost", "frozen", "rime", "winter", "hoarfrost", "glacier", "cold", "ice", "snow"):
        base, glow, metal = "#9cc8e4", "#c8f6ff", "#c8d8e8"
    if has("rot", "plague", "venom", "toxic", "poison", "bile", "nettle", "spore"):
        base, glow = "#5a7a2a", "#b6ff5a"
    if has("storm", "thunder", "lightning", "static", "spark", "battery"):
        base, glow, metal = "#3e5078", "#ffe85a", "#a8b4c8"
    if has("blood", "heart", "vampire", "leech", "red"):
        base, glow = "#a01e2a", "#ff6a7a"
    if has("void", "shadow", "night", "dread", "nightmare", "umbral", "gloom", "echo", "wraith", "ghost"):
        base, glow, metal = "#4a3466", "#c890ff", "#7a6a9a"
    if has("holy", "blessed", "sacred", "saint", "halo", "dawn", "sunlight", "prayer", "guardian", "ward"):
        base, glow, metal = "#efe4c8", "#fff2b0", "#e0b850"
    if has("gold", "gilded", "coin", "miser", "penny", "purse", "wallet", "tithe", "merchant", "allowance", "lucky", "gambler", "bargain", "haggler"):
        metal, glow = GOLD, "#ffe27a"
    if has("iron", "anvil", "lead", "nail", "pewter", "tin"):
        metal = IRON
    if has("brass"):
        metal = "#c8a050"
    if has("copper"):
        metal = "#c87a4a"
    if has("bone", "skeleton", "rattle", "marrow", "ossuary", "tooth", "fang", "grave", "rabbit"):
        base = "#e2d6bc"
    if has("glass", "mirror", "prism", "lens", "crystal", "shard", "marble"):
        base = "#8fd0dc"
    if has("powder", "bomb", "keg", "fuse", "blasting", "demolition", "cluster", "mortar", "firecracker", "sapper"):
        base, glow = "#34343c", "#ffb040"
    if has("thorn", "bramble", "seed", "root", "moss", "herb", "verdant", "heartroot", "four-leaf", "toad"):
        base = "#4e7a34"
    if has("bark", "twig", "splinter", "staff", "wooden"):
        base = "#7a5232"
    return dict(base=px.ramp(base), metal=px.ramp(metal, spread=0.5), glow=px.ramp(glow, spread=0.5),
                wood=px.ramp(WOOD), bone=px.ramp(BONE, spread=0.35), dark=px.ramp("#2a2430", spread=0.3),
                paper=px.ramp("#e6d6a8", spread=0.3), glass=px.ramp("#bfe8f0", spread=0.4))


C = I / 2

# --------------------------------------------------------------------------- objects (24x24)


def o_ring(c, p):
    c.ellipse(C, C + 2, 7, 6, p["metal"]); c.ellipse(C, C + 3, 4.2, 3.4, [(0, 0, 0, 0)] * 3, dither=False)
    c.a[(c.a[:, :, 0] == 0) & (c.a[:, :, 1] == 0) & (c.a[:, :, 2] == 0)] = (0, 0, 0, 0)
    c.ellipse(C, C - 4, 3.2, 3, p["glow"], dither=False); c.px(C - 1, C - 5, (255, 255, 255, 255))


def o_amulet(c, p):
    for i in range(9):
        a = math.pi * (1.1 + 0.8 * i / 8)
        c.px(C + 7 * math.cos(a), C - 2 + 6 * math.sin(a), p["metal"][3])
    c.ellipse(C, C + 3, 5.5, 6, p["metal"]); c.ellipse(C, C + 3, 3.2, 3.6, p["glow"], dither=False)
    c.px(C - 1, C + 1, (255, 255, 255, 255))


def o_key(c, p):
    c.ellipse(C - 5, C - 4, 4, 4, p["metal"]); c.ellipse(C - 5, C - 4, 1.6, 1.6, [(0, 0, 0, 0)] * 3, dither=False)
    for i in range(12):
        c.px(C - 2 + i * 0.75, C - 1 + i * 0.75, p["metal"][3]); c.px(C - 1 + i * 0.75, C - 1 + i * 0.75, p["metal"][2])
    c.rect(C + 4, C + 5, C + 5, C + 7, p["metal"][2]); c.rect(C + 6, C + 4, C + 7, C + 6, p["metal"][2])


def o_coin(c, p):
    for (x, y) in ((C - 3, C + 4), (C + 3, C + 2), (C, C - 1)):
        c.ellipse(x, y, 5, 4, px.ramp(GOLD, spread=0.5), dither=False)
        c.ellipse(x, y, 2.5, 1.8, px.ramp("#ffe9a0"), dither=False)


def o_bottle(c, p):
    c.ellipse(C, C + 3, 7, 7, p["glass"])
    c.ellipse(C, C + 4.5, 5.6, 5, p["glow"])
    c.rect(C - 2, C - 7, C + 2, C - 3, p["glass"][2]); c.rect(C - 2, C - 9, C + 2, C - 8, p["wood"][2])
    c.px(C - 3, C + 1, (255, 255, 255, 255)); c.px(C - 4, C + 2, (255, 255, 255, 255))


def o_jar(c, p):
    c.box(C - 7, C - 4, C + 7, C + 9, p["glass"], top=1)
    c.rect(C - 6, C + 1, C + 6, C + 8, p["base"][2]); c.rect(C - 6, C + 1, C + 6, C + 1, p["base"][4])
    c.box(C - 8, C - 7, C + 8, C - 4, p["metal"], top=1)
    c.px(C - 5, C - 2, (255, 255, 255, 255))


def o_bell(c, p):
    pts = [(C - 3, C - 7), (C + 3, C - 7), (C + 7, C + 5), (C - 7, C + 5)]
    c.poly(pts, p["metal"], tone=3)
    c.ellipse(C, C - 7, 3.5, 2.2, p["metal"], dither=False)
    c.rect(C - 8, C + 5, C + 8, C + 6, p["metal"][1]); c.ellipse(C, C + 8, 1.6, 1.6, p["dark"], dither=False)
    c.line(C - 3, C - 5, C - 5, C + 3, p["metal"][4])


def o_drum(c, p):
    c.box(C - 8, C - 3, C + 8, C + 7, p["base"], top=1)
    c.ellipse(C, C - 3, 8, 3, p["paper"], dither=False)
    for x in range(-7, 8, 3):
        c.line(C + x, C - 1, C + x + 2, C + 6, p["metal"][3])
    c.line(C + 4, C - 9, C + 9, C - 4, p["wood"][3])


def o_boots(c, p):
    for sx, dy in ((-4, 0), (4, 1)):
        c.box(C + sx - 3, C - 7 + dy, C + sx + 2, C + 4 + dy, p["base"], top=1)
        c.box(C + sx - 3, C + 3 + dy, C + sx + 5, C + 7 + dy, p["base"], top=1)
        c.rect(C + sx - 3, C + 7 + dy, C + sx + 5, C + 8 + dy, p["dark"][1])


def o_cloth(c, p):
    c.poly([(C - 8, C - 6), (C + 8, C - 6), (C + 10, C + 8), (C + 4, C + 6), (C, C + 9), (C - 5, C + 6), (C - 10, C + 8)], p["base"], tone=2)
    c.rect(C - 8, C - 6, C + 8, C - 5, p["base"][4])
    for x in (-4, 0, 4):
        c.line(C + x, C - 4, C + x * 1.4, C + 6, p["base"][1])
    c.ellipse(C, C - 6, 2, 2, p["glow"], dither=False)


def o_mask(c, p):
    c.ellipse(C, C, 8, 9, p["base"])
    for sx in (-3, 3):
        c.ellipse(C + sx, C - 1, 2.2, 1.6, [px.OUTLINE] * 3, dither=False)
        c.px(C + sx, C - 1, p["glow"][4])
    c.rect(C - 2, C + 5, C + 2, C + 5, px.OUTLINE)


def o_feather(c, p):
    for i in range(14):
        x, y = C - 6 + i * 0.85, C + 8 - i * 1.15
        w = 2.4 * math.sin(math.pi * i / 14) + 0.6
        c.ellipse(x, y, w + 0.5, 1.1, p["base"], dither=False)
    c.line(C - 7, C + 10, C + 6, C - 8, p["bone"][4])


def o_tooth(c, p):
    c.poly([(C - 6, C - 6), (C + 6, C - 6), (C + 3, C + 9), (C, C + 4), (C - 3, C + 9)], p["bone"], tone=3)
    c.ellipse(C, C - 6, 6.5, 3.5, p["bone"], dither=False)
    c.line(C - 4, C - 4, C - 2, C + 6, p["bone"][4])


def o_bone(c, p):
    for i in range(13):
        c.ellipse(C - 6 + i, C + 5 - i * 0.85, 1.6, 1.6, p["bone"], dither=False)
    for (x, y) in ((C - 8, C + 5), (C - 6, C + 8), (C + 7, C - 6), (C + 5, C - 8)):
        c.ellipse(x, y, 2.4, 2.4, p["bone"], dither=False)


def o_shell(c, p):
    for i in range(18):
        t = i / 17
        a = t * 3.6 * math.pi
        r = 1 + 7 * (1 - t)
        c.ellipse(C + r * math.cos(a) * 0.9, C + 1 + r * math.sin(a) * 0.8, 4.5 * (1 - t) + 1.2, 4.5 * (1 - t) + 1.2, p["base"], dither=False)
    c.px(C - 3, C - 3, (255, 245, 230, 255))


def o_heart(c, p, half=False):
    lobes = (-3.5,) if half else (-3.5, 3.5)
    for x in lobes:
        c.ellipse(C + x, C - 2, 4.6, 4.4, p["base"], dither=False)
    pts = [(C - 8, C - 1), (C + (0 if half else 8), C - 1), (C, C + 8)]
    if half:
        pts = [(C - 8, C - 1), (C, C - 1), (C, C + 8)]
    c.poly(pts, p["base"], tone=2)
    c.px(C - 5, C - 4, (255, 255, 255, 255)); c.px(C - 4, C - 4, p["base"][4]); c.px(C - 5, C - 3, p["base"][4])


def o_stone(c, p):
    c.ellipse(C, C + 1, 8, 7, p["base"])
    c.line(C - 3, C - 2, C + 3, C + 4, p["glow"][4]); c.line(C + 3, C - 2, C - 3, C + 4, p["glow"][4])
    c.px(C, C + 1, (255, 255, 255, 255))


def o_orb(c, p):
    c.box(C - 5, C + 5, C + 5, C + 8, p["metal"], top=1)
    c.ellipse(C, C - 1, 7, 7, p["glass"])
    c.ellipse(C, C - 1, 3.5, 3.5, p["glow"], dither=False)
    c.px(C - 3, C - 4, (255, 255, 255, 255)); c.px(C - 2, C - 4, (255, 255, 255, 255))


def o_plant(c, p):
    c.line(C, C + 9, C, C - 2, px.ramp("#3e6a2a")[2])
    for (dx, dy, sx) in ((-5, 1, -1), (5, -1, 1), (-4, -5, -1), (4, -7, 1)):
        c.ellipse(C + dx, C + dy, 3.4, 1.8, px.ramp("#5e9a3a"), dither=False)
    c.ellipse(C, C - 4, 2.4, 2.4, p["glow"], dither=False)
    c.ellipse(C, C + 9, 4, 1.4, p["wood"], dither=False)


def o_crystal(c, p):
    for (dx, h, w) in ((-4, 9, 3), (4, 8, 3), (0, 12, 4)):
        c.poly([(C + dx - w, C + 8), (C + dx - w, C + 8 - h + 3), (C + dx, C + 8 - h), (C + dx + w, C + 8 - h + 3), (C + dx + w, C + 8)], p["base"], tone=3)
        c.line(C + dx - w + 1, C + 7, C + dx - w + 1, C + 8 - h + 4, p["base"][4])
    c.px(C, C - 1, (255, 255, 255, 255))


def o_lantern(c, p):
    c.rect(C - 1, C - 10, C + 1, C - 9, p["metal"][1])
    c.box(C - 5, C - 8, C + 5, C - 6, p["metal"], top=1)
    c.rect(C - 4, C - 5, C + 4, C + 6, p["glow"][3]); c.rect(C - 2, C - 3, C + 2, C + 4, p["glow"][4])
    for x in (C - 5, C, C + 5):
        c.line(x, C - 5, x, C + 6, p["metal"][2])
    c.box(C - 6, C + 7, C + 6, C + 9, p["metal"], top=1)


def o_candle(c, p):
    c.box(C - 3, C - 3, C + 3, C + 8, p["paper"], top=1)
    c.ellipse(C, C + 9, 7, 1.8, p["metal"], dither=False)
    c.ellipse(C, C - 7, 1.8, 3, p["glow"], dither=False); c.px(C, C - 5, (255, 255, 230, 255))
    c.px(C + 2, C - 1, p["paper"][4]); c.px(C + 2, C, p["paper"][4])


def o_book(c, p):
    c.box(C - 8, C - 7, C + 7, C + 8, p["base"], top=1)
    c.rect(C + 6, C - 6, C + 8, C + 8, p["paper"][3])
    c.rect(C - 8, C - 7, C - 6, C + 8, p["base"][1])
    c.ellipse(C, C, 3, 3, p["glow"], dither=False)
    c.rect(C - 5, C + 5, C + 4, C + 5, p["metal"][3])


def o_scroll(c, p):
    c.rect(C - 7, C - 6, C + 7, C + 6, p["paper"][3])
    for y in (C - 7, C + 6):
        c.ellipse(C, y, 8.5, 1.8, p["paper"], dither=False)
    for y in (C - 3, C, C + 3):
        c.rect(C - 5, y, C + 4 - (y % 3), y, p["dark"][2])
    c.ellipse(C + 4, C + 4, 1.8, 1.8, px.ramp("#b02a2a"), dither=False)


def o_blade(c, p):
    for i in range(13):
        c.px(C - 4 + i, C + 4 - i, p["metal"][4]); c.px(C - 4 + i, C + 5 - i, p["metal"][2]); c.px(C - 3 + i, C + 5 - i, p["metal"][1])
    c.line(C - 8, C + 2, C - 2, C + 8, p["metal"][2])
    c.line(C - 9, C + 9, C - 5, C + 5, p["wood"][2])


def o_needles(c, p):
    for k in range(4):
        x = C - 6 + k * 4
        c.line(x, C + 8, x + 3, C - 8, p["metal"][3 if k % 2 else 4])


def o_bow(c, p):
    for i in range(17):
        t = -1 + i / 8
        c.px(C - 4 + 6 * (1 - t * t), C + t * 9, p["wood"][3]); c.px(C - 5 + 6 * (1 - t * t), C + t * 9, p["wood"][1])
    c.line(C - 4, C - 9, C - 4, C + 9, p["paper"][4])
    c.line(C - 6, C, C + 9, C, p["metal"][3]); c.px(C + 9, C, p["metal"][4])


def o_eye(c, p):
    c.ellipse(C, C, 8, 6, p["bone"])
    c.ellipse(C, C, 4, 4, p["glow"], dither=False); c.ellipse(C, C, 1.8, 1.8, [px.OUTLINE] * 3, dither=False)
    c.px(C - 2, C - 2, (255, 255, 255, 255))


def o_crown(c, p):
    c.box(C - 8, C, C + 8, C + 6, p["metal"], top=1)
    for k, dx in enumerate((-8, -4, 0, 4, 8)):
        c.poly([(C + dx - 2, C), (C + dx, C - 6 - (k % 2) * 2), (C + dx + 2, C)], p["metal"], tone=3)
    for dx in (-4, 0, 4):
        c.px(C + dx, C + 3, p["glow"][4])


def o_halo(c, p):
    c.ellipse(C, C, 9, 5, p["glow"], dither=False); c.ellipse(C, C, 6, 2.8, [(0, 0, 0, 0)] * 3, dither=False)
    c.a[(c.a[:, :, 3] == 0)] = (0, 0, 0, 0)


def o_bomb(c, p):
    c.ellipse(C, C + 2, 7.5, 7.5, px.ramp("#3a3a44", spread=0.5))
    c.rect(C - 2, C - 7, C + 2, C - 5, p["metal"][2])
    c.line(C, C - 8, C + 3, C - 10, p["paper"][3]); c.px(C + 4, C - 11, p["glow"][4])
    c.px(C - 3, C - 1, (220, 220, 230, 255)); c.px(C - 4, C, (160, 160, 170, 255))


def o_keg(c, p):
    c.ellipse(C, C + 1, 7.5, 9, p["wood"])
    for y in (C - 4, C + 5):
        c.rect(C - 7, y, C + 7, y, p["metal"][3])
    c.box(C - 3, C - 1, C + 3, C + 3, p["glow"], top=1)


def o_coil(c, p):
    for k in range(6):
        c.ellipse(C, C + 7 - k * 3, 6, 1.8, p["metal"], dither=False)
    c.ellipse(C, C - 9, 2.4, 2.4, p["glow"], dither=False)


def o_bag(c, p):
    c.ellipse(C, C + 3, 8, 7, p["base"])
    c.poly([(C - 3, C - 4), (C + 3, C - 4), (C + 4, C - 8), (C - 4, C - 8)], p["base"], tone=3)
    c.rect(C - 4, C - 4, C + 4, C - 3, p["metal"][3])
    c.ellipse(C, C + 4, 2, 2, p["glow"], dither=False)


def o_box(c, p):
    c.box(C - 9, C - 3, C + 9, C + 8, p["wood"], top=1)
    c.box(C - 9, C - 7, C + 9, C - 3, p["wood"][1:] + [p["wood"][-1]], top=1)
    c.rect(C - 9, C - 3, C + 9, C - 3, p["metal"][2])
    c.box(C - 2, C - 4, C + 2, C, p["metal"], top=1)


def o_horn(c, p):
    for i in range(14):
        t = i / 13
        c.ellipse(C - 8 + t * 14, C + 4 - 9 * math.sin(t * 1.6), 0.8 + 3 * t, 0.8 + 3 * t, p["bone"], dither=False)
    c.ellipse(C + 6, C - 4, 2, 3, p["dark"], dither=False)


def o_glove(c, p):
    c.ellipse(C, C + 2, 6.5, 6, p["base"])
    for k in range(4):
        c.ellipse(C - 4.5 + k * 3, C - 5, 1.5, 3, p["base"], dither=False)
    c.ellipse(C - 7, C + 1, 2, 3, p["base"], dither=False)
    c.box(C - 6, C + 6, C + 6, C + 9, p["metal"], top=1)


def o_critter(c, p):
    c.ellipse(C, C + 3, 7, 6, p["base"])
    c.ellipse(C, C - 4, 5, 4.5, p["base"])
    for sx in (-5, 5):
        c.ellipse(C + sx, C + 1, 3.5, 2, p["base"][1:] + [p["base"][-1]], dither=False)
    for sx in (-2, 2):
        c.rect(C + sx - (1 if sx < 0 else 0), C - 5, C + sx - (1 if sx < 0 else 0), C - 4, p["glow"][4])


def o_die(c, p):
    c.box(C - 7, C - 7, C + 7, C + 7, p["bone"], top=2)
    for (x, y) in ((-3, -3), (3, 3), (0, 0), (3, -3), (-3, 3)):
        c.px(C + x, C + y, px.OUTLINE)


def o_hourglass(c, p):
    c.box(C - 7, C - 10, C + 7, C - 8, p["wood"], top=1); c.box(C - 7, C + 8, C + 7, C + 10, p["wood"], top=1)
    c.poly([(C - 5, C - 8), (C + 5, C - 8), (C, C)], p["glass"], tone=3)
    c.poly([(C - 5, C + 8), (C + 5, C + 8), (C, C)], p["glass"], tone=3)
    c.poly([(C - 3, C + 8), (C + 3, C + 8), (C, C + 4)], p["glow"], tone=3)


def o_food(c, p):
    crust = px.ramp("#b47a3a")
    c.ellipse(C, C + 1, 9, 6, crust)
    for dx in (-4, 0, 4):
        c.line(C + dx - 1, C - 2, C + dx + 1, C + 2, crust[4])


VOCAB = [
    (o_food, ["bread", "pork", "stew", "meat"]),
    (o_ring, ["ring", "band"]), (o_key, ["key", "keymaker"]), (o_coin, ["coin", "penny", "allowance", "tally", "flip"]),
    (o_bottle, ["vial", "flask", "tonic", "draught", "water", "wine", "salve", "sap", "honey", "potion"]),
    (o_jar, ["jar", "bowl", "cup", "bucket", "socket", "censer", "pot"]), (o_bell, ["bell"]), (o_drum, ["drum"]),
    (o_boots, ["boots", "heel", "wrap", "quickstep"]), (o_cloth, ["cloak", "mantle", "shroud", "veil", "rag", "bandage", "hood", "lint", "pocket", "pockets", "mail", "lining"]),
    (o_mask, ["mask", "visor", "face"]), (o_feather, ["feather", "quill"]), (o_tooth, ["tooth", "fang", "claw"]),
    (o_bone, ["bone", "rattle", "flute", "wishing"]), (o_shell, ["shell", "snail", "tortoise"]), (o_heart, ["heart", "locket", "heartroot", "pride"]),
    (o_orb, ["pearl", "marble", "orbit", "moon", "core", "scrying"]), (o_eye, ["eye", "lens", "spyglass", "sight", "sentinel"]),
    (o_crystal, ["shard", "chip", "prism", "glass", "crystal", "rime", "splinter"]), (o_lantern, ["lantern", "lamp"]),
    (o_candle, ["candle", "wick", "kindling"]), (o_book, ["book", "hymnal", "ledger", "tome"]), (o_scroll, ["map", "slip", "seal", "ink", "scrap"]),
    (o_blade, ["blade", "knife", "knives", "sickle", "edge", "lance", "whetstone", "flint", "whetted", "mark"]), (o_needles, ["needle", "nail", "thorn", "thorns", "pin", "spite", "quill"]),
    (o_bow, ["bow"]), (o_crown, ["crown"]), (o_halo, ["halo"]), (o_bomb, ["bomb", "bombs", "powder", "firecracker", "fuse", "cap", "charge", "shot", "burst"]),
    (o_keg, ["keg", "satchel", "vest", "corpse"]), (o_coil, ["coil", "spring", "wire", "rod", "plug", "battery"]),
    (o_bag, ["bag", "sack", "pouch", "purse", "wallet", "kit"]), (o_box, ["box", "chest"]), (o_horn, ["horn", "whistle"]),
    (o_glove, ["glove", "knuckles", "knuckle", "arm", "fist"]), (o_die, ["die", "dice", "loaded"]), (o_hourglass, ["sand", "hourglass"]),
    (o_stone, ["stone", "pebble", "bead", "rune", "sigil", "charm", "amulet", "tear", "salt", "ash"]),
    (o_plant, ["seed", "pod", "bloom", "herb", "root", "bark", "twig", "moss", "nettle", "stool", "puffball", "four-leaf", "spore", "sac", "gland"]),
    (o_critter, ["moth", "rat", "crow", "sparrow", "beetle", "tick", "leech", "wisp", "hound", "magpie", "imp", "goblin", "mole", "wren", "flock", "mite", "hive", "swarm", "toad", "owl", "raven"]),
]


def pick(name, seed):
    words = name.lower().replace("'s", "").split()
    for word in reversed(words):
        for fn, keys in VOCAB:
            if word in keys:
                return fn
    low = name.lower()
    for fn, keys in VOCAB:
        if any(k in low for k in keys):
            return fn
    return [o_amulet, o_stone, o_orb, o_ring][seed % 4]


def icon(fn, p, size=I):
    c = px.Canvas(size, size)
    fn(c, p)
    c.outline()
    return c


def render_item(it):
    words = it["name"].lower().replace("'s", "").split()
    p = palette(words, it.get("tags", []))
    fn = pick(it["name"], zlib.crc32(it["id"].encode()))
    key = it.get("iconKey") or "item_" + it["id"]
    px.save(icon(fn, p), os.path.join(px.SPRITES, key + ".png"))


# --------------------------------------------------------------------------- built-ins (several animated)

def strip(key, frames):
    px.save_strip(frames, os.path.join(px.SPRITES, key + ".png"))
    print("builtin", key, flush=True)


def anim(size, n, draw):
    out = []
    for f in range(n):
        c = px.Canvas(size, size)
        draw(c, f)
        c.outline()
        out.append(c)
    return out


def builtins():
    gold = px.ramp(GOLD, spread=0.5)
    def coin(c, f):
        w = (6, 4, 1.5, 4)[f]
        c.shadow(10, 17, 5, 1.5)
        c.ellipse(10, 9, w, 6, gold, dither=False)
        if w > 2:
            c.ellipse(10, 9, w * 0.5, 3, px.ramp("#ffeaa0"), dither=False)
    strip("pickup_coin", anim(20, 4, coin))

    def heart(color, half=False):
        def d(c, f):
            p = dict(base=px.ramp(color))
            k = px.Canvas(24, 24)
            o_heart(k, p, half)
            c.shadow(12, 21, 6, 1.5)
            c.paste(k, 0, -1 if f in (1, 2) else 0)
        return d
    strip("pickup_vigorfull", anim(24, 4, heart("#d0283a")))
    strip("pickup_vigorhalf", anim(24, 4, heart("#d0283a", True)))
    strip("pickup_ward", anim(24, 4, heart("#7ab0e8")))
    strip("pickup_umbral", anim(24, 4, heart("#4a2e6a")))

    def key(c, f):
        p = palette(["iron"], [])
        p["metal"] = px.ramp("#c8ccd4", spread=0.5)
        o_key(c, p)
        if f == 1:
            c.px(5, 3, (255, 255, 255, 255)); c.px(4, 4, (255, 255, 255, 255)); c.px(6, 4, (255, 255, 255, 255))
    strip("pickup_key", anim(24, 4, key))

    def bomb_pickup(c, f):
        p = palette(["bomb"], [])
        o_bomb(c, p)
    strip("pickup_bomb", anim(24, 1, bomb_pickup))

    def ember(c, f):
        p = palette(["ember"], [])
        o_crystal(c, p)
        if f % 2:
            c.px(12, 3, (255, 240, 180, 255))
    strip("pickup_ember", anim(24, 2, ember))
    strip("pickup_consumable", anim(24, 1, lambda c, f: o_stone(c, palette(["rune"], []))))
    strip("pickup_trinket", anim(24, 1, lambda c, f: o_amulet(c, palette(["charm"], []))))
    strip("pickup_item", anim(24, 1, lambda c, f: o_box(c, palette(["gold"], []))))

    def pedestal(c, f):
        st = px.ramp("#8a8078", spread=0.45)
        c.shadow(16, 28, 13, 3)
        c.box(4, 22, 27, 27, st, top=1)
        c.box(8, 12, 23, 22, st, top=1)
        c.box(5, 9, 26, 13, st, top=2)
        c.rect(9, 10, 22, 10, px.ramp("#ffe9a0")[2 + f % 2])
    strip("pedestal", anim(32, 2, pedestal))

    def bomb(c, f, tint="#3a3a44"):
        c.shadow(12, 21, 7, 2)
        c.ellipse(12, 13, 7.5, 7.5, px.ramp(tint, spread=0.5))
        c.rect(10, 4, 14, 6, px.ramp(IRON)[2])
        c.line(12, 3, 14, 1, px.ramp("#e6d6a8")[3])
        spark = [(255, 250, 200, 255), (255, 180, 60, 255), (255, 120, 40, 255), (255, 220, 120, 255)][f]
        c.px(15, 0, spark); c.px(15 + (f % 2), 1, spark); c.px(14 - (f % 2), 0, (255, 255, 255, 255))
        c.px(8, 10, (220, 220, 230, 255))
    strip("bomb", anim(24, 4, bomb))
    strip("bomb_companion", anim(24, 4, lambda c, f: bomb(c, f, "#3a4a78")))

    def trapdoor(c, f):
        wood = px.ramp(WOOD)
        c.rect(2, 2, 29, 29, (10, 8, 12, 255))
        for i in range(5):
            c.rect(4, 4 + i, 27, 4 + i, (30 - i * 4, 24 - i * 3, 30 - i * 4, 255))
        c.box(0, 0, 31, 3, wood, top=1); c.box(0, 28, 31, 31, wood, top=1)
        c.box(0, 0, 3, 31, wood, top=1); c.box(28, 0, 31, 31, wood, top=1)
    strip("exit_trapdoor", anim(32, 1, trapdoor))

    def light(c, f):
        g = px.ramp("#ffe9a0", spread=0.4)
        c.ellipse(16, 26, 12, 4, px.ramp("#c8a050"), dither=False)
        w = (8, 9, 10, 9)[f]
        c.poly([(16 - w, 26), (16 + w, 26), (16 + w - 3, 0), (16 - w + 3, 0)], g, tone=3)
        c.poly([(16 - w + 4, 26), (16 + w - 4, 26), (16 + w - 5, 0), (16 - w + 5, 0)], g, tone=4)
        for k in range(3):
            c.px(10 + k * 6, (20 - f * 5 - k * 7) % 26, (255, 255, 255, 255))
    strip("exit_victory", anim(32, 4, light))

    def shot(color):
        def d(c, f):
            g = px.ramp(color, spread=0.5)
            r = (4.6, 5, 4.6, 4.2)[f]
            c.ellipse(8, 8, r, r, g, dither=False)
            c.ellipse(8, 8, r * 0.55, r * 0.55, px.ramp("#ffffff", spread=0.2), dither=False) if f % 2 == 0 else None
            c.px(6, 6, (255, 255, 255, 255))
        return d
    strip("shot_player", anim(16, 4, shot("#7ac8ff")))
    strip("shot_enemy", anim(16, 4, shot("#ff5a4a")))

    def fire(c, f):
        fl = px.ramp("#ff7a1a", spread=0.55)
        core = px.ramp("#ffd84a", spread=0.4)
        for k, (x, h) in enumerate(((10, 10), (16, 14), (22, 9))):
            hh = h + (0, 2, 1, -1)[(f + k) % 4]
            c.poly([(x - 5, 26), (x + 5, 26), (x + 1, 26 - hh), (x - 1, 26 - hh - 2)], fl, tone=2)
            c.poly([(x - 2, 26), (x + 2, 26), (x, 26 - hh * 0.6)], core, tone=3)
        c.ellipse(16, 27, 13, 3, px.ramp("#5a2a10"), dither=False)
    strip("hazard_fire", anim(32, 4, fire))

    def acid(c, f):
        a = px.ramp("#6ac02a", spread=0.5)
        c.ellipse(16, 18, 14, 9, a, dither=False)
        c.ellipse(16, 18, 10, 6, a[1:] + [a[-1]], dither=False)
        for k in range(3):
            x, y = 9 + k * 7, 15 + (k % 2) * 4
            if (f + k) % 4 < 2:
                c.ellipse(x, y, 1.6, 1.6, px.ramp("#c8ff8a"), dither=False)
    strip("hazard_acid", anim(32, 4, acid))

    def spikes(c, f):
        m = px.ramp(IRON, spread=0.5)
        up = (0, 2, 3, 2)[f]
        for gy in range(3):
            for gx in range(3):
                x, y = 6 + gx * 10, 9 + gy * 9
                c.rect(x - 2, y + 2, x + 2, y + 3, (30, 24, 28, 255))
                for k in range(up + 2):
                    w = max(0, (up + 1 - k) // 2)
                    c.rect(x - w, y + 1 - k, x + w, y + 1 - k, m[min(4, 1 + k)])
    strip("hazard_spikes", anim(32, 4, spikes))

    def shrine(color, top):
        def d(c, f):
            st = px.ramp("#7a7268", spread=0.45)
            g = px.ramp(color, spread=0.5)
            c.shadow(16, 29, 13, 3)
            c.box(3, 24, 28, 29, st, top=1)
            c.box(8, 10, 23, 24, st, top=1)
            top(c, g, st, f)
        return d
    strip("shrine_shrine_blood_font", anim(32, 4, shrine("#e0283a", lambda c, g, st, f: (c.box(5, 6, 26, 10, st, top=1), c.rect(7, 7, 24, 8, g[2 + (f % 2)])))))
    strip("shrine_shrine_crossroads", anim(32, 4, shrine("#9ad0ff", lambda c, g, st, f: (c.box(14, 0, 17, 10, st, top=1), c.box(9, 3, 22, 5, st, top=1), c.ellipse(15.5, 17, 2, 2, g, dither=False) if f % 2 else None))))
    strip("shrine_shrine_gilded_idol", anim(32, 4, shrine("#ffd040", lambda c, g, st, f: (c.ellipse(16, 6, 5, 5, gold), c.px(15, 5, g[4]) if f < 2 else c.px(17, 5, g[4])))))
    strip("shrine_shrine_reliquary_lock", anim(32, 4, shrine("#c0a0ff", lambda c, g, st, f: (c.box(9, 3, 22, 10, px.ramp(IRON, spread=0.5), top=1), c.ellipse(15.5, 6.5, 2, 2, g, dither=False) if f % 2 == 0 else None))))


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
    print("items done", flush=True)


if __name__ == "__main__":
    main()
