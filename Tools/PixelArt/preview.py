"""Preview sheet: python preview.py out.png scale strip1.png strip2.png ...  (one strip per row)."""
import sys
from PIL import Image
out, scale, files = sys.argv[1], int(sys.argv[2]), sys.argv[3:]
ims = [Image.open(f).convert("RGBA") for f in files]
W = max(i.width for i in ims) * scale
H = sum(i.height for i in ims) * scale + 2 * len(ims)
sheet = Image.new("RGBA", (W, H), (58, 52, 62, 255))
y = 0
for im in ims:
    sheet.alpha_composite(im.resize((im.width * scale, im.height * scale), Image.NEAREST), (0, y))
    y += im.height * scale + 2
sheet.save(out)
