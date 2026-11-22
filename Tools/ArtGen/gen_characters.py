"""Renders the 12 playable characters (plus the default `player`) as 128x128 sprites, high 3/4 view.

Usage: python Tools/ArtGen/gen_characters.py [id ...]
Output: Assets/Resources/Sprites/character_<id>.png (and player.png = the Wanderer)
Each character is a stylised big-headed figure assembled from smooth primitives, with its own
outfit colours and signature props.
"""
import math
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(__file__))
import hc_common as hc  # noqa: E402

SIZE = 128
SKIN = {"warm": "#e6b48f", "pale": "#efe2d2", "ash": "#8e8a86", "green": "#a9c79a", "porcelain": "#f4efe6", "grey": "#9a95a8"}


def figure(cloak, skin="warm", hood=True, trim=None, eye="#1a1416", eye_glow=None, hood_col=None, face=True):
    m_cloak = hc.mat("cloak", hc.hexcol(cloak), rough=0.8, noise=0.2, bump=0.25, scale=25)
    m_hood = hc.mat("hood", hc.hexcol(hood_col or cloak), rough=0.8, noise=0.2, bump=0.25, scale=25)
    m_skin = hc.mat("skin", hc.hexcol(SKIN.get(skin, skin)), rough=0.5, noise=0.05, bump=0.02, sss=0.2)
    m_trim = hc.mat("trim", hc.hexcol(trim), rough=0.35, metal=0.8, noise=0.1, bump=0.1) if trim else None
    # body: flared cloak
    hc.cyl((0, 0, 0.2), 0.27, 0.4, m_cloak, verts=40, r2=0.13)
    body = hc.bpy.context.active_object
    s = body.modifiers.new("Sub", "SUBSURF"); s.levels = s.render_levels = 2
    if m_trim:
        hc.torus((0, -0.01, 0.3), 0.175, 0.022, m_trim)  # belt
    # feet and hands
    for sx in (-0.1, 0.1):
        hc.sphere((sx, -0.08, 0.02), 0.07, m_hood, scale=(1, 1.3, 0.6))
    for sx in (-0.24, 0.24):
        hc.sphere((sx, -0.05, 0.26), 0.065, m_skin)
    # head
    hc.sphere((0, -0.03, 0.55), 0.2, m_skin if face else m_hood)
    if hood:
        hc.sphere((0, 0.06, 0.6), 0.245, m_hood, scale=(1, 1, 1.05))
        hc.cone((0, 0.2, 0.75), 0.12, 0.25, m_hood, rot=(math.radians(-60), 0, 0))
    if face:
        m_eye = hc.glow_mat("eyeglow", hc.hexcol(eye_glow), 6) if eye_glow else hc.mat("eye", hc.hexcol(eye), rough=0.15, noise=0, bump=0)
        for sx in (-0.07, 0.07):
            hc.sphere((sx, -0.205, 0.55), 0.04, m_eye, scale=(1, 0.6, 1.25))
            if not eye_glow:
                hc.sphere((sx + 0.012, -0.235, 0.57), 0.011, hc.glow_mat("glint", (1, 1, 1), 3))
    return dict(cloak=m_cloak, skin=m_skin, trim=m_trim, hood=m_hood)


def staff(x, mat_wood, top=None, top_mat=None):
    hc.cyl((x, -0.08, 0.38), 0.022, 0.78, mat_wood, verts=12)
    if top == "orb":
        hc.sphere((x, -0.08, 0.8), 0.06, top_mat)
    elif top == "crook":
        hc.torus((x + 0.05, -0.08, 0.78), 0.05, 0.02, mat_wood, rot=(math.radians(90), 0, 0))


def build(cid):
    wood = hc.mat("wood", hc.hexcol("#6b4a2b"), rough=0.7, noise=0.3, bump=0.3, scale=40)
    gold = hc.mat("gold", hc.hexcol("#d8a840"), rough=0.25, metal=1.0, noise=0.1, bump=0.05)
    iron = hc.mat("iron", hc.hexcol("#7c7f86"), rough=0.35, metal=1.0, noise=0.1, bump=0.1)
    if cid == "wanderer":
        figure("#7a5a3a", "warm", trim="#a88a50", hood_col="#5f4630")
        staff(0.3, wood, "crook")
    elif cid == "ashwright":
        m = figure("#4a4442", "ash", hood=False, trim="#c2662a")
        hc.sphere((0, 0.02, 0.69), 0.16, hc.mat("hair", hc.hexcol("#2a2422"), rough=0.9), scale=(1.15, 1, 0.6))
        fire = hc.glow_mat("fire", hc.hexcol("#ff7a1a"), 5)
        for sx in (-0.24, 0.24):
            hc.cone((sx, -0.05, 0.36), 0.05, 0.14, fire, verts=10)
        hc.box((0, -0.2, 0.18), (0.22, 0.03, 0.26), hc.mat("apron", hc.hexcol("#5a3a22"), rough=0.8), bevel=0.02)
    elif cid == "thornmaid":
        m = figure("#3f6b3a", "green", hood=False, trim="#2c4a26")
        hc.sphere((0, 0.04, 0.62), 0.215, hc.mat("hair", hc.hexcol("#2f4a22"), rough=0.8), scale=(1.05, 1, 0.9))
        thorn = hc.mat("thorn", hc.hexcol("#4a3520"), rough=0.6)
        for i in range(9):
            a = math.pi * (0.1 + 0.8 * i / 8)
            hc.cone((0.2 * math.cos(a), 0.05 - 0.05 * math.sin(a), 0.74 + 0.04 * math.sin(a)), 0.025, 0.14, thorn, verts=8)
        leaf = hc.mat("leaf", hc.hexcol("#6fa84a"), rough=0.5)
        for i in range(6):
            a = i * math.pi / 3
            hc.sphere((0.25 * math.cos(a), 0.25 * math.sin(a) - 0.02, 0.12), 0.06, leaf, scale=(1.6, 0.6, 0.3))
    elif cid == "hollow_saint":
        m = figure("#e8dfc8", "porcelain", trim="#d8a840", hood_col="#d6ccb3", eye="#0a0808")
        hc.torus((0, 0.12, 0.93), 0.16, 0.022, hc.glow_mat("halo", hc.hexcol("#ffe08a"), 6), rot=(math.radians(-25), 0, 0))
        hc.box((0, -0.25, 0.3), (0.05, 0.02, 0.18), gold, bevel=0.005)
        hc.box((0, -0.25, 0.34), (0.13, 0.02, 0.04), gold, bevel=0.005)
    elif cid == "tinker":
        m = figure("#3f5a7a", "warm", hood=False, trim="#7c7f86")
        hc.sphere((0, 0.04, 0.66), 0.19, hc.mat("cap", hc.hexcol("#6b4a2b"), rough=0.8), scale=(1.1, 1.1, 0.7))
        lens = hc.mat("lens", hc.hexcol("#9fe0ff"), rough=0.05, metal=0.2)
        for sx in (-0.08, 0.08):
            hc.torus((sx, -0.17, 0.66), 0.05, 0.016, iron, rot=(math.radians(80), 0, 0))
            hc.sphere((sx, -0.17, 0.66), 0.042, lens, scale=(1, 0.4, 1))
        hc.sphere((0.27, 0.0, 0.14), 0.09, hc.mat("bomb", hc.hexcol("#26262b"), rough=0.3, metal=0.3))
        hc.cyl((0.27, 0.0, 0.25), 0.012, 0.06, hc.glow_mat("fuse", hc.hexcol("#ffb040"), 4))
    elif cid == "gambler":
        m = figure("#5a2f6b", "pale", hood=False, trim="#d8a840")
        hc.cyl((0, 0.0, 0.79), 0.21, 0.025, hc.mat("brim", hc.hexcol("#1d1a20"), rough=0.6))
        hc.cyl((0, 0.0, 0.9), 0.14, 0.22, hc.mat("hat", hc.hexcol("#1d1a20"), rough=0.6))
        hc.cyl((0, 0.0, 0.82), 0.145, 0.04, gold)
        hc.cyl((0.24, -0.1, 0.33), 0.06, 0.012, gold, rot=(math.radians(70), 0, 0))
    elif cid == "bellwarden":
        m = figure("#6c7078", "warm", hood=False, trim="#b08a50")
        hc.sphere((0, 0.0, 0.62), 0.215, iron, scale=(1, 1, 0.85))
        hc.box((0, -0.19, 0.6), (0.2, 0.03, 0.05), hc.mat("visor", hc.hexcol("#15151a"), rough=0.4), bevel=0.01)
        bronze = hc.mat("bronze", hc.hexcol("#b5803a"), rough=0.3, metal=1.0)
        hc.cone((0, 0.22, 0.42), 0.2, 0.34, bronze, verts=32, r2=0.09)
        hc.sphere((0, 0.22, 0.6), 0.06, bronze)
    elif cid == "moth_seer":
        m = figure("#2b3a6b", "pale", trim="#8fa0d8", hood_col="#22305a", face=True)
        hc.box((0, -0.2, 0.555), (0.32, 0.04, 0.07), hc.mat("band", hc.hexcol("#d8d0b8"), rough=0.8), bevel=0.015)
        wing = hc.mat("wing", hc.hexcol("#e8dcb0"), rough=0.6, sss=0.3)
        for (x, y, z, a) in ((-0.32, -0.1, 0.7, 20), (0.3, -0.05, 0.85, -15), (0.18, -0.2, 0.95, 40)):
            for side in (-1, 1):
                hc.sphere((x + side * 0.04, y, z), 0.05, wing, scale=(1.3, 0.25, 0.9))
            hc.sphere((x, y, z), 0.015, hc.mat("mothbody", hc.hexcol("#4a3a2a")))
    elif cid == "beastcaller":
        m = figure("#7a5532", "warm", trim="#d8d0b8", hood_col="#a07a50")
        hc.sphere((0, 0.08, 0.76), 0.12, hc.mat("fur", hc.hexcol("#c8b090"), rough=1.0, noise=0.3, bump=0.6, scale=60), scale=(1.8, 1, 0.5))
        hc.sphere((-0.33, -0.05, 0.62), 0.07, hc.glow_mat("wisp", hc.hexcol("#9ff0ff"), 5))
        bone = hc.mat("bone", hc.hexcol("#e8dcc0"), rough=0.6)
        hc.sphere((0.3, 0.05, 0.78), 0.05, bone, scale=(1, 1.4, 1))
        for side in (-1, 1):
            hc.sphere((0.3 + side * 0.07, 0.06, 0.8), 0.05, bone, scale=(1.4, 0.4, 0.3))
    elif cid == "quickling":
        m = figure("#2e4a36", "warm", trim="#8a6a3a", hood_col="#243a2b")
        hc.torus((0, -0.02, 0.4), 0.17, 0.05, hc.mat("scarf", hc.hexcol("#a8322a"), rough=0.8))
        hc.box((0.27, -0.08, 0.3), (0.03, 0.02, 0.22), iron, bevel=0.005, rot=(0, math.radians(20), 0))
    elif cid == "lanternbearer":
        m = figure("#e2d6b6", "warm", trim="#c9a048", hood_col="#cfc09a")
        hc.cyl((0.28, -0.08, 0.33), 0.012, 0.16, iron)
        hc.cyl((0.28, -0.08, 0.2), 0.07, 0.13, hc.glow_mat("lamp", hc.hexcol("#ffd27a"), 5), verts=6)
        hc.cyl((0.28, -0.08, 0.28), 0.08, 0.025, iron, verts=6)
        hc.cyl((0.28, -0.08, 0.12), 0.08, 0.025, iron, verts=6)
    elif cid == "the_unmade":
        m = figure("#4a4258", "grey", trim=None, hood_col="#3a3348", eye_glow="#d8b0ff")
        rag = hc.mat("rag", hc.hexcol("#3a3348"), rough=0.9)
        for i in range(10):
            a = i * 2 * math.pi / 10
            hc.cone((0.25 * math.cos(a), 0.25 * math.sin(a), 0.03), 0.05, 0.12, rag, verts=6, rot=(math.radians(180), 0, 0))
    else:
        figure("#808080")


def main():
    ids = sys.argv[1:] or ["wanderer", "ashwright", "thornmaid", "hollow_saint", "tinker", "gambler", "bellwarden",
                           "moth_seer", "beastcaller", "quickling", "lanternbearer", "the_unmade"]
    for cid in ids:
        hc.reset(samples=32)
        hc.camera(top_down=False, ortho_scale=1.05, tilt_deg=40)
        cam = hc.bpy.context.scene.camera
        cam.location.z += 0.42  # aim at the figure's middle
        hc.lights(key=2.0)
        build(cid)
        path = os.path.join(hc.SPRITES, "character_" + cid + ".png")
        hc.render(path, SIZE)
        if cid == "wanderer":
            shutil.copyfile(path, os.path.join(hc.SPRITES, "player.png"))
        print("character", cid, flush=True)


if __name__ == "__main__":
    main()
