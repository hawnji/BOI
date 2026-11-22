"""Renders every enemy, miniboss, boss and companion sprite from the content JSON.

Usage: python Tools/ArtGen/gen_creatures.py [--only enemy|miniboss|boss|companion] [id ...]
Output: Assets/Resources/Sprites/<spriteKey>.png
Each creature's body plan (archetype) comes from its name, description, tags and movement; its palette from
the floor theme it lives on. Everything is seeded by the id, so reruns give identical art.
"""
import json
import math
import os
import sys
import zlib

sys.path.insert(0, os.path.dirname(__file__))
import hc_common as hc  # noqa: E402
from gen_tiles import THEMES  # noqa: E402

CONTENT = os.path.join(hc.REPO, "Assets", "StreamingAssets", "Content")

ARCHETYPES = [
    ("eye", ["eye"]),
    ("turret", ["turret", "sentry", "reliquary", "bellows", "tree", "wall", "chime", "organ", "font", "engine", "crucible", "censer"]),
    ("lantern", ["lantern", "skull lantern"]),
    ("flyer", ["bat", "crow", "moth", "wasp", "fly", "chick", "drone", "sparrow", "magpie", "wren", "bird", "bomber", "strafer", "raven", "phoenix"]),
    ("wisp", ["wisp", "mote", "sprite", "spiral", "swirl", "shade", "wraith", "blinker", "refraction", "echo", "dancer", "spark", "flame", "ember moon", "halo", "nothing", "dark", "void", "prism", "kaleidos", "tempest", "unformed"]),
    ("blob", ["bloat", "sludge", "jelly", "mound", "puff", "mushroom", "spore", "apple", "clot", "seed mother", "bloom", "rot", "gutmaw", "heart", "ventricle", "matron", "maw", "pod", "pulse", "throb", "mother"]),
    ("hopper", ["hopper", "toad", "imp", "frog", "goblin", "rat", "mole", "beetle", "tick", "leech"]),
    ("crawler", ["crawler", "snake", "eel", "wader", "burrower", "digger", "runner", "hound", "skater", "larva", "vertebra", "lasher", "swimmer", "fish", "barnacle", "thresher", "hanger", "leviathan"]),
    ("brute", ["brute", "golem", "shambler", "giant", "beast", "smelter", "anvil", "glacier", "knight", "guard", "warden", "king", "bough", "permafrost", "pyroclasm", "ironmonger", "harvester", "gargoyle"]),
    ("humanoid", ["priest", "monk", "acolyte", "zealot", "pilgrim", "herald", "witch", "deacon", "farmer", "hag", "scarecrow", "choir", "nurse", "thrower", "reaper", "abbess", "sexton", "widow", "glazier", "gardener", "regent", "queen", "bellringer", "walker", "husk", "puppet", "saint", "one", "spitter", "monk"]),
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
    if movement in ("Idle",):
        return "turret"
    return "brute" if "melee" in tags else "humanoid"


def theme_index():
    idx = {}
    import glob
    for f in glob.glob(os.path.join(CONTENT, "Floors", "*.json")):
        d = json.load(open(f))
        for fl in d["floors"] if isinstance(d, dict) else d:
            for key in ("enemies", "bosses", "minibosses"):
                for e in fl.get(key, []):
                    eid = e if isinstance(e, str) else e.get("enemyId") or e.get("bossId") or e.get("id")
                    idx.setdefault(eid, fl["theme"])
    return idx


def load():
    import glob
    out = []
    themes = theme_index()
    for f in glob.glob(os.path.join(CONTENT, "Enemies", "*.json")):
        d = json.load(open(f))
        for e in d.get("enemies", []):
            key = e.get("spriteKey") or e["id"]
            kind = "miniboss" if key.startswith("miniboss_") or e.get("isMiniboss") else "enemy"
            out.append(dict(kind=kind, id=e["id"], key=key, name=e["name"], desc=e.get("description", ""), tags=e.get("tags", []),
                            move=e.get("movement", {}).get("kind", ""), theme=themes.get(e["id"], "crypt")))
        for b in d.get("bosses", []):
            body = b["body"]
            bkey = body.get("spriteKey") or b["id"]
            out.append(dict(kind="miniboss" if bkey.startswith("miniboss_") else "boss", id=b["id"], key=bkey, name=b["name"], desc=b.get("description", ""),
                            tags=body.get("tags", []), move=body.get("movement", {}).get("kind", ""), theme=themes.get(b["id"], "crypt")))
    for f in glob.glob(os.path.join(CONTENT, "Companions", "*.json")):
        for c in json.load(open(f))["companions"]:
            out.append(dict(kind="companion", id=c["id"], key=c.get("spriteKey") or "companion_" + c["id"], name=c["name"],
                            desc=c.get("description", ""), tags=[], move=c.get("behavior", ""), theme=None))
    return out


# --------------------------------------------------------------------------- palette

COMPANION_PALETTE = dict(stone="#8a8070", mortar="#2a2420", accent="#6fa0c8", glow="#bff4ff", liquid="#5a7aa0")


def palette(c):
    t = THEMES.get(c["theme"]) if c["theme"] else COMPANION_PALETTE
    r = hc.rnd(zlib.crc32(c["id"].encode()))
    import colorsys
    def shift(hexv, dh, ds=0.0, dv=0.0):
        rr, gg, bb = (int(hexv[i:i + 2], 16) / 255 for i in (1, 3, 5))
        h, s, v = colorsys.rgb_to_hsv(rr, gg, bb)
        h = (h + dh) % 1.0
        s = min(1, max(0, s + ds))
        v = min(1, max(0, v + dv))
        rr, gg, bb = colorsys.hsv_to_rgb(h, s, v)
        return "#%02x%02x%02x" % (int(rr * 255), int(gg * 255), int(bb * 255))
    body = shift(t["stone"], r.uniform(-0.06, 0.06), r.uniform(0.05, 0.25), r.uniform(-0.05, 0.12))
    accent = shift(t["accent"], r.uniform(-0.05, 0.05), 0.1, 0.05)
    glow = t["glow"]
    tagset = set(c["tags"]) | set(c["name"].lower().split())
    if tagset & {"fire", "ember", "cinder", "pyre", "ash", "flame", "slag", "magma"}:
        glow = "#ff7a1a"; body = shift("#5a3a30", r.uniform(-0.03, 0.03), 0.1, 0.05)
    if tagset & {"frost", "ice", "rime", "snow", "icicle", "blizzard", "glacier", "hoarfrost"}:
        glow = "#9ee8ff"; body = shift("#a8c4d8", r.uniform(-0.03, 0.03), 0.0, 0.0)
    if tagset & {"poison", "rot", "bile", "sludge", "spore", "mire", "bog"}:
        glow = "#b6ff5a"
    if tagset & {"storm", "lightning", "thunder", "spark", "static"}:
        glow = "#ffe85a"
    if tagset & {"undead", "bone", "skull", "rib", "marrow", "knuckle", "vertebra", "ossuary"}:
        body = shift("#cdbf9e", r.uniform(-0.02, 0.02), 0.0, r.uniform(-0.05, 0.05))
    return dict(
        body=hc.mat("body", hc.hexcol(body), rough=0.6, noise=0.25, bump=0.35, scale=r.uniform(10, 30), sss=0.1),
        body2=hc.mat("body2", hc.hexcol(shift(body, 0.0, 0.05, -0.2)), rough=0.7, noise=0.2, bump=0.3, scale=20),
        accent=hc.mat("accent", hc.hexcol(accent), rough=0.4, metal=0.6, noise=0.15, bump=0.15),
        glow=hc.glow_mat("glow", hc.hexcol(glow), 5),
        eye_white=hc.mat("white", hc.hexcol("#efe8dc"), rough=0.2, noise=0.05, bump=0.02),
        pupil=hc.mat("pupil", hc.hexcol("#120c0c"), rough=0.1, noise=0, bump=0),
        bone=hc.mat("bone", hc.hexcol("#e2d6bc"), rough=0.55, noise=0.15, bump=0.2),
        dark=hc.mat("dark", hc.hexcol("#16121a"), rough=0.5, noise=0.1, bump=0.1),
        rng=r,
    )


# --------------------------------------------------------------------------- body plans


def glowing_eyes(p, z, y, spread=0.08, r=0.04, count=2):
    xs = [0.0] if count == 1 else [(-spread + 2 * spread * i / (count - 1)) for i in range(count)]
    for x in xs:
        hc.sphere((x, y, z), r, p["glow"], scale=(1, 0.6, 0.8))


def eyeball(p, loc, r, look=(0, -1, 0.2)):
    hc.sphere(loc, r, p["eye_white"])
    lx, ly, lz = look
    n = math.sqrt(lx * lx + ly * ly + lz * lz)
    iris = (loc[0] + r * 0.82 * lx / n, loc[1] + r * 0.82 * ly / n, loc[2] + r * 0.82 * lz / n)
    hc.sphere(iris, r * 0.45, p["glow"], scale=(1, 0.5, 1))
    pup = (loc[0] + r * 0.98 * lx / n, loc[1] + r * 0.98 * ly / n, loc[2] + r * 0.98 * lz / n)
    hc.sphere(pup, r * 0.22, p["pupil"], scale=(1, 0.4, 1))


def spikes_ring(p, z, R, n, length, mat, tilt=30):
    for i in range(n):
        a = i * 2 * math.pi / n
        hc.cone((R * math.cos(a), R * math.sin(a), z), length * 0.25, length, mat, verts=10,
                rot=(math.radians(tilt) * math.sin(a) * -1, math.radians(tilt) * math.cos(a), 0))


def b_eye(p, big):
    r = p["rng"]
    eyeball(p, (0, 0, 0.42), 0.36)
    hc.torus((0, 0, 0.42), 0.37, 0.04, p["body"], rot=(math.radians(90 - 20), 0, 0))
    for i in range(7):
        a = r.uniform(0, 2 * math.pi)
        hc.cone((0.33 * math.cos(a), 0.2 + 0.1 * math.sin(a), 0.42 + 0.3 * math.sin(a)), 0.04, 0.35, p["body2"], verts=8,
                rot=(math.radians(r.uniform(-70, 70)), math.radians(r.uniform(-70, 70)), 0))
    if big:
        spikes_ring(p, 0.42, 0.38, 10, 0.18, p["accent"], tilt=90)


def b_turret(p, big):
    r = p["rng"]
    hc.cyl((0, 0, 0.06), 0.42, 0.12, p["dark"], verts=8, bevel=0.02)
    hc.cyl((0, 0, 0.35), 0.3, 0.5, p["body"], verts=8, r2=0.24, bevel=0.02)
    hc.cyl((0, 0, 0.62), 0.27, 0.06, p["accent"], verts=8, bevel=0.01)
    hc.sphere((0, 0, 0.66), 0.2, p["body2"], scale=(1, 1, 0.8))
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 4
        hc.cyl((0.27 * math.cos(a), 0.27 * math.sin(a), 0.43), 0.05, 0.08, p["glow"], rot=(math.radians(90), 0, a + math.pi / 2))
    eyeball(p, (0, -0.12, 0.7), 0.1)
    if big:
        spikes_ring(p, 0.8, 0.15, 6, 0.25, p["accent"], tilt=20)


def b_lantern(p, big):
    hc.cyl((0, 0, 0.88), 0.015, 0.25, p["dark"])
    hc.torus((0, 0, 0.76), 0.06, 0.015, p["accent"])
    hc.cyl((0, 0, 0.66), 0.2, 0.08, p["accent"], verts=6, r2=0.1)
    hc.cyl((0, 0, 0.42), 0.2, 0.4, p["glow"], verts=6)
    for i in range(6):
        a = i * math.pi / 3
        hc.box((0.2 * math.cos(a), 0.2 * math.sin(a), 0.42), (0.03, 0.03, 0.42), p["accent"], bevel=0.005)
    hc.cyl((0, 0, 0.2), 0.22, 0.06, p["accent"], verts=6)
    hc.sphere((0, -0.05, 0.42), 0.12, p["bone"])
    glowing_eyes(p, 0.44, -0.16, 0.045, 0.025)


def wings(p, z, span, mat, membrane=True):
    r = p["rng"]
    for side in (-1, 1):
        for k in range(3):
            hc.sphere((side * (0.18 + 0.12 * k) * span, 0.05 + 0.04 * k, z + 0.06 * k - 0.04), 0.13 * span, mat,
                      scale=(1.3, 0.8 - 0.15 * k, 0.12))


def b_flyer(p, big):
    hc.sphere((0, 0, 0.45), 0.2, p["body"], scale=(1, 1, 1.05))
    hc.sphere((0, -0.08, 0.6), 0.14, p["body"])
    wings(p, 0.5, 1.2, p["body2"])
    glowing_eyes(p, 0.63, -0.2, 0.05, 0.03)
    for sx in (-0.07, 0.07):
        hc.cone((sx, -0.05, 0.75), 0.03, 0.1, p["body"], verts=8)
    hc.cone((0, -0.21, 0.56), 0.03, 0.08, p["accent"], verts=8, rot=(math.radians(100), 0, 0))


def b_wisp(p, big):
    r = p["rng"]
    hc.sphere((0, 0, 0.5), 0.24, p["glow"])
    shell = hc.ico((0, 0, 0.5), 0.3, p["body2"], subdiv=2, smooth=False)
    shell.modifiers.new("W", "WIREFRAME").thickness = 0.02
    for i in range(6):
        a = i * math.pi / 3 + r.uniform(0, 0.4)
        hc.sphere((0.36 * math.cos(a), 0.36 * math.sin(a), 0.5 + 0.12 * math.sin(2 * a)), 0.05, p["glow"])
    hc.cone((0, 0.05, 0.22), 0.16, 0.3, p["glow"], verts=12, rot=(math.radians(180), 0, 0))
    eyeball(p, (0, -0.2, 0.55), 0.07) if big else glowing_eyes(p, 0.55, -0.23, 0.07, 0.035)


def blob_eyes(p, z, y, r):
    for sx in (-0.11, 0.11):
        eyeball(p, (sx, y, z), r, look=(0, -1, 0.3))


def b_blob(p, big):
    r = p["rng"]
    o = hc.ico((0, 0, 0.3), 0.42, p["body"], scale=(1, 1, 0.75), subdiv=4, displace=0.08, seed=1)
    for _ in range(6):
        a = r.uniform(0, 2 * math.pi)
        hc.sphere((0.32 * math.cos(a), 0.32 * math.sin(a), 0.45), r.uniform(0.04, 0.08), p["glow"], scale=(1, 1, 0.6))
    blob_eyes(p, 0.45, -0.3, 0.09)
    hc.box((0, -0.36, 0.25), (0.2, 0.04, 0.03), p["dark"], bevel=0.015)
    if big:
        spikes_ring(p, 0.45, 0.36, 12, 0.22, p["bone"], tilt=55)


def legs(p, n, R, z, mat, length=0.22):
    for i in range(n):
        a = math.pi * (0.15 + 0.7 * i / max(1, n - 1))
        for side in (-1, 1):
            x = side * R * math.sin(a)
            y = -R * math.cos(a) * 0.8
            hc.cyl((x * 1.2, y, z), 0.025, length, mat, rot=(0, math.radians(side * 55), 0))


def b_hopper(p, big):
    hc.sphere((0, 0.02, 0.3), 0.3, p["body"], scale=(1.1, 1, 0.85))
    hc.sphere((0, -0.18, 0.42), 0.18, p["body"], scale=(1.2, 1, 0.8))
    for side in (-1, 1):
        hc.sphere((side * 0.3, 0.08, 0.16), 0.13, p["body2"], scale=(0.8, 1.4, 0.8))
        hc.sphere((side * 0.18, -0.2, 0.1), 0.08, p["body2"], scale=(1, 1.3, 0.6))
    blob_eyes(p, 0.53, -0.25, 0.07)
    hc.box((0, -0.33, 0.36), (0.18, 0.03, 0.025), p["dark"], bevel=0.01)


def b_crawler(p, big):
    r = p["rng"]
    segs = 5
    for i in range(segs):
        y = -0.32 + i * 0.16
        s = 0.2 - i * 0.02
        hc.sphere((0.04 * math.sin(i * 1.3), y, s * 0.9), s, p["body"] if i % 2 == 0 else p["body2"], scale=(1.1, 1, 0.8))
        for side in (-1, 1):
            hc.cyl((side * (s + 0.06), y, 0.06), 0.018, 0.18, p["dark"], rot=(0, math.radians(side * 70), 0))
    glowing_eyes(p, 0.25, -0.5, 0.07, 0.035)
    for side in (-1, 1):
        hc.cone((side * 0.08, -0.5, 0.12), 0.025, 0.12, p["bone"], verts=8, rot=(math.radians(100), 0, side * 0.3))


def b_brute(p, big):
    hc.sphere((0, 0, 0.35), 0.33, p["body"], scale=(1.15, 0.9, 1))
    hc.sphere((0, -0.12, 0.66), 0.14, p["body"])
    for side in (-1, 1):
        hc.sphere((side * 0.38, -0.05, 0.42), 0.13, p["accent"])   # shoulder plates
        hc.sphere((side * 0.42, -0.12, 0.2), 0.12, p["body2"], scale=(1, 1, 1.4))  # arms
        hc.sphere((side * 0.42, -0.18, 0.06), 0.1, p["body"])     # fists
        hc.sphere((side * 0.15, -0.05, 0.05), 0.11, p["body2"], scale=(1, 1.2, 0.6))
    glowing_eyes(p, 0.68, -0.25, 0.05, 0.028)
    for side in (-1, 1):
        hc.cone((side * 0.1, -0.12, 0.8), 0.04, 0.14, p["bone"], verts=8, rot=(0, math.radians(side * 30), 0))
    if big:
        hc.box((0, -0.3, 0.35), (0.3, 0.04, 0.3), p["accent"], bevel=0.03)


def b_humanoid(p, big):
    r = p["rng"]
    hc.cyl((0, 0, 0.22), 0.28, 0.44, p["body"], verts=40, r2=0.13)
    s = hc.bpy.context.active_object.modifiers.new("Sub", "SUBSURF"); s.levels = s.render_levels = 2
    hc.torus((0, 0, 0.32), 0.17, 0.025, p["accent"])
    hc.sphere((0, 0.04, 0.6), 0.22, p["body"], scale=(1, 1, 1.1))   # hood
    hc.sphere((0, -0.06, 0.57), 0.16, p["dark"])                     # shadowed face
    glowing_eyes(p, 0.58, -0.2, 0.06, 0.03)
    for side in (-1, 1):
        hc.sphere((side * 0.25, -0.06, 0.28), 0.06, p["bone"])
    if r.random() < 0.5:
        hc.cyl((0.3, -0.06, 0.38), 0.018, 0.72, p["dark"], verts=10)
        hc.sphere((0.3, -0.06, 0.76), 0.055, p["glow"])
    if big:
        for i in range(5):
            a = math.pi * (0.2 + 0.6 * i / 4)
            hc.cone((0.17 * math.cos(a), 0.04, 0.8 + 0.03 * math.sin(a)), 0.03, 0.14, p["accent"], verts=8)


BUILDERS = dict(eye=b_eye, turret=b_turret, lantern=b_lantern, flyer=b_flyer, wisp=b_wisp, blob=b_blob,
                hopper=b_hopper, crawler=b_crawler, brute=b_brute, humanoid=b_humanoid)
SIZES = dict(enemy=128, miniboss=192, boss=256, companion=96)


def top_z():
    top = 0.0
    for o in hc.bpy.data.objects:
        if o.type == "MESH":
            for corner in o.bound_box:
                top = max(top, (o.matrix_world @ hc.Vector(corner)).z)
    return top


def boss_dressing(p, boss):
    """Crown, glowing heart-gem and a ring of back spikes so bosses read as bosses at a glance."""
    z = top_z() - 0.1
    n = 7 if boss else 5
    hc.torus((0, 0.02, z - 0.02), 0.13, 0.025, p["accent"])
    for i in range(n):
        a = math.pi * (0.1 + 0.8 * i / (n - 1))
        hc.cone((0.13 * math.cos(a), 0.02 + 0.05 * math.sin(a), z + 0.06), 0.03, 0.13, p["accent"], verts=8)
        if boss and i % 2 == 0:
            hc.sphere((0.13 * math.cos(a), 0.02 + 0.05 * math.sin(a), z + 0.14), 0.022, p["glow"])
    if boss:
        hc.ico((0, -0.36, 0.5), 0.05, p["glow"], subdiv=1, smooth=False)
        for i in range(9):
            a = math.pi * (1.05 + 0.9 * i / 8)
            hc.cone((0.42 * math.cos(a), 0.18 - 0.1 * math.sin(a), 0.38), 0.05, 0.28, p["bone"], verts=10,
                    rot=(math.radians(-50), 0, a - math.pi * 1.5))


def render_one(c):
    hc.reset(samples=28 if c["kind"] != "boss" else 40)
    hc.camera(top_down=False, ortho_scale=1.12, tilt_deg=40)
    cam = hc.bpy.context.scene.camera
    cam.location.z += 0.4
    hc.lights(key=2.0)
    p = palette(c)
    arch = archetype(c["name"], c["desc"], c["tags"], c["move"])
    big = c["kind"] in ("boss", "miniboss")
    BUILDERS[arch](p, big)
    if big:
        boss_dressing(p, c["kind"] == "boss")
    hc.render(os.path.join(hc.SPRITES, c["key"] + ".png"), SIZES[c["kind"]])
    return arch


def main():
    args = sys.argv[1:]
    only = None
    if args and args[0] == "--only":
        only, args = args[1], args[2:]
    for c in load():
        if only and c["kind"] != only:
            continue
        if args and c["id"] not in args:
            continue
        arch = render_one(c)
        print(c["kind"], c["id"], arch, flush=True)


if __name__ == "__main__":
    main()
