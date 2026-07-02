"""
Preview-only: current icon with two tweaks the user requested:
  1. remove the top-right companion spark (visual artifact),
  2. shift the session-bar group up a few px so the bottom node dot no longer
     overhangs the bottom-left rounded corner.
Writes only to icon_previews\\ (currentv2_*.png + contact_v2.png). Never touches
the live app.ico.
"""
import math
import os
from PIL import Image, ImageDraw

OUT_DIR = os.path.join(os.path.dirname(__file__), "icon_previews")
OUT_DIR = os.path.abspath(OUT_DIR)

SS = 8
C_TL = (91, 108, 255)
C_BR = (23, 195, 230)

# how far up to shift the bar group (fraction of tile). ~0.04 == a few px @ tray.
BAR_DY = 0.04


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def diagonal_gradient(size):
    img = Image.new("RGB", (size, size))
    px = img.load()
    maxd = (size - 1) * 2.0
    for y in range(size):
        for x in range(size):
            px[x, y] = lerp(C_TL, C_BR, (x + y) / maxd)
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
    return img


def _bars(draw, S, dy):
    bar_h = S * 0.072
    bar_x = S * 0.20
    gap = S * 0.128
    widths = [0.46, 0.36, 0.26]
    y0 = S * (0.545 - dy)
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


def render_v2(size):
    S = size * SS
    img = _tile_base(S)
    draw = ImageDraw.Draw(img)
    if size <= 20:
        # single centered spark, no companion.
        _spark(draw, S * 0.5, S * 0.5, S * 0.40, S * 0.13, 255)
    else:
        _bars(draw, S, BAR_DY)
        _spark(draw, S * 0.66, S * 0.345, S * 0.235, S * 0.072, 255)
        # companion spark removed per request.
    return img.resize((size, size), Image.LANCZOS)


for s in (256, 48, 32, 16):
    render_v2(s).save(os.path.join(OUT_DIR, f"currentv2_{s}.png"))
print("wrote currentv2 previews")

# Before/after contact sheet: reuse existing current_*.png (before) vs v2 (after).
pad, cell, label_h = 40, 256, 34
cols = 2
sheet_w = cols * cell + (cols + 1) * pad
sheet_h = pad + label_h + cell + 18 + 40 + pad
sheet = Image.new("RGB", (sheet_w, sheet_h), (32, 34, 40))
sd = ImageDraw.Draw(sheet)
pairs = [("current_256.png", "BEFORE  (current)", "current_32.png"),
         ("currentv2_256.png", "AFTER  (no star, bars up)", "currentv2_32.png")]
for i, (big_name, title, small_name) in enumerate(pairs):
    x = pad + i * (cell + pad)
    y = pad + label_h
    sd.text((x + 4, pad + 8), title, fill=(230, 232, 238))
    big = Image.open(os.path.join(OUT_DIR, big_name)).convert("RGBA")
    sheet.paste(big, (x, y), big)
    small_img = Image.open(os.path.join(OUT_DIR, small_name)).convert("RGBA")
    sy = y + cell + 12
    sd.rectangle([x, sy, x + 32, sy + 32], fill=(240, 240, 240))
    sheet.paste(small_img, (x, sy), small_img)
    sd.rectangle([x + 44, sy, x + 76, sy + 32], fill=(20, 20, 20))
    sheet.paste(small_img, (x + 44, sy), small_img)
sheet.save(os.path.join(OUT_DIR, "contact_v2.png"))
print("wrote contact_v2.png")
