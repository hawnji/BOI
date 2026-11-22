#!/usr/bin/env python3
"""Generates Content/Rooms/expansion_rooms.json: themed combat rooms for all 15 floors plus extra special rooms.

Layouts are built from mirrored obstacle motifs. Every cell keeps its two midlines clear so all doors connect,
and every marker is placed on a tile reachable from the doors (the C# RoomTemplateValidator re-checks this).
Run from the repo root: python3 Tools/ContentGen/gen_rooms.py
"""
import json, random

CW, CH = 15, 9
SHAPES = {"Single": [(0, 0)], "Wide": [(0, 0), (1, 0)], "Tall": [(0, 0), (0, 1)], "Large": [(0, 0), (1, 0), (0, 1), (1, 1)],
          "LNoNE": [(0, 0), (0, 1), (1, 1)], "LNoNW": [(1, 0), (0, 1), (1, 1)], "LNoSE": [(0, 0), (1, 0), (0, 1)], "LNoSW": [(0, 0), (1, 0), (1, 1)]}
WALK = set(".ECIW")

# theme -> (difficulty min, max, obstacle palette weights)
THEMES = {
    "crypt":    (1, 3,  {"#": 5, "X": 2, "B": 2, "S": 1}),
    "ossuary":  (1, 3,  {"#": 4, "X": 3, "S": 2, "B": 1}),
    "mire":     (1, 4,  {"W": 5, "P": 3, "#": 2}),
    "forge":    (2, 4,  {"F": 3, "K": 2, "X": 3, "#": 2}),
    "frost":    (2, 5,  {"P": 3, "X": 3, "#": 3, "S": 1}),
    "garden":   (2, 5,  {"B": 4, "#": 3, "W": 2}),
    "belfry":   (3, 6,  {"X": 4, "P": 3, "#": 2}),
    "chapel":   (3, 6,  {"W": 5, "X": 2, "#": 2}),
    "orchard":  (3, 7,  {"B": 3, "#": 4, "S": 2}),
    "hive":     (4, 7,  {"#": 4, "P": 2, "B": 2}),
    "glass":    (4, 8,  {"X": 5, "S": 2, "P": 2}),
    "void":     (4, 8,  {"P": 5, "X": 2, "#": 1}),
    "throne":   (5, 9,  {"F": 3, "X": 3, "K": 2, "#": 2}),
    "heart":    (5, 9,  {"P": 3, "#": 3, "S": 2}),
    "last":     (6, 10, {"P": 3, "X": 3, "F": 2, "S": 2}),
}

rng = random.Random(20261122)
ROOMS = []


def blank(shape):
    cells = SHAPES[shape]
    w = CW * (max(c[0] for c in cells) + 1)
    h = CH * (max(c[1] for c in cells) + 1)
    g = [[" "] * w for _ in range(h)]
    for cx, cy in cells:
        for y in range(cy * CH, cy * CH + CH):
            for x in range(cx * CW, cx * CW + CW):
                g[y][x] = "."
    return g, cells, w, h


def protected(cells, x, y):
    """Midlines of every cell (door corridors) and the ring right next to each door stay clear."""
    for cx, cy in cells:
        if cx * CW <= x < cx * CW + CW and cy * CH <= y < cy * CH + CH:
            lx, ly = x - cx * CW, y - cy * CH
            return lx == CW // 2 or ly == CH // 2
    return True


def doors(cells):
    s = set(cells)
    out = []
    for cx, cy in cells:
        ox, oy = cx * CW, cy * CH
        if (cx, cy - 1) not in s: out.append((ox + CW // 2, oy))
        if (cx, cy + 1) not in s: out.append((ox + CW // 2, oy + CH - 1))
        if (cx - 1, cy) not in s: out.append((ox, oy + CH // 2))
        if (cx + 1, cy) not in s: out.append((ox + CW - 1, oy + CH // 2))
    return out


def flood(g, start):
    h, w = len(g), len(g[0])
    seen = [[False] * w for _ in range(h)]
    st = [start]
    seen[start[1]][start[0]] = True
    while st:
        x, y = st.pop()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and g[ny][nx] in WALK:
                seen[ny][nx] = True
                st.append((nx, ny))
    return seen


def pick(pal):
    keys = list(pal)
    return rng.choices(keys, weights=[pal[k] for k in keys])[0]


MOTIFS = ["pillars", "blocks", "ring", "diagonal", "corners", "scatter", "lanes", "checker"]


def paint(g, cells, w, h, pal, motif):
    sym = rng.choice(["both", "both", "x", "y"])
    tile = pick(pal)
    alt = pick(pal)

    def put(x, y, t):
        pts = {(x, y)}
        if sym in ("both", "x"): pts.add((w - 1 - x, y))
        if sym in ("both", "y"): pts.add((x, h - 1 - y))
        if sym == "both": pts.add((w - 1 - x, h - 1 - y))
        for px, py in pts:
            if 0 <= px < w and 0 <= py < h and g[py][px] == "." and not protected(cells, px, py):
                g[py][px] = t

    if motif == "pillars":
        for x in range(2, w // 2, 4):
            for y in range(2, h // 2, 3): put(x, y, tile)
    elif motif == "blocks":
        for _ in range(rng.randint(2, 4)):
            bx, by = rng.randint(1, w // 2 - 2), rng.randint(1, h // 2 - 1)
            for dx in range(rng.randint(1, 3)):
                for dy in range(rng.randint(1, 2)): put(bx + dx, by + dy, tile)
    elif motif == "ring":
        for x in range(3, w // 2): put(x, 2, tile)
        for y in range(2, h // 2): put(3, y, alt)
    elif motif == "diagonal":
        for i in range(1, min(w, h) // 2): put(i + 1, i, tile)
    elif motif == "corners":
        for dx in range(3):
            for dy in range(2): put(1 + dx, 1 + dy, tile)
        put(5, 3, alt)
    elif motif == "scatter":
        for _ in range(rng.randint(5, 10)): put(rng.randint(1, w // 2), rng.randint(1, h // 2), pick(pal))
    elif motif == "lanes":
        for x in range(1, w // 2 - 1): put(x, 2, tile)
    elif motif == "checker":
        for x in range(1, w // 2, 2):
            for y in range(1, h // 2, 2):
                if (x + y) % 4 == 0: put(x, y, tile)
    # occasional reward tiles and props
    if rng.random() < 0.25: put(rng.randint(1, w // 2 - 1), rng.randint(1, h // 2 - 1), "T")
    if rng.random() < 0.35: put(rng.randint(1, w // 2 - 1), rng.randint(1, h // 2 - 1), "B")


def place(g, cells, w, h, code, n, min_door_dist=3):
    ds = doors(cells)
    reach = flood(g, ds[0])
    spots = [(x, y) for y in range(h) for x in range(w)
             if g[y][x] == "." and reach[y][x] and all(abs(x - dx) + abs(y - dy) >= min_door_dist for dx, dy in ds)]
    rng.shuffle(spots)
    for x, y in spots[:n]: g[y][x] = code
    return min(n, len(spots))


def valid(g, cells):
    ds = doors(cells)
    for x, y in ds:
        if g[y][x] not in WALK: return False
    reach = flood(g, ds[0])
    if not all(reach[y][x] for x, y in ds): return False
    for y, row in enumerate(g):
        for x, c in enumerate(row):
            if c in "ECI" and not reach[y][x]: return False
    return True


def room(id, shape, types, themes, difficulty, enemies=0, interact=0, pickups=0, pal=None, motif=None, weight=1.0, tags=()):
    for attempt in range(40):
        g, cells, w, h = blank(shape)
        if pal: paint(g, cells, w, h, pal, motif or rng.choice(MOTIFS))
        if interact:
            if interact == 1 and g[h // 2 - (0 if h == CH else 0)][w // 2] == ".": pass
            placed = 0
            # interaction points near the centre first
            cx, cy = w // 2, h // 2
            for x, y in sorted(((x, y) for y in range(h) for x in range(w) if g[y][x] == "."), key=lambda p: (abs(p[0] - cx) + abs(p[1] - cy), p)):
                if placed >= interact: break
                if (x - cx) % 2 == 0 and y in (cy - 2, cy + 2, cy - 1, cy + 1) and not protected(cells, x, y):
                    g[y][x] = "I"; placed += 1
            if placed < interact: continue
        if enemies and place(g, cells, w, h, "E", enemies) < enemies: continue
        if pickups: place(g, cells, w, h, "C", pickups, 2)
        if valid(g, cells):
            ROOMS.append({"id": id, "shape": shape, "types": list(types), "themes": list(themes), "difficulty": difficulty, "weight": weight,
                          "tags": list(tags), "blockedDoors": [], "layout": ["".join(r) for r in g]})
            return
    raise SystemExit("could not build " + id)


COMBAT_SHAPES = ["Single"] * 8 + ["Wide", "Wide", "Tall", "Tall", "Large", "LNoNE", "LNoSW"]
for theme, (dmin, dmax, pal) in THEMES.items():
    n = 14
    for i in range(n):
        shape = COMBAT_SHAPES[i % len(COMBAT_SHAPES)]
        diff = dmin + (i * (dmax - dmin + 1)) // n
        size = len(SHAPES[shape])
        enemies = min(14, rng.randint(2, 3) + diff // 2 + 2 * (size - 1))
        # hazard tiles are fine in combat rooms but keep spikes/fire rare at low difficulty
        p = dict(pal)
        if diff <= 2: p.pop("S", None); p.pop("F", None); p.pop("K", None)
        if not p: p = {"#": 1}
        room(f"{theme}_combat_{i + 1:02d}", shape, ["Combat"], [theme], diff, enemies=enemies, pickups=rng.randint(0, 1), pal=p)

GEN = {"#": 4, "X": 2, "B": 2}
specials = [
    ("treasure_pillared", "Single", ["Treasure"], 1, dict(interact=1, pal={"X": 1}, motif="corners")),
    ("treasure_garden", "Single", ["Treasure"], 1, dict(interact=1, pal={"B": 1}, motif="pillars")),
    ("shop_long_counter", "Single", ["Shop"], 1, dict(interact=4, pal={"X": 1}, motif="corners")),
    ("shop_stalls", "Single", ["Shop"], 1, dict(interact=3, pal={"B": 1}, motif="corners")),
    ("boss_pillared_hall", "Single", ["Boss"], 3, dict(enemies=1, pal={"X": 1}, motif="corners")),
    ("boss_great_arena", "Large", ["Boss"], 5, dict(enemies=1, pal={"X": 1}, motif="pillars")),
    ("boss_wide_nave", "Wide", ["Boss"], 4, dict(enemies=1, pal={"X": 1}, motif="corners")),
    ("lair_rubble", "Single", ["Miniboss"], 2, dict(enemies=1, pal={"#": 1}, motif="corners")),
    ("lair_long", "Wide", ["Miniboss"], 4, dict(enemies=1, pal={"X": 1}, motif="pillars")),
    ("secret_rubble_cache", "Single", ["Secret"], 1, dict(pickups=4, pal={"#": 1}, motif="corners")),
    ("secret_shrine", "Single", ["Secret"], 2, dict(interact=1, pickups=2)),
    ("vault_hidden_hoard", "Single", ["HiddenVault"], 2, dict(pickups=6, pal={"X": 1}, motif="pillars")),
    ("gauntlet_ring", "Single", ["Challenge"], 4, dict(enemies=6, pal={"#": 1}, motif="ring")),
    ("gauntlet_wide", "Wide", ["Challenge"], 6, dict(enemies=9, pal={"X": 1}, motif="pillars")),
    ("shrine_circle", "Single", ["Event"], 1, dict(interact=1, pal={"B": 1}, motif="corners")),
    ("spring_pool", "Single", ["Healing"], 1, dict(interact=1, pal={"W": 1}, motif="corners")),
]
for sid, shape, types, diff, kw in specials:
    room(sid, shape, types, ["*"], diff, **kw)

ids = [r["id"] for r in ROOMS]
assert len(ids) == len(set(ids))
json.dump({"rooms": ROOMS}, open("Assets/StreamingAssets/Content/Rooms/expansion_rooms.json", "w"), indent=1)
print(len(ROOMS), "rooms")
