"""Builds a scaled-up preview sheet of rendered PNGs: python contact_sheet.py out.png scale file1.png file2.png ..."""
import sys
from PIL import Image

out, scale, files = sys.argv[1], int(sys.argv[2]), sys.argv[3:]
ims = [Image.open(f).convert("RGBA") for f in files]
w = max(i.width for i in ims) * scale
cols = min(len(ims), max(1, 1024 // w))
rows = (len(ims) + cols - 1) // cols
sheet = Image.new("RGBA", (cols * w, rows * w), (40, 40, 48, 255))
for n, im in enumerate(ims):
    im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    sheet.alpha_composite(im, ((n % cols) * w, (n // cols) * w))
sheet.save(out)
