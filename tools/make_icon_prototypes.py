"""
Prototype-only icon explorations for the "Searchlight" rename.

Does NOT touch the live icon: writes previews to tools\\icon_previews\\ exclusively.
Reuses the current tile/gradient/spark language from make_icon.py so the
comparison is apples-to-apples.

Prototype 1 - "beam over the bars": the exact current design (tile + spark +
    session bars) with a soft searchlight cone sweeping diagonally across the
    bars. Smallest change; literalizes the name.

Prototype 2 - "beacon spark": the four-point spark is replaced by a compact
    searchlight beacon (lamp + short projected cone) over the bars. A bigger
    departure that reads unmistakably as a searchlight.
"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT_DIR = os.path.join(os.path.dirname(__file__), "icon_previews")
OUT_DIR = os.path.abspath(OUT_DIR)
os.makedirs(OUT_DIR, exist_ok=True)

SS = 8  # supersample factor for antialiasing

C_TL = (91, 108, 255)    # indigo / periwinkle  #5B6CFF
C_BR = (23, 195, 230)    # cyan                 #17C3E6


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def diagonal_gradient(size):
    img = Image.new("RGB", (size, size))
    px = img.load()
    maxd = (size - 1) * 2.0
    for y in range(size):
        for x in range(size):
            t = (x + y) / maxd
            px[x, y] = lerp(C_TL, C_BR, t)
    return img


def star4(cx, cy, r_tip, r_in, rot=0.0):
    pts = []
    for i in range(4):
        a_tip = rot + i * (math.pi / 2)
        pts.append((cx + r_tip * math.cos(a_tip), cy + r_tip * math.sin(a_tip)))
        a_in = a_tip + math.pi / 4
        pts.append((cx + r_in * math.cos(a_in), cy + r_in * math.sin(a_in)))
    return pts


def _spark(draw, cx, cy, r_tip, r_in, alpha=255):
    draw.polygon(star4(cx, cy, r_tip, r_in), fill=(255, 255, 255, alpha))


def _tile_base(S):
    """Rounded gradient tile + top highlight. Returns (img, mask)."""
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    pad = int(S * 0.06)
    radius = int(S * 0.235)
    box = [pad, pad, S - pad, S - pad]

    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle(box, radius=radius, fill=255)

    grad = diagonal_gradient(S).convert("RGBA")
    img.paste(grad, (0, 0), mask)

    hi = Image.new("L", (S, S), 0)
    hpx = hi.load()
    fade_end = S * 0.65
    for y in range(S):
        t = 1.0 - (y / fade_end)
        v = int(max(0.0, t) * 46) if y < fade_end else 0
        if v:
            for x in range(S):
                hpx[x, y] = v
    hi = Image.composite(hi, Image.new("L", (S, S), 0), mask)
    img.alpha_composite(Image.merge("RGBA", (
        Image.new("L", (S, S), 255), Image.new("L", (S, S), 255),
        Image.new("L", (S, S), 255), hi)))
    return img, mask


def _bars(draw, S):
    """Current lower-left descending session bars with leading node dots."""
    bar_h = S * 0.072
    bar_x = S * 0.20
    gap = S * 0.128
    widths = [0.46, 0.36, 0.26]
    y0 = S * 0.545
    for i, w in enumerate(widths):
        y = y0 + i * gap
        draw.rounded_rectangle(
            [bar_x, y, bar_x + S * w, y + bar_h],
            radius=bar_h / 2, fill=(255, 255, 255, 235))
        dot_r = bar_h * 0.62
        dcx = bar_x - S * 0.052
        dcy = y + bar_h / 2
        draw.ellipse([dcx - dot_r, dcy - dot_r, dcx + dot_r, dcy + dot_r],
                     fill=(255, 255, 255, 235))


def _clip_layer(layer, mask):
    a = ImageChops.multiply(layer.split()[3], mask)
    layer.putalpha(a)
    return layer


# ---------------------------------------------------------------------------
# CURRENT (reference render, for the contact sheet)
# ---------------------------------------------------------------------------
def render_current(size):
    S = size * SS
    img, mask = _tile_base(S)
    draw = ImageDraw.Draw(img)
    small = size <= 20
    if small:
        cx, cy = S * 0.5, S * 0.5
        _spark(draw, cx, cy, S * 0.40, S * 0.13, 255)
        _spark(draw, S * 0.78, S * 0.26, S * 0.11, S * 0.035, 235)
    else:
        _bars(draw, S)
        _spark(draw, S * 0.66, S * 0.345, S * 0.235, S * 0.072, 255)
        _spark(draw, S * 0.855, S * 0.145, S * 0.078, S * 0.024, 240)
    return img.resize((size, size), Image.LANCZOS)


# ---------------------------------------------------------------------------
# PROTOTYPE 1 - beam over the bars (current design + soft searchlight cone)
# ---------------------------------------------------------------------------
def render_proto1(size):
    S = size * SS
    img, mask = _tile_base(S)
    draw = ImageDraw.Draw(img)
    small = size <= 20

    # Soft searchlight cone emanating from the spark, sweeping down-left across
    # the bars. Built on its own layer, blurred, and clipped to the tile.
    beam = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    bd = ImageDraw.Draw(beam)
    apex = (S * 0.655, S * 0.345)
    if small:
        apex = (S * 0.5, S * 0.42)
        edge1 = (S * 0.12, S * 0.98)
        edge2 = (S * 0.92, S * 1.02)
    else:
        edge1 = (S * -0.02, S * 0.60)   # upper edge of the cone (down-left)
        edge2 = (S * 0.42, S * 1.04)    # lower edge of the cone
    bd.polygon([apex, edge1, edge2], fill=(255, 255, 255, 66))
    beam = beam.filter(ImageFilter.GaussianBlur(S * 0.018))
    # a brighter central ray for a crisp "beam" read
    ray = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    rd = ImageDraw.Draw(ray)
    mid = ((edge1[0] + edge2[0]) / 2, (edge1[1] + edge2[1]) / 2)
    rd.line([apex, mid], fill=(255, 255, 255, 90), width=max(1, int(S * 0.012)))
    ray = ray.filter(ImageFilter.GaussianBlur(S * 0.01))
    beam.alpha_composite(ray)
    _clip_layer(beam, mask)
    img.alpha_composite(beam)

    if small:
        _spark(draw, S * 0.5, S * 0.42, S * 0.34, S * 0.11, 255)
    else:
        _bars(draw, S)
        _spark(draw, S * 0.655, S * 0.345, S * 0.235, S * 0.072, 255)
        _spark(draw, S * 0.855, S * 0.145, S * 0.078, S * 0.024, 240)
    return img.resize((size, size), Image.LANCZOS)


# ---------------------------------------------------------------------------
# PROTOTYPE 2 - beacon spark (searchlight lamp + short projected cone)
# ---------------------------------------------------------------------------
def render_proto2(size):
    S = size * SS
    img, mask = _tile_base(S)
    draw = ImageDraw.Draw(img)
    small = size <= 20

    if small:
        lamp = (S * 0.5, S * 0.30)
        lamp_r = S * 0.13
        cone_edges = [(S * 0.16, S * 0.94), (S * 0.84, S * 0.94)]
    else:
        _bars(draw, S)
        lamp = (S * 0.70, S * 0.28)
        lamp_r = S * 0.088
        cone_edges = [(S * 0.06, S * 0.60), (S * 0.44, S * 0.92)]

    # Projected cone from the lamp (soft, blurred, clipped).
    beam = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    bd = ImageDraw.Draw(beam)
    bd.polygon([lamp, cone_edges[0], cone_edges[1]], fill=(255, 255, 255, 96))
    beam = beam.filter(ImageFilter.GaussianBlur(S * 0.016))
    _clip_layer(beam, mask)
    img.alpha_composite(beam)

    # The lamp head: a bright filled disc with a subtle rim, projecting the cone.
    draw.ellipse([lamp[0] - lamp_r, lamp[1] - lamp_r,
                  lamp[0] + lamp_r, lamp[1] + lamp_r],
                 fill=(255, 255, 255, 255))
    # a small inner "bulb" cut of gradient color for a lens feel
    inner = lamp_r * 0.46
    draw.ellipse([lamp[0] - inner, lamp[1] - inner,
                  lamp[0] + inner, lamp[1] + inner],
                 fill=(120, 150, 255, 235))

    if not small:
        # keep a tiny AI sparkle so the identity still nods to "agent".
        _spark(draw, S * 0.885, S * 0.135, S * 0.062, S * 0.02, 235)
    return img.resize((size, size), Image.LANCZOS)


PREVIEW_SIZES = (256, 48, 32, 16)
RENDERERS = {
    "current": render_current,
    "proto1": render_proto1,
    "proto2": render_proto2,
}

for name, fn in RENDERERS.items():
    for s in PREVIEW_SIZES:
        p = os.path.join(OUT_DIR, f"{name}_{s}.png")
        fn(s).save(p)
    print("wrote", name, "previews")

# Contact sheet: current | proto1 | proto2 at 256, on a neutral card, with the
# tray-size (32) strip beneath each for a small-size sanity check.
pad = 40
cell = 256
label_h = 34
strip = 40
cols = 3
sheet_w = cols * cell + (cols + 1) * pad
sheet_h = pad + label_h + cell + 18 + strip + pad
sheet = Image.new("RGB", (sheet_w, sheet_h), (32, 34, 40))
sd = ImageDraw.Draw(sheet)
order = ["current", "proto1", "proto2"]
titles = ["CURRENT", "PROTO 1  beam", "PROTO 2  beacon"]
for i, key in enumerate(order):
    x = pad + i * (cell + pad)
    y = pad + label_h
    sd.text((x + 4, pad + 8), titles[i], fill=(230, 232, 238))
    big = RENDERERS[key](256).convert("RGBA")
    sheet.paste(big, (x, y), big)
    # 32px strip on a light + dark swatch to check tray legibility
    small_img = RENDERERS[key](32).convert("RGBA")
    sy = y + cell + 12
    sd.rectangle([x, sy, x + 32, sy + 32], fill=(240, 240, 240))
    sheet.paste(small_img, (x, sy), small_img)
    sd.rectangle([x + 44, sy, x + 76, sy + 32], fill=(20, 20, 20))
    sheet.paste(small_img, (x + 44, sy), small_img)
sheet_path = os.path.join(OUT_DIR, "contact_sheet.png")
sheet.save(sheet_path)
print("wrote", sheet_path)
