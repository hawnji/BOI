"""Pixel-art player characters: 32x32 frames, 4-frame idle (breathing + blink) and 4-frame walk.

Usage: python Tools/PixelArt/gen_characters.py [id ...]
Output: Assets/Resources/Sprites/character_<id>.png (idle strip) and character_<id>_move.png (walk strip);
player.png / player_move.png are the Wanderer.
"""
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(__file__))
import px  # noqa: E402

S = 32
SKINS = {"warm": "#e9b38c", "pale": "#f1dcc6", "ash": "#9b9088", "green": "#9cc48a", "porcelain": "#f4eee2", "grey": "#8e88a2", "dark": "#a8714f"}

CHARS = {
    # id: cloak, skin, head style, accent, prop, extras
    "wanderer":      dict(cloak="#7d5a3c", skin="warm", head="hood", hood="#5e432c", accent="#c9a35a", prop="staff"),
    "ashwright":     dict(cloak="#4d4643", skin="dark", head="cap", hair="#3a2e28", accent="#ff8a2a", prop="flames", apron="#6a4128"),
    "thornmaid":     dict(cloak="#3f7a3a", skin="green", head="hair", hair="#2d5a26", accent="#8bc85a", prop="thorns"),
    "hollow_saint":  dict(cloak="#e9e0c8", skin="porcelain", head="hood", hood="#d3c8ac", accent="#f2c24a", prop="halo", hollow_eyes=True),
    "tinker":        dict(cloak="#3d5f86", skin="warm", head="cap", hair="#6b4a2b", accent="#9aa4b0", prop="bomb", goggles=True),
    "gambler":       dict(cloak="#62307a", skin="pale", head="tophat", hair="#1e1a22", accent="#e8b440", prop="coin"),
    "bellwarden":    dict(cloak="#6e737c", skin="warm", head="helm", hair="#8c919a", accent="#c08a3e", prop="bell"),
    "moth_seer":     dict(cloak="#2c3d72", skin="pale", head="hood", hood="#23315d", accent="#e8dcb0", prop="moths", blindfold=True),
    "beastcaller":   dict(cloak="#7a5534", skin="warm", head="hood", hood="#b08a5a", accent="#9ff0ff", prop="wisp"),
    "quickling":     dict(cloak="#2f4c37", skin="warm", head="hood", hood="#243a2b", accent="#c0392b", prop="dagger", scarf=True),
    "lanternbearer": dict(cloak="#e4d8b8", skin="warm", head="hood", hood="#cbbb94", accent="#ffd27a", prop="lantern"),
    "the_unmade":    dict(cloak="#4a4258", skin="grey", head="hood", hood="#3a3348", accent="#d8b0ff", prop="rags", glow_eyes=True),
}


def draw(spec, frame, walking):
    c = px.Canvas(S, S)
    cloak = px.ramp(spec["cloak"])
    skin = px.ramp(SKINS[spec["skin"]], spread=0.3)
    acc = px.ramp(spec["accent"])
    # animation offsets
    if walking:
        bob = (0, 1, 0, 1)[frame]
        step = (-1, 0, 1, 0)[frame]
        swing = (1, 0, -1, 0)[frame]
    else:
        bob = (0, 0, 1, 1)[frame]
        step, swing = 0, 0
    blink = (not walking and frame == 3)
    cx = 16
    c.shadow(cx, 29, 7, 2.2)
    # feet
    shoe = px.ramp(spec.get("hood", spec["cloak"]), spread=0.35)
    c.ellipse(cx - 3 + (step if walking else 0), 27 - (1 if walking and step > 0 else 0), 2.2, 1.6, shoe, dither=False)
    c.ellipse(cx + 3 - (step if walking else 0), 27 - (1 if walking and step < 0 else 0), 2.2, 1.6, shoe, dither=False)
    # body
    by = 21 + bob
    c.ellipse(cx, by, 6.2, 5.6, cloak)
    if spec.get("apron"):
        c.ellipse(cx, by + 1, 3.2, 4, px.ramp(spec["apron"]), dither=False)
    c.rect(cx - 5, by + 1, cx + 5, by + 1, cloak[1])  # belt line
    c.px(cx, by + 1, acc[3])
    # hands
    hy = by - 1
    c.ellipse(cx - 6.5, hy + swing, 1.8, 1.8, skin, dither=False)
    c.ellipse(cx + 6.5, hy - swing, 1.8, 1.8, skin, dither=False)
    # props behind/in hand
    prop = spec.get("prop")
    if prop == "staff":
        c.line(cx + 8, 7 + bob - swing, cx + 8, 28, px.ramp("#7a5432")[1])
        c.line(cx + 9, 7 + bob - swing, cx + 9, 28, px.ramp("#7a5432")[3])
        c.px(cx + 10, 6 + bob - swing, px.ramp("#7a5432")[3]); c.px(cx + 10, 5 + bob - swing, px.ramp("#7a5432")[2])
    # head
    hy = 12 + bob
    head_r = 7.2
    if spec["head"] == "helm":
        c.ellipse(cx, hy, head_r, 6.8, px.ramp(spec["hair"]))
        c.rect(cx - 5, hy, cx + 5, hy + 1, px.OUTLINE)
        c.px(cx - 2, hy, spec["accent"] and acc[4]); c.px(cx + 2, hy, acc[4])
    else:
        c.ellipse(cx, hy, head_r, 6.8, skin)
    if spec["head"] == "hood":
        hood = px.ramp(spec["hood"])
        # hood: shell over top and sides, open face
        m = c.ellipse(cx, hy - 1, head_r + 1.3, 7.8, hood)
        c.ellipse(cx, hy + 1.6, 5.4, 4.8, skin)
        c.px(cx, hy - 9, hood[2]); c.px(cx, hy - 8, hood[3])
    elif spec["head"] == "hair":
        hair = px.ramp(spec["hair"])
        c.ellipse(cx, hy - 3, head_r + 0.4, 4.5, hair)
        c.ellipse(cx - 5.5, hy, 2, 3.5, hair); c.ellipse(cx + 5.5, hy, 2, 3.5, hair)
    elif spec["head"] == "cap":
        c.ellipse(cx, hy - 3.5, head_r, 4, px.ramp(spec["hair"]))
        c.rect(cx - 7, hy - 1, cx + 3, hy - 1, px.ramp(spec["hair"])[1])
    elif spec["head"] == "tophat":
        hat = px.ramp(spec["hair"], spread=0.3)
        c.rect(cx - 7, hy - 5, cx + 7, hy - 4, hat[2])
        c.box(cx - 4, hy - 13, cx + 4, hy - 5, hat)
        c.rect(cx - 4, hy - 7, cx + 4, hy - 6, acc[3])
    # face
    ey = hy + 1
    if spec.get("blindfold"):
        c.rect(cx - 6, ey - 1, cx + 6, ey + 1, px.ramp("#e2d6b0")[3])
        c.rect(cx - 6, ey + 1, cx + 6, ey + 1, px.ramp("#e2d6b0")[1])
    elif spec.get("goggles"):
        for ex in (cx - 3, cx + 3):
            c.ellipse(ex, ey, 2.3, 2.3, px.ramp("#8a9098"), dither=False)
            c.ellipse(ex, ey, 1.3, 1.3, px.ramp("#8fe0ff"), dither=False)
    else:
        for ex in (cx - 3, cx + 2):
            if blink:
                c.rect(ex, ey + 1, ex + 1, ey + 1, px.OUTLINE)
            elif spec.get("glow_eyes"):
                c.rect(ex, ey - 1, ex + 1, ey + 1, acc[4]); c.px(ex, ey - 1, (255, 255, 255, 255))
            elif spec.get("hollow_eyes"):
                c.rect(ex, ey - 1, ex + 1, ey + 1, (20, 14, 18, 255))
            else:
                c.rect(ex, ey - 1, ex + 1, ey + 1, (34, 22, 30, 255))
                c.px(ex, ey - 1, (250, 250, 250, 255))
        if not spec.get("glow_eyes") and not spec.get("hollow_eyes"):
            c.px(cx - 4, ey + 2, skin[1]); c.px(cx + 4, ey + 2, skin[1])  # cheeks
            c.ellipse(cx - 4.3, ey + 2.5, 1.2, 0.8, px.ramp("#e07a7a"), dither=False)
            c.ellipse(cx + 4.3, ey + 2.5, 1.2, 0.8, px.ramp("#e07a7a"), dither=False)
    if spec.get("scarf"):
        sc = px.ramp(spec["accent"])
        c.ellipse(cx, by - 4.5, 6, 2, sc, dither=False)
        c.rect(cx + 3, by - 3, cx + 4, by + 2 - swing, sc[2])
    # props in front
    if prop == "flames":
        for hx, sw in ((cx - 7, swing), (cx + 7, -swing)):
            f = (frame % 2)
            c.ellipse(hx, by - 4 + sw - f, 1.6, 2.6, px.ramp("#ff9a2a", spread=0.5), dither=False)
            c.px(hx, by - 3 + sw - f, (255, 240, 160, 255))
    elif prop == "thorns":
        th = px.ramp("#5a3d22")
        for tx in range(cx - 6, cx + 7, 3):
            c.px(tx, hy - 7 - (1 if (tx // 3) % 2 else 0), th[2]); c.px(tx, hy - 6, th[1])
        leaf = px.ramp("#7ec850")
        c.ellipse(cx - 5, hy - 5, 1.6, 1, leaf, dither=False); c.ellipse(cx + 5, hy - 5, 1.6, 1, leaf, dither=False)
    elif prop == "halo":
        y = 2 + (frame % 2 if not walking else 0)
        for x in range(cx - 5, cx + 6):
            c.px(x, y, acc[4] if abs(x - cx) < 4 else acc[3])
        c.px(cx - 6, y + 1, acc[3]); c.px(cx + 6, y + 1, acc[3])
    elif prop == "bomb":
        c.ellipse(cx + 9, by + 2, 3, 3, px.ramp("#2c2c34", spread=0.3))
        c.px(cx + 10, by - 2, (255, 200 if frame % 2 else 120, 60, 255)); c.px(cx + 9, by - 1, px.ramp("#c8b080")[2])
    elif prop == "coin":
        cy = by - 5 - (abs(frame - 1.5) > 1 and 2 or 0)
        c.ellipse(cx + 8, cy, 1.8 if frame % 2 == 0 else 0.9, 1.8, acc, dither=False)
    elif prop == "bell":
        b = px.ramp("#b5803a")
        c.ellipse(cx + 9, by, 3, 3.5, b)
        c.rect(cx + 6, by + 3, cx + 12, by + 3, b[1]); c.px(cx + 9, by + 4, b[0])
    elif prop == "moths":
        w = px.ramp("#ecdcb0")
        for (mx, my, ph) in ((4, 6, 0), (27, 9, 1), (24, 3, 2)):
            o = (frame + ph) % 4
            up = o in (0, 1)
            c.px(mx, my + (o % 2), (70, 52, 40, 255))
            c.px(mx - 1, my - (1 if up else 0) + (o % 2), w[3]); c.px(mx + 1, my - (1 if up else 0) + (o % 2), w[3])
    elif prop == "wisp":
        g = px.ramp("#9ff0ff", spread=0.5)
        wy = 8 + (0, 1, 2, 1)[frame]
        c.ellipse(4, wy, 2.4, 2.4, g, dither=False); c.px(3, wy - 1, (255, 255, 255, 255))
    elif prop == "dagger":
        st = px.ramp("#b8c0c8")
        c.line(cx + 7, by - 1 - swing, cx + 10, by - 5 - swing, st[4]); c.px(cx + 7, by - swing, (80, 50, 30, 255))
    elif prop == "lantern":
        ly = by + 2 - swing
        c.line(cx + 7, ly - 4, cx + 8, ly - 2, (60, 60, 66, 255))
        c.box(cx + 6, ly - 2, cx + 10, ly + 3, px.ramp("#ffd27a", spread=0.35))
        c.px(cx + 8, ly, (255, 255, 220, 255) if frame % 2 else (255, 230, 150, 255))
    elif prop == "rags":
        rg = px.ramp(spec["cloak"])
        for rx in range(cx - 6, cx + 7, 2):
            c.px(rx, by + 5 + ((rx + frame) % 2), rg[1])
    c.outline()
    return c


def main():
    ids = sys.argv[1:] or list(CHARS)
    for cid in ids:
        spec = CHARS[cid]
        idle = [draw(spec, f, False) for f in range(4)]
        walk = [draw(spec, f, True) for f in range(4)]
        base = os.path.join(px.SPRITES, "character_" + cid)
        px.save_strip(idle, base + ".png")
        px.save_strip(walk, base + "_move.png")
        if cid == "wanderer":
            shutil.copyfile(base + ".png", os.path.join(px.SPRITES, "player.png"))
            shutil.copyfile(base + "_move.png", os.path.join(px.SPRITES, "player_move.png"))
        print("character", cid, flush=True)


if __name__ == "__main__":
    main()
