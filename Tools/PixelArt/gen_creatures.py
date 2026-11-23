"""Pixel-art enemies, minibosses, bosses and companions with idle and move animations (4 frames each).

Usage: python Tools/PixelArt/gen_creatures.py [--only enemy|miniboss|boss|companion] [id ...]
Output: Assets/Resources/Sprites/<spriteKey>.png (idle strip) and <spriteKey>_move.png (move strip).
Body plan (archetype) comes from the creature's name/description/tags/movement; palette from its floor theme
and element. Sprites are drawn at 32 px per world unit, sized from the creature's collision radius.
"""
import colorsys
import glob
import json
import math
import os
import sys
import zlib

sys.path.insert(0, os.path.dirname(__file__))
import px  # noqa: E402
from gen_tiles import THEMES  # noqa: E402

CONTENT = os.path.join(px.REPO, "Assets", "StreamingAssets", "Content")

ARCHETYPES = [
    ("eye", ["eye"]),
    ("turret", ["turret", "sentry", "reliquary", "bellows", "tree", "wall", "chime", "organ", "font", "engine", "crucible", "censer"]),
    ("lantern", ["lantern"]),
    ("flyer", ["bat", "crow", "moth", "wasp", "fly", "chick", "drone", "sparrow", "magpie", "wren", "bird", "bomber", "strafer", "raven", "phoenix"]),
    ("wisp", ["wisp", "mote", "sprite", "spiral", "swirl", "shade", "wraith", "blinker", "refraction", "echo", "dancer", "spark", "flame", "ember moon", "halo", "nothing", "dark", "void", "prism", "kaleidos", "tempest", "unformed"]),
    ("blob", ["bloat", "sludge", "jelly", "mound", "puff", "mushroom", "spore", "apple", "clot", "seed mother", "bloom", "rot", "gutmaw", "heart", "ventricle", "matron", "maw", "pod", "pulse", "throb", "mother"]),
    ("hopper", ["hopper", "toad", "imp", "frog", "goblin", "rat", "mole", "beetle", "tick", "leech"]),
    ("crawler", ["crawler", "snake", "eel", "wader", "burrower", "digger", "runner", "hound", "skater", "larva", "vertebra", "lasher", "swimmer", "fish", "barnacle", "thresher", "hanger", "leviathan"]),
    ("brute", ["brute", "golem", "shambler", "giant", "beast", "smelter", "anvil", "glacier", "knight", "guard", "warden", "king", "bough", "permafrost", "pyroclasm", "ironmonger", "harvester", "gargoyle", "stone"]),
    ("humanoid", ["priest", "monk", "acolyte", "zealot", "pilgrim", "herald", "witch", "deacon", "farmer", "hag", "scarecrow", "choir", "nurse", "thrower", "reaper", "abbess", "sexton", "widow", "glazier", "gardener", "regent", "queen", "bellringer", "walker", "husk", "puppet", "saint", "one", "spitter"]),
]


def archetype(name, desc, tags, movement):
    n = name.lower()
    for arch, words in ARCHETYPES:
        if any(w in n for w in words):
            return arch
    d = desc.lower()
    for arch, words in ARCHETYPES:
        if any(" " + w in " " + d for w in words):
            return arch
    if "flying" in tags:
        return "flyer"
    if movement == "Idle":
        return "turret"
    return "brute" if "melee" in tags else "humanoid"


def theme_index():
    idx = {}
    for f in glob.glob(os.path.join(CONTENT, "Floors", "*.json")):
        d = json.load(open(f))
        for fl in d["floors"] if isinstance(d, dict) else d:
            for key in ("enemies", "bosses", "minibosses"):
                for e in fl.get(key, []):
                    eid = e if isinstance(e, str) else e.get("enemyId") or e.get("bossId") or e.get("id")
                    idx.setdefault(eid, fl["theme"])
    return idx


def load():
    out = []
    themes = theme_index()
    for f in glob.glob(os.path.join(CONTENT, "Enemies", "*.json")):
        d = json.load(open(f))
        for e in d.get("enemies", []):
            key = e.get("spriteKey") or e["id"]
            out.append(dict(kind="miniboss" if key.startswith("miniboss_") else "enemy", id=e["id"], key=key, name=e["name"],
                            desc=e.get("description", ""), tags=e.get("tags", []), move=e.get("movement", {}).get("kind", ""),
                            radius=e.get("radius", 0.4), theme=themes.get(e["id"], "crypt")))
        for b in d.get("bosses", []):
            body = b["body"]
            key = body.get("spriteKey") or b["id"]
            out.append(dict(kind="miniboss" if key.startswith("miniboss_") else "boss", id=b["id"], key=key, name=b["name"],
                            desc=b.get("description", ""), tags=body.get("tags", []), move=body.get("movement", {}).get("kind", ""),
                            radius=body.get("radius", 1.0), theme=themes.get(b["id"], "crypt")))
    for f in glob.glob(os.path.join(CONTENT, "Companions", "*.json")):
        for c in json.load(open(f))["companions"]:
            out.append(dict(kind="companion", id=c["id"], key=c.get("spriteKey") or "companion_" + c["id"], name=c["name"],
                            desc=c.get("description", ""), tags=[], move=c.get("behavior", ""), radius=0.25, theme=None))
    return out


# --------------------------------------------------------------------------- palette

def shift(hexv, dh=0.0, ds=0.0, dv=0.0):
    r, g, b = (int(hexv[i:i + 2], 16) / 255 for i in (1, 3, 5))
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    r, g, b = colorsys.hsv_to_rgb((h + dh) % 1, min(1, max(0, s + ds)), min(1, max(0, v + dv)))
    return "#%02x%02x%02x" % (int(r * 255), int(g * 255), int(b * 255))


COMPANION = dict(stone="#9a8f7a", accent="#6fa0c8", glow="#bff4ff")


def palette(c):
    t = THEMES.get(c["theme"]) if c["theme"] else COMPANION
    r = px.rng(zlib.crc32(c["id"].encode()))
    body = shift(t["stone"], r.uniform(-0.08, 0.08), r.uniform(0.1, 0.3), r.uniform(-0.02, 0.12))
    accent = shift(t["accent"], r.uniform(-0.05, 0.05), 0.15, 0.08)
    glow = t["glow"]
    words = set(c["tags"]) | set(c["name"].lower().replace("'", "").split())
    if words & {"fire", "ember", "cinder", "pyre", "ash", "flame", "slag", "magma", "phoenix", "smelter", "forge", "brand"}:
        glow, body = "#ff8a2a", shift("#6a3a2c", r.uniform(-0.03, 0.03), 0.1, 0.05)
    if words & {"frost", "ice", "rime", "snow", "icicle", "blizzard", "glacier", "hoarfrost", "winter", "permafrost"}:
        glow, body = "#a8ecff", shift("#a8c8dc", r.uniform(-0.03, 0.03))
    if words & {"poison", "rot", "bile", "sludge", "spore", "mire", "bog", "mushroom", "puff"}:
        glow = "#b6ff5a"
    if words & {"storm", "lightning", "thunder", "spark", "static", "tempest"}:
        glow = "#ffe85a"
    if words & {"undead", "bone", "skull", "rib", "marrow", "knuckle", "vertebra", "ossuary", "osseous"}:
        body = shift("#d2c4a2", r.uniform(-0.02, 0.02), 0, r.uniform(-0.05, 0.05))
    if words & {"void", "umbral", "shade", "null", "abyss", "gloom", "dark", "nothing", "rift"}:
        body, glow = shift("#4a3a66", r.uniform(-0.04, 0.04)), "#c890ff"
    return dict(body=px.ramp(body, 5), dark=px.ramp(shift(body, 0, 0.05, -0.25), 5), accent=px.ramp(accent, 5),
                glow=px.ramp(glow, 5, spread=0.5), bone=px.ramp("#e6dac0", 5, spread=0.35),
                white=px.ramp("#f2ece2", 4, spread=0.3), rng=r)


# --------------------------------------------------------------------------- drawing helpers

def eyes(c, x, y, p, gap, size=1, glow=True, look=0):
    """Two glowing eyes in dark sockets (always readable on light or dark bodies)."""
    size = max(2, int(size))
    for ex in (x - gap - size // 2, x + gap - size // 2 + 1):
        ex, yy = int(round(ex)), int(round(y))
        c.rect(ex - 1, yy - size, ex + size, yy + 1, px.OUTLINE)
        c.rect(ex, yy - size + 1, ex + size - 1, yy, p["glow"][3])
        c.px(ex, yy - size + 1, p["glow"][4])


def eyeball(c, x, y, r, p, look=(0, 0)):
    c.ellipse(x, y, r, r, p["white"])
    ix, iy = x + look[0] * r * 0.35, y + look[1] * r * 0.3 + r * 0.15
    c.ellipse(ix, iy, r * 0.5, r * 0.5, p["glow"], dither=False)
    c.ellipse(ix, iy, r * 0.22, r * 0.22, [px.OUTLINE] * 3, dither=False)
    c.px(ix - r * 0.25, iy - r * 0.25, (255, 255, 255, 255))


# --------------------------------------------------------------------------- body plans  (u = size unit)

def a_eye(c, S, p, f, moving, big):
    u = S / 32
    cx, cy = S / 2, S / 2 - 1 + (0, -1, -2, -1)[f]
    c.shadow(cx, S - 4 * u, 8 * u, 2.4 * u)
    r = p["rng"]
    for i in range(6):  # tendrils
        a = i * math.pi / 3 + 0.4 + 0.15 * math.sin(f * math.pi / 2 + i)
        c.line(cx + 9 * u * math.cos(a), cy + 9 * u * math.sin(a), cx + 13 * u * math.cos(a), cy + 13 * u * math.sin(a), p["dark"][2])
    look = [(0, 0), (1, 0), (0, 1), (-1, 0)][f]
    eyeball(c, cx, cy, 10 * u, p, look)
    for i in range(3):  # veins
        a = r.uniform(0, 2 * math.pi)
        c.line(cx + 6 * u * math.cos(a), cy + 6 * u * math.sin(a), cx + 9.5 * u * math.cos(a), cy + 9.5 * u * math.sin(a), (190, 40, 50, 255))
    c.ellipse(cx, cy - 7 * u, 9 * u, 3.5 * u, p["body"])  # lid


def a_turret(c, S, p, f, moving, big):
    u = S / 32
    cx = S / 2
    c.shadow(cx, S - 5 * u, 12 * u, 3 * u)
    c.box(cx - 11 * u, S - 11 * u, cx + 11 * u, S - 5 * u, p["dark"], top=max(1, int(u)))
    c.ellipse(cx, S / 2 + 1 * u, 9 * u, 10 * u, p["body"])
    c.rect(cx - 9 * u, S / 2 + 2 * u, cx + 9 * u, S / 2 + 3 * u, p["accent"][2])
    pulse = (1, 2, 3, 2)[f]
    c.ellipse(cx, S / 2 - 1 * u, 4.5 * u, 4.5 * u, [px.OUTLINE] * 3, dither=False)
    c.ellipse(cx, S / 2 - 1 * u, (1.6 + pulse * 0.8) * u, (1.6 + pulse * 0.8) * u, p["glow"], dither=False)
    for sx in (-1, 1):
        c.ellipse(cx + sx * 9.5 * u, S / 2 + 2 * u, 2 * u, 2 * u, p["accent"], dither=False)


def a_lantern(c, S, p, f, moving, big):
    u = S / 32
    sw = (0, 1, 0, -1)[f] * u
    cx = S / 2 + sw
    c.line(S / 2, 0, cx, 6 * u, (50, 46, 52, 255))
    c.rect(cx - 3 * u, 6 * u, cx + 3 * u, 8 * u, p["accent"][2])
    c.box(cx - 7 * u, 8 * u, cx + 7 * u, 24 * u, p["accent"], top=max(1, int(u)))
    fl = (3, 4, 3, 4)[f]
    c.rect(cx - 5 * u, 10 * u, cx + 5 * u, 22 * u, p["glow"][fl - 1])
    c.ellipse(cx, 16 * u, 3.5 * u, 4 * u, p["bone"], dither=False)
    c.rect(cx - 2 * u, 15 * u, cx - 1 * u, 16 * u, px.OUTLINE); c.rect(cx + 1 * u, 15 * u, cx + 2 * u, 16 * u, px.OUTLINE)
    for x in (cx - 2.5 * u, cx + 2.5 * u):
        c.line(x, 10 * u, x, 22 * u, p["accent"][1])
    c.rect(cx - 8 * u, 24 * u, cx + 8 * u, 25 * u, p["accent"][1])
    c.shadow(S / 2, S - 3 * u, 6 * u, 1.8 * u)


def a_flyer(c, S, p, f, moving, big):
    u = S / 32
    cx, cy = S / 2, S / 2 + (0, -1, -2, -1)[f] * u
    c.shadow(cx, S - 3 * u, 7 * u, 2 * u)
    wing = (-5, -1, 3, -1)[f] * u
    for sx in (-1, 1):
        pts = [(cx + sx * 4 * u, cy - 1 * u), (cx + sx * 15 * u, cy + wing - 3 * u), (cx + sx * 13 * u, cy + wing + 3 * u),
               (cx + sx * 9 * u, cy + wing * 0.5 + 2 * u), (cx + sx * 4 * u, cy + 3 * u)]
        c.poly(pts, p["dark"], tone=2)
        c.line(cx + sx * 4 * u, cy - 1 * u, cx + sx * 15 * u, cy + wing - 3 * u, p["dark"][4])
    c.ellipse(cx, cy, 6 * u, 6.5 * u, p["body"])
    for sx in (-1, 1):
        c.poly([(cx + sx * 2 * u, cy - 5 * u), (cx + sx * 4.5 * u, cy - 9 * u), (cx + sx * 5 * u, cy - 4 * u)], p["body"], tone=2)
    eyes(c, cx, cy - 1 * u, p, max(2, int(2.5 * u)), size=max(2, int(1.3 * u)))
    c.px(cx, cy + 2 * u, p["bone"][4]); c.px(cx - 1, cy + 2 * u, p["bone"][4])


def a_wisp(c, S, p, f, moving, big):
    u = S / 32
    cx, cy = S / 2, S / 2 + (0, -1, 0, 1)[f] * u
    g = p["glow"]
    c.shadow(cx, S - 3 * u, 6 * u, 1.8 * u)
    c.ellipse(cx, cy + 2 * u, 8 * u, 8 * u, p["body"])
    flick = (0, 1, 2, 1)[f] * u
    c.poly([(cx - 7 * u, cy), (cx - 3 * u + flick, cy - 13 * u), (cx, cy - 6 * u), (cx + 3 * u - flick, cy - 14 * u), (cx + 7 * u, cy)], p["body"], tone=3)
    c.ellipse(cx, cy + 2 * u, 5.5 * u, 5.5 * u, g, dither=False)
    c.poly([(cx - 4 * u, cy), (cx - 1 * u + flick, cy - 8 * u), (cx + 4 * u, cy)], g, tone=4)
    for i in range(3):
        a = f * math.pi / 2 + i * 2.1
        c.px(cx + 11 * u * math.cos(a), cy + 4 * u + 5 * u * math.sin(a), g[4])
    c.rect(cx - 3 * u, cy + 1 * u, cx - 2 * u, cy + 3 * u, px.OUTLINE); c.rect(cx + 2 * u, cy + 1 * u, cx + 3 * u, cy + 3 * u, px.OUTLINE)


def a_blob(c, S, p, f, moving, big):
    u = S / 32
    sq = (0, 1, 2, 1)[f] if not moving else (0, 2, 3, 1)[f]
    rx, ry = (12 + sq * 0.6) * u, (10 - sq * 0.6) * u
    cx, cy = S / 2, S - 6 * u - ry
    c.shadow(cx, S - 5 * u, rx + 1, 2.5 * u)
    c.ellipse(cx, cy, rx, ry, p["body"])
    r = p["rng"]
    for i in range(4):
        x = cx + r.uniform(-0.6, 0.6) * rx
        y = cy + r.uniform(-0.6, 0.2) * ry
        c.ellipse(x, y, 1.6 * u, 1.6 * u, p["glow"], dither=False)
    for sx in (-1, 1):
        eyeball(c, cx + sx * 4.5 * u, cy - 1 * u, 2.6 * u, p, ((0, 0), (sx * 0.5, 0), (0, 0.5), (0, 0))[f])
    c.rect(cx - 3 * u, cy + 4 * u, cx + 3 * u, cy + 4 * u + max(0, u - 1), px.OUTLINE)
    c.px(cx - 2 * u, cy + 5 * u, p["bone"][4]); c.px(cx + 2 * u, cy + 5 * u, p["bone"][4])
    if big:
        for i in range(7):
            a = math.pi * (1.1 + 0.8 * i / 6)
            c.poly([(cx + rx * 0.9 * math.cos(a) - 1.5 * u, cy + ry * 0.9 * math.sin(a)), (cx + rx * 1.15 * math.cos(a), cy + ry * 1.25 * math.sin(a)),
                    (cx + rx * 0.9 * math.cos(a) + 1.5 * u, cy + ry * 0.9 * math.sin(a))], p["bone"], tone=3)


def a_hopper(c, S, p, f, moving, big):
    u = S / 32
    hop = (0, -3, -5, -2)[f] * u if moving else (0, 0, 1, 0)[f] * u
    cx, cy = S / 2, S / 2 + 3 * u + hop
    c.shadow(S / 2, S - 4 * u, 9 * u, 2.4 * u)
    for sx in (-1, 1):
        c.ellipse(cx + sx * 9 * u, cy + 4 * u - hop * 0.5, 3.5 * u, 4.5 * u, p["dark"])
        c.ellipse(cx + sx * 6 * u, S - 6 * u + (hop * 0.3), 3 * u, 1.6 * u, p["dark"], dither=False)
    c.ellipse(cx, cy, 10 * u, 8.5 * u, p["body"])
    c.ellipse(cx, cy + 3 * u, 6 * u, 4 * u, p["bone"], dither=False)
    for sx in (-1, 1):
        eyeball(c, cx + sx * 4.5 * u, cy - 6 * u, 3 * u, p)
    c.line(cx - 4 * u, cy + 0.5 * u, cx + 4 * u, cy + 0.5 * u, px.OUTLINE)


def a_crawler(c, S, p, f, moving, big):
    u = S / 32
    n = 5
    ph = f * math.pi / 2
    for i in reversed(range(n)):
        y = S / 2 - 8 * u + i * 4.2 * u
        x = S / 2 + 2.5 * u * math.sin(ph + i * 1.1)
        r = (6.5 - i * 0.6) * u
        for sx in (-1, 1):
            leg = (1 if (i + f) % 2 == 0 else -1) * u
            c.line(x + sx * r, y + 1 * u, x + sx * (r + 3 * u), y + 3 * u + leg, p["dark"][1])
        c.ellipse(x, y, r, r * 0.85, p["body"] if i % 2 == 0 else p["dark"])
    hx = S / 2 + 2.5 * u * math.sin(ph)
    hy = S / 2 - 8 * u
    eyes(c, hx, hy + 1 * u, p, max(1, int(2.5 * u)), size=max(2, int(1.3 * u)))
    for sx in (-1, 1):
        c.line(hx + sx * 2 * u, hy + 4 * u, hx + sx * 3 * u, hy + 7 * u, p["bone"][4])


def a_brute(c, S, p, f, moving, big):
    u = S / 32
    bob = (0, 1, 0, 1)[f] * u if moving else (0, 0, 1, 0)[f] * u
    sw = (1, 0, -1, 0)[f] * u if moving else 0
    cx, cy = S / 2, S / 2 + 2 * u + bob
    c.shadow(cx, S - 4 * u, 12 * u, 2.8 * u)
    for sx in (-1, 1):
        c.ellipse(cx + sx * 5 * u, S - 6 * u, 3.5 * u, 2.2 * u, p["dark"], dither=False)
    c.ellipse(cx, cy, 11 * u, 9.5 * u, p["body"])
    for sx in (-1, 1):
        y = cy + 2 * u + sx * sw
        c.ellipse(cx + sx * 12 * u, y, 3.6 * u, 5 * u, p["dark"])
        c.ellipse(cx + sx * 12.5 * u, y + 5 * u, 3.6 * u, 3.2 * u, p["body"])
        c.ellipse(cx + sx * 10 * u, cy - 6 * u, 4 * u, 3 * u, p["accent"])
    c.ellipse(cx, cy - 8 * u, 5 * u, 4.5 * u, p["body"])
    eyes(c, cx, cy - 8 * u, p, max(1, int(2 * u)), size=max(2, int(1.3 * u)))
    for sx in (-1, 1):
        c.poly([(cx + sx * 3 * u, cy - 11 * u), (cx + sx * 5 * u, cy - 15 * u), (cx + sx * 5.5 * u, cy - 10 * u)], p["bone"], tone=3)
    c.rect(cx - 2 * u, cy - 5.5 * u, cx + 2 * u, cy - 5.5 * u, px.OUTLINE)


def a_humanoid(c, S, p, f, moving, big):
    u = S / 32
    bob = (0, 1, 0, 1)[f] * u if moving else (0, 0, 1, 1)[f] * u
    step = (-1, 0, 1, 0)[f] * u if moving else 0
    cx = S / 2
    c.shadow(cx, S - 4 * u, 8 * u, 2.2 * u)
    for sx in (-1, 1):
        c.ellipse(cx + sx * 3 * u + sx * step, S - 6 * u, 2.4 * u, 1.6 * u, p["dark"], dither=False)
    by = S - 13 * u + bob
    c.poly([(cx - 9 * u, by + 7 * u), (cx - 5 * u, by - 6 * u), (cx + 5 * u, by - 6 * u), (cx + 9 * u, by + 7 * u)], p["body"], tone=2)
    c.ellipse(cx, by + 1 * u, 7 * u, 7 * u, p["body"])
    c.rect(cx - 6 * u, by + 1 * u, cx + 6 * u, by + 1 * u + max(0, u - 1), p["accent"][3])
    hy = by - 9 * u
    c.ellipse(cx, hy, 7 * u, 7 * u, p["body"])
    c.ellipse(cx, hy + 2 * u, 4.5 * u, 4 * u, [(16, 12, 18, 255), (24, 18, 26, 255), (30, 22, 32, 255)], dither=False)
    eyes(c, cx, hy + 2 * u, p, max(1, int(2 * u)), size=max(2, int(1.3 * u)))
    for sx in (-1, 1):
        c.ellipse(cx + sx * 8 * u, by - 1 * u - sx * step * 0.5, 1.8 * u, 1.8 * u, p["bone"], dither=False)
    if p["rng"].random() < 0.6:
        x = cx + 10 * u
        c.line(x, by - 14 * u, x, S - 5 * u, p["accent"][1])
        c.ellipse(x, by - 15 * u, 2 * u, 2 * u, p["glow"], dither=False)
    if big:
        for i in range(5):
            x = cx - 4 * u + i * 2 * u
            c.line(x, hy - 7 * u, x, hy - 9 * u - (i % 2) * u, p["accent"][4])


BODIES = dict(eye=a_eye, turret=a_turret, lantern=a_lantern, flyer=a_flyer, wisp=a_wisp, blob=a_blob,
              hopper=a_hopper, crawler=a_crawler, brute=a_brute, humanoid=a_humanoid)


def crown(c, S, p, f):
    """Boss / miniboss crown floating over the highest opaque pixel, with a sparkle that travels."""
    alpha = c.a[:, :, 3] > 200
    rows = alpha.any(axis=1).nonzero()[0]
    top = int(rows[0]) if len(rows) else 4
    cx = S // 2
    y = max(4, top - 1)
    g = p["glow"]
    gold = px.ramp("#e8b440", 5)
    c.rect(cx - 6, y, cx + 6, y + 2, gold[2])
    c.rect(cx - 6, y, cx + 6, y, gold[4])
    for i, dx in enumerate((-6, -3, 0, 3, 6)):
        h = 4 if i % 2 == 0 else 2
        c.rect(cx + dx, y - h, cx + dx, y - 1, gold[3])
        if i % 2 == 0:
            c.px(cx + dx, y - h - 1, g[4] if (i // 2) == f % 3 else gold[4])
    c.px(cx, y + 1, g[4])


SIZES = dict(enemy=None, miniboss=None, boss=None, companion=24)


def frame_size(cdef):
    if cdef["kind"] == "companion":
        return 24
    s = int(math.ceil(cdef["radius"] * 2 * 32 * 1.25)) + 6
    s += s % 2
    return max(24, min(96, s))


def render(cdef):
    arch = archetype(cdef["name"], cdef["desc"], cdef["tags"], cdef["move"])
    big = cdef["kind"] in ("boss", "miniboss")
    S = frame_size(cdef)
    sets = []
    for moving in (False, True):
        frames = []
        for f in range(4):
            p = palette(cdef)  # fresh rng per frame keeps random details identical across frames
            c = px.Canvas(S, S)
            BODIES[arch](c, S, p, f, moving, big)
            if big:
                crown(c, S, p, f)
            c.outline()
            frames.append(c)
        sets.append(frames)
    base = os.path.join(px.SPRITES, cdef["key"])
    px.save_strip(sets[0], base + ".png")
    if cdef["move"] not in ("Idle", ""):
        px.save_strip(sets[1], base + "_move.png")
    elif os.path.exists(base + "_move.png"):
        os.remove(base + "_move.png")
    return arch, S


def main():
    args = sys.argv[1:]
    only = None
    if args[:1] == ["--only"]:
        only, args = args[1], args[2:]
    for cdef in load():
        if only and cdef["kind"] != only:
            continue
        if args and cdef["id"] not in args:
            continue
        arch, S = render(cdef)
        print(cdef["kind"], cdef["id"], arch, S, flush=True)


if __name__ == "__main__":
    main()
