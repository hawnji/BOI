"""Pixel-art dungeon tiles: 32x32, every kind for all 15 floor themes, several variants per kind.

Usage: python Tools/PixelArt/gen_tiles.py [theme ...]
Output: Assets/Resources/Tiles/<theme>/<kind>.png - a strip of square variants; the room renderer picks a
variant per cell, so floors and walls don't repeat visibly.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import px  # noqa: E402

T = 32
THEMES = {
    "crypt":   dict(stone="#6b625a", mortar="#2a2321", accent="#7a5230", glow="#ffb24a", liquid="#2f4f6e", decor="cracks"),
    "ossuary": dict(stone="#b3a386", mortar="#463a2e", accent="#6e5a3c", glow="#ffd27a", liquid="#4d5e52", decor="bones"),
    "mire":    dict(stone="#57603f", mortar="#232a19", accent="#5b4429", glow="#b6ff5a", liquid="#3d5a2b", decor="moss"),
    "forge":   dict(stone="#4a4442", mortar="#171211", accent="#6a6870", glow="#ff6a1a", liquid="#ff5a10", decor="embers"),
    "frost":   dict(stone="#9fb8cc", mortar="#4a5c70", accent="#5f7487", glow="#9ee8ff", liquid="#6fb4e0", decor="ice"),
    "garden":  dict(stone="#66724f", mortar="#2a3620", accent="#6b4b2a", glow="#f5ff8a", liquid="#2f5d55", decor="grass"),
    "belfry":  dict(stone="#646b78", mortar="#22262e", accent="#8a6a3a", glow="#ffe38a", liquid="#33465e", decor="planks"),
    "chapel":  dict(stone="#8a95a3", mortar="#353b45", accent="#4a5d75", glow="#a9d4ff", liquid="#2b5878", decor="checker"),
    "orchard": dict(stone="#8a6a4b", mortar="#3a2a1c", accent="#5c3d22", glow="#ffc06a", liquid="#4a5a2a", decor="leaves"),
    "hive":    dict(stone="#b0802e", mortar="#4a300c", accent="#6b4310", glow="#ffd23a", liquid="#d99a1a", decor="comb"),
    "glass":   dict(stone="#5f9296", mortar="#1e3a3a", accent="#9fd8d6", glow="#c8fffb", liquid="#3fa5b0", decor="shine"),
    "void":    dict(stone="#3f3456", mortar="#120d1c", accent="#5a3f7a", glow="#c07aff", liquid="#2a1a45", decor="stars"),
    "throne":  dict(stone="#6e3334", mortar="#250d0e", accent="#c79a3a", glow="#ffcf5a", liquid="#5a1a20", decor="carpet"),
    "heart":   dict(stone="#8e3e42", mortar="#330f12", accent="#5a1e22", glow="#ff6a7a", liquid="#8a0f1e", decor="veins"),
    "last":    dict(stone="#34333a", mortar="#0c0c0f", accent="#d8d4c8", glow="#f0ecff", liquid="#141418", decor="pale"),
}
VARIANTS = dict(floor=4, wall=3, rock=3, block=1, pit=1, spikes=1, prop=2, marked_rock=1, fire=1, water=2, explosive=1,
                door_open=1, door_closed=1)


class Pal:
    def __init__(self, t):
        self.t = t
        self.stone = px.ramp(t["stone"], 6, spread=0.36)
        self.mortar = px.ramp(t["mortar"], 4, spread=0.3)
        self.accent = px.ramp(t["accent"], 5)
        self.glow = px.ramp(t["glow"], 5, spread=0.5)
        self.liquid = px.ramp(t["liquid"], 5, spread=0.4)
        self.rock = px.ramp(t["stone"], 6, spread=0.5)
        self.wood = px.ramp("#7a5434", 5)
        self.metal = px.ramp("#8a8e98", 5, spread=0.5)
        self.wall = px.ramp(t["stone"], 7, spread=0.55)


def floor(c, p, seed, var):
    r = px.rng(seed)
    st, mo = p.stone, p.mortar
    c.rect(0, 0, T - 1, T - 1, st[2])
    layouts = [
        [(0, 0, 15, 15), (16, 0, 31, 15), (0, 16, 15, 31), (16, 16, 31, 31)],
        [(0, 0, 31, 15), (0, 16, 15, 31), (16, 16, 31, 31)],
        [(0, 0, 15, 31), (16, 0, 31, 15), (16, 16, 31, 31)],
        [(0, 0, 9, 15), (10, 0, 31, 15), (0, 16, 21, 31), (22, 16, 31, 31)],
    ]
    if p.t["decor"] == "planks":
        for i in range(4):
            y0 = i * 8
            tone = st[2 + r.choice((0, 0, 1))]
            c.rect(0, y0, T - 1, y0 + 7, tone)
            c.rect(0, y0, T - 1, y0, st[3])
            c.rect(0, y0 + 7, T - 1, y0 + 7, mo[1])
            cut = r.randint(4, 27)
            c.rect(cut, y0, cut, y0 + 7, mo[1])
            for _ in range(3):
                c.px(r.randint(0, 31), y0 + r.randint(2, 5), st[1])
        return
    if p.t["decor"] == "checker":
        for i in range(4):
            for j in range(4):
                tone = st[3] if (i + j) % 2 == 0 else st[1]
                c.rect(i * 8, j * 8, i * 8 + 7, j * 8 + 7, tone)
        c.rect(0, 0, T - 1, 0, mo[1]); c.rect(0, 0, 0, T - 1, mo[1])
        return
    for (x0, y0, x1, y1) in layouts[var % len(layouts)]:
        tone = r.choice((2, 2, 2, 3))
        c.rect(x0, y0, x1, y1, st[tone])
        c.rect(x0, y0, x1, y0, st[min(5, tone + 1)])          # lit top edge
        c.rect(x0, y0, x0, y1, st[min(5, tone + 1)])
        c.rect(x0, y1, x1, y1, mo[2])                         # grout
        c.rect(x1, y0, x1, y1, mo[2])
        for _ in range(int((x1 - x0) * (y1 - y0) / 70)):      # speckle
            c.px(r.randint(x0 + 1, x1 - 1), r.randint(y0 + 1, y1 - 1), st[max(0, tone - 1)])
    decor(c, p, r)


def decor(c, p, r):
    d = p.t["decor"]
    st = p.stone
    if d == "cracks" and r.random() < 0.6:
        x, y = r.randint(4, 26), r.randint(4, 26)
        for _ in range(r.randint(4, 8)):
            c.px(x, y, p.mortar[0]); x += r.choice((-1, 0, 1)); y += r.choice((0, 1))
    elif d == "bones" and r.random() < 0.5:
        bone = px.ramp("#e8dcc0", 4)
        x, y = r.randint(5, 24), r.randint(5, 24)
        c.line(x, y, x + 4, y + 2, bone[2]); c.px(x - 1, y, bone[3]); c.px(x, y - 1, bone[3]); c.px(x + 5, y + 2, bone[3]); c.px(x + 4, y + 3, bone[3])
    elif d in ("moss", "grass"):
        g = px.ramp("#5f8a3a" if d == "moss" else "#6fa040", 4)
        for _ in range(r.randint(2, 5)):
            x, y = r.randint(1, 29), r.randint(1, 29)
            c.px(x, y, g[2]); c.px(x + 1, y, g[1]); c.px(x, y - 1 if d == "grass" else y + 1, g[3])
    elif d == "embers":
        for _ in range(r.randint(0, 3)):
            c.px(r.randint(1, 30), r.randint(1, 30), p.glow[2])
        if r.random() < 0.4:
            x, y = r.randint(4, 26), r.randint(4, 26)
            for _ in range(6):
                c.px(x, y, p.glow[1]); x += r.choice((-1, 1)); y += r.choice((0, 1))
    elif d == "ice":
        for _ in range(3):
            x, y = r.randint(2, 28), r.randint(2, 28)
            c.line(x, y, x + 2, y - 2, (230, 248, 255, 255))
    elif d == "leaves":
        lf = px.ramp(r.choice(("#c0602a", "#d8a030", "#8a3a20")), 4)
        for _ in range(r.randint(1, 3)):
            x, y = r.randint(2, 28), r.randint(2, 28)
            c.px(x, y, lf[2]); c.px(x + 1, y, lf[3]); c.px(x, y + 1, lf[1])
    elif d == "comb":
        for j in range(0, 32, 8):
            for i in range(0, 32, 10):
                ox = i + (5 if (j // 8) % 2 else 0)
                for k in range(6):
                    a = k * math.pi / 3
                    c.px(ox + 4 * math.cos(a), j + 4 + 4 * math.sin(a), p.mortar[2])
    elif d == "shine":
        c.line(4, 12, 12, 4, (210, 255, 250, 255)); c.line(6, 13, 13, 6, st[5])
    elif d == "stars":
        for _ in range(r.randint(2, 5)):
            c.px(r.randint(0, 31), r.randint(0, 31), p.glow[r.choice((2, 4))])
    elif d == "carpet":
        cp = px.ramp("#8a1f26", 4)
        c.rect(6, 0, 25, 31, cp[2]); c.rect(6, 0, 6, 31, p.accent[3]); c.rect(25, 0, 25, 31, p.accent[3])
        for y in range(2, 32, 6):
            c.px(15, y, p.accent[2]); c.px(16, y, p.accent[2])
    elif d == "veins":
        v = px.ramp("#5a0f18", 4)
        x, y = r.randint(0, 31), 0
        while y < 32:
            c.px(x, y, v[1]); c.px(x + 1, y, v[2]); x += r.choice((-1, 0, 1)); y += 1
    elif d == "pale":
        x, y = r.randint(3, 28), r.randint(3, 28)
        for _ in range(5):
            c.px(x, y, (200, 196, 210, 255)); x += r.choice((-1, 1)); y += r.choice((-1, 1))


def shadow_under(c, cx, cy, rx, ry):
    c2 = px.Canvas(T, T)
    c2.ellipse(cx, cy, rx, ry, [(0, 0, 0, 255)] * 3, dither=False)
    m = c2.a[:, :, 3] > 0
    c.a[m, :3] = (c.a[m, :3] * 0.55).astype("uint8")


def obj_on(c, obj):
    """Paste an outlined object canvas over a tile."""
    obj.outline()
    c.paste(obj, 0, 0)


def rock(c, p, seed, marked=False):
    r = px.rng(seed)
    shadow_under(c, 17, 20, 12, 9)
    o = px.Canvas(T, T)
    o.ellipse(16, 16, 12 + r.uniform(-1, 1), 10.5 + r.uniform(-1, 1), p.rock)
    o.ellipse(10 + r.randint(-1, 1), 18, 6, 5, p.rock)
    o.ellipse(22, 19 + r.randint(-1, 1), 6, 5, p.rock)
    for _ in range(3):  # cracks / facets
        x, y = r.randint(9, 22), r.randint(10, 20)
        o.px(x, y, p.rock[1]); o.px(x + 1, y + 1, p.rock[1])
    if marked:
        g = p.glow
        for i in range(-3, 4):
            o.px(16 + i, 14 + i, g[4] if abs(i) < 2 else g[3]); o.px(16 + i, 14 - i, g[4] if abs(i) < 2 else g[3])
    obj_on(c, o)


def block(c, p):
    shadow_under(c, 18, 19, 15, 14)
    o = px.Canvas(T, T)
    o.box(2, 3, 29, 28, p.stone, top=3)
    o.box(7, 8, 24, 23, p.stone[1:], top=2)
    for (x, y) in ((4, 5), (27, 5), (4, 26), (27, 26)):
        o.px(x, y, p.metal[4])
    obj_on(c, o)


def pit(c, p):
    c.rect(0, 0, T - 1, T - 1, (10, 8, 12, 255))
    for i in range(6):  # depth gradient on the far (top) inner wall
        c.rect(1, 1 + i, T - 2, 1 + i, p.mortar[max(0, 2 - i // 2)])
    c.rect(0, 0, T - 1, 0, p.stone[3]); c.rect(0, 0, 0, T - 1, p.stone[2]); c.rect(T - 1, 0, T - 1, T - 1, p.stone[1])
    c.rect(0, T - 1, T - 1, T - 1, p.stone[1])


def spikes(c, p, seed):
    floor(c, p, seed, 0)
    for gy in range(3):
        for gx in range(3):
            x, y = 6 + gx * 10, 8 + gy * 9
            c.rect(x - 2, y + 2, x + 2, y + 3, p.mortar[0])
            for k in range(5):
                w = (4 - k) // 2
                c.rect(x - w, y + 1 - k, x + w, y + 1 - k, p.metal[1 + min(3, k)])
            c.px(x, y - 4, p.metal[4])


def barrel(c, p, seed, explosive=False):
    shadow_under(c, 18, 25, 10, 4)
    o = px.Canvas(T, T)
    body = px.ramp("#a02a22", 5) if explosive else p.wood
    o.ellipse(16, 18, 10, 11, body)
    o.ellipse(16, 9, 9, 3.2, body[1:] + [body[-1]], dither=False)
    o.ellipse(16, 9, 7.5, 2.2, [body[3]] * 3, dither=False)
    for yy in (13, 23):
        for x in range(7, 26):
            if o.a[yy, x, 3]:
                o.px(x, yy, p.metal[3] if x < 14 else p.metal[1])
    if explosive:
        o.rect(13, 16, 19, 18, (250, 220, 90, 255)); o.rect(15, 14, 17, 20, (250, 220, 90, 255))
    obj_on(c, o)


def urn(c, p, seed):
    shadow_under(c, 18, 25, 9, 4)
    o = px.Canvas(T, T)
    o.ellipse(16, 18, 9, 9.5, p.accent)
    o.ellipse(16, 8, 5, 2.5, p.accent, dither=False)
    o.rect(13, 8, 19, 10, p.accent[2])
    o.ellipse(16, 8, 3.5, 1.4, [(20, 14, 16, 255)] * 3, dither=False)
    o.rect(9, 17, 23, 17, p.accent[4])
    obj_on(c, o)


def fire(c, p, seed):
    shadow_under(c, 17, 24, 12, 5)
    o = px.Canvas(T, T)
    o.box(6, 16, 26, 26, p.metal, top=2)
    o.rect(8, 15, 24, 16, (20, 14, 12, 255))
    fl = px.ramp("#ff7a1a", 5, spread=0.55)
    o.ellipse(16, 11, 7, 8, fl, dither=False)
    o.ellipse(16, 13, 4, 5, px.ramp("#ffd84a", 5, spread=0.4), dither=False)
    o.px(16, 3, fl[3]); o.px(12, 6, fl[2]); o.px(20, 5, fl[2])
    obj_on(c, o)


def water(c, p, seed, var):
    r = px.rng(seed)
    lq = p.liquid
    c.rect(0, 0, T - 1, T - 1, lq[1])
    for y in range(T):
        for x in range(T):
            if (x + y * 2 + var * 5) % 11 == 0:
                c.px(x, y, lq[2])
    for _ in range(4):
        x, y = r.randint(2, 26), r.randint(2, 28)
        c.rect(x, y, x + r.randint(2, 4), y, lq[4])
    c.rect(0, 0, T - 1, 1, lq[0])


def wall(c, p, seed, var):
    r = px.rng(seed)
    c.rect(0, 0, T - 1, T - 1, p.mortar[0])
    for row in range(4):
        y0 = row * 8
        off = (8 + var * 3) if row % 2 else var * 5
        x = -off
        while x < T:
            tone = r.choice((1, 2, 2, 3))
            ramp = p.wall[tone:tone + 3] + [p.wall[min(5, tone + 3)]]
            c.box(max(0, x), y0, min(T - 1, x + 14), y0 + 6, ramp, top=1)
            if r.random() < 0.25:
                c.px(r.randint(max(1, x + 2), max(2, min(30, x + 12))), y0 + r.randint(2, 4), p.stone[1])
            x += 16
    if p.t["decor"] in ("moss", "garden", "grass"):
        g = px.ramp("#5f8a3a", 4)
        for _ in range(4):
            x = r.randint(0, 30); c.px(x, 7 + 8 * r.randint(0, 3), g[2])


def door(c, p, seed, closed):
    floor(c, p, seed, 0)
    # stone jambs left/right, passage vertical (rotated by the renderer for side walls)
    for x0 in (0, 25):
        c.box(x0, 0, x0 + 6, T - 1, p.stone[1:], top=2)
    if closed:
        for i in range(4):
            c.box(7 + i * 4 + (1 if i else 0), 1, 7 + i * 4 + 4, T - 2, p.wood, top=1)
        for y in (6, 25):
            c.rect(8, y, 23, y + 1, p.metal[2])
        c.px(20, 15, p.metal[4]); c.px(20, 16, p.metal[2])
    else:
        c.rect(7, 0, 24, T - 1, (12, 9, 12, 255))
        for i in range(5):
            c.rect(7, T - 1 - i, 24, T - 1 - i, p.mortar[min(3, i // 2)])


def build(kind, p, theme, var):
    seed = hash((theme, kind, var)) & 0xffffff
    c = px.Canvas(T, T)
    if kind not in ("pit", "water", "wall"):
        floor(c, p, seed ^ 0x55, var)
    if kind == "floor":
        floor(c, p, seed, var)
    elif kind == "rock":
        rock(c, p, seed)
    elif kind == "marked_rock":
        rock(c, p, seed, marked=True)
    elif kind == "block":
        block(c, p)
    elif kind == "pit":
        pit(c, p)
    elif kind == "spikes":
        spikes(c, p, seed)
    elif kind == "prop":
        (barrel if var == 0 else urn)(c, p, seed)
    elif kind == "explosive":
        barrel(c, p, seed, explosive=True)
    elif kind == "fire":
        fire(c, p, seed)
    elif kind == "water":
        water(c, p, seed, var)
    elif kind == "wall":
        wall(c, p, seed, var)
    elif kind == "door_open":
        door(c, p, seed, False)
    elif kind == "door_closed":
        door(c, p, seed, True)
    return c


def main():
    themes = sys.argv[1:] or list(THEMES)
    for theme in themes:
        p = Pal(THEMES[theme])
        for kind, n in VARIANTS.items():
            frames = [build(kind, p, theme, v) for v in range(n)]
            px.save_strip(frames, os.path.join(px.TILES, theme, kind + ".png"))
        print("tiles", theme, flush=True)


if __name__ == "__main__":
    main()
