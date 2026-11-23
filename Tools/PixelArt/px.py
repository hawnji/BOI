"""Tiny pixel-art engine used by Hollowcrawl's art generators (pip install pillow numpy).

Sprites are painted with shaded primitives (lit from the upper left, quantised to a hue-shifted colour ramp with
ordered dithering), then given a dark outline, like hand-made pixel art. Frames are saved as horizontal strips.
"""
import colorsys
import math
import os
import random

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SPRITES = os.path.join(REPO, "Assets", "Resources", "Sprites")
TILES = os.path.join(REPO, "Assets", "Resources", "Tiles")

OUTLINE = (24, 16, 26, 255)
LIGHT = np.array([-0.55, -0.65, 0.52])
LIGHT = LIGHT / np.linalg.norm(LIGHT)
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


def hexrgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def ramp(base, n=5, spread=0.42, hue_shift=0.06):
    """Colour ramp dark -> light around a base colour, shadows shifted toward blue/purple and highlights toward
    yellow (classic pixel-art palette practice)."""
    r, g, b = (c / 255 for c in (hexrgb(base) if isinstance(base, str) else base))
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    out = []
    for i in range(n):
        t = i / (n - 1) - 0.5  # -0.5 .. 0.5
        hh = (h - hue_shift * t * 2 + (0.66 - h) * 0.0) % 1.0
        # shadows drift toward 0.7 (blue-violet), lights toward 0.15 (warm)
        target = 0.72 if t < 0 else 0.13
        hh = (h + (((target - h + 0.5) % 1.0) - 0.5) * abs(t) * hue_shift * 4) % 1.0
        ss = min(1, max(0, s + (0.12 if t < 0 else -0.18) * abs(t) * 2))
        vv = min(1, max(0.03, v + t * spread * 2 * (0.9 if t < 0 else 0.8)))
        rr, gg, bb = colorsys.hsv_to_rgb(hh, ss, vv)
        out.append((int(rr * 255), int(gg * 255), int(bb * 255), 255))
    return out


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.a = np.zeros((h, w, 4), dtype=np.uint8)

    # ------------------------------------------------------------------ basics
    def px(self, x, y, col):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            self.a[y, x] = col if len(col) == 4 else tuple(col) + (255,)

    def get(self, x, y):
        return tuple(self.a[y, x])

    def rect(self, x0, y0, x1, y1, col):
        x0, y0, x1, y1 = (int(round(v)) for v in (x0, y0, x1, y1))
        x0, x1 = max(0, min(x0, x1)), min(self.w, max(x0, x1) + 1)
        y0, y1 = max(0, min(y0, y1)), min(self.h, max(y0, y1) + 1)
        self.a[y0:y1, x0:x1] = col if len(col) == 4 else tuple(col) + (255,)

    def line(self, x0, y0, x1, y1, col):
        n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
        for i in range(n + 1):
            t = i / max(1, n)
            self.px(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, col)

    # ------------------------------------------------------------------ shaded shapes
    def _shade(self, mask, nx, ny, nz, rmp, dither=True, ambient=0.18):
        lit = np.clip(nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2], 0, 1)
        lit = ambient + (1 - ambient) * lit
        n = len(rmp)
        f = lit * (n - 1)
        if dither:
            yy, xx = np.mgrid[0:self.h, 0:self.w]
            f = f + (BAYER[yy % 4, xx % 4] - 0.5) * 0.45
        idx = np.clip(np.round(f), 0, n - 1).astype(int)
        cols = np.array(rmp, dtype=np.uint8)[idx]
        self.a[mask] = cols[mask]

    def ellipse(self, cx, cy, rx, ry, rmp, dither=True, flat=False):
        """Filled, shaded ellipse (a sphere seen from above-front). Coordinates may be fractional."""
        yy, xx = np.mgrid[0:self.h, 0:self.w]
        dx = (xx + 0.5 - cx) / max(rx, 0.01)
        dy = (yy + 0.5 - cy) / max(ry, 0.01)
        d2 = dx * dx + dy * dy
        mask = d2 <= 1.0
        nz = np.sqrt(np.clip(1 - d2, 0, 1))
        if flat:
            nz = np.ones_like(nz)
            dx = dx * 0.25
            dy = dy * 0.25
        self._shade(mask, dx, dy, nz, rmp, dither)
        return mask

    def box(self, x0, y0, x1, y1, rmp, top=2, dither=False):
        """Shaded block: lit top-left edges, dark bottom-right edges, flat face in the middle."""
        x0, y0, x1, y1 = (int(round(v)) for v in (x0, y0, x1, y1))
        n = len(rmp)
        self.rect(x0, y0, x1, y1, rmp[n // 2])
        self.rect(x0, y0, x1, y0 + top - 1, rmp[n - 1])
        self.rect(x0, y0, x0, y1, rmp[n - 2])
        self.rect(x0, y1 - top + 1, x1, y1, rmp[0])
        self.rect(x1, y0, x1, y1, rmp[1])

    def poly(self, pts, rmp, tone=None):
        """Filled polygon, flat tone (index into ramp, default middle)."""
        from PIL import ImageDraw
        img = Image.new("L", (self.w, self.h), 0)
        ImageDraw.Draw(img).polygon([(x, y) for x, y in pts], fill=255)
        mask = np.array(img) > 0
        col = rmp[len(rmp) // 2 if tone is None else tone]
        self.a[mask] = col
        return mask

    def noise_fill(self, mask, rmp, seed, density=0.12, tones=(0, 1)):
        r = random.Random(seed)
        ys, xs = np.nonzero(mask)
        for y, x in zip(ys, xs):
            if r.random() < density:
                self.a[y, x] = rmp[r.choice(tones)]

    # ------------------------------------------------------------------ finishing
    def outline(self, col=OUTLINE, diagonal=False):
        alpha = self.a[:, :, 3] > 0
        grow = np.zeros_like(alpha)
        shifts = [(1, 0), (-1, 0), (0, 1), (0, -1)] + ([(1, 1), (1, -1), (-1, 1), (-1, -1)] if diagonal else [])
        for dy, dx in shifts:
            grow |= np.roll(np.roll(alpha, dy, 0), dx, 1)
        edge = grow & ~alpha
        self.a[edge] = col

    def shadow(self, cx, cy, rx, ry, alpha=90):
        """Soft-edged drop shadow ellipse painted *under* existing pixels."""
        yy, xx = np.mgrid[0:self.h, 0:self.w]
        d = ((xx + 0.5 - cx) / rx) ** 2 + ((yy + 0.5 - cy) / ry) ** 2
        mask = (d <= 1.0) & (self.a[:, :, 3] == 0)
        self.a[mask] = (12, 8, 16, alpha)

    def paste(self, other, ox, oy, flip=False):
        src = other.a[:, ::-1] if flip else other.a
        h, w = src.shape[:2]
        for y in range(h):
            ty = y + oy
            if not 0 <= ty < self.h:
                continue
            for x in range(w):
                tx = x + ox
                if 0 <= tx < self.w and src[y, x, 3] > 0:
                    self.a[ty, tx] = src[y, x]

    def image(self):
        return Image.fromarray(self.a, "RGBA")


def save_strip(frames, path):
    """Saves canvases side by side as one horizontal animation strip."""
    w, h = frames[0].w, frames[0].h
    strip = Image.new("RGBA", (w * len(frames), h), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        strip.paste(f.image(), (i * w, 0))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    strip.save(path)


def save(canvas, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    canvas.image().save(path)


def rng(seed):
    return random.Random(seed)
