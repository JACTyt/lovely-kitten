"""Generates Lovely Kitten art and audio.

Run from the project root:  python tools/art/generate.py
Outputs: sprites/generated/*.png, audio/generated/*.wav, entity/cat/CatAnchors.g.cs
Re-runnable and deterministic (fixed seeds).
"""
import math
import os
import random
import struct
import wave

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SPR = os.path.join(ROOT, "sprites", "generated")
AUD = os.path.join(ROOT, "audio", "generated")
os.makedirs(SPR, exist_ok=True)
os.makedirs(AUD, exist_ok=True)

OUTLINE = "#2b2433"


def rgba(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def hexs(c):
    return "#%02x%02x%02x" % c[:3]


def draw(img, ox, oy, rows, pal):
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != "." and ch != " ":
                img.putpixel((ox + x, oy + y), rgba(pal[ch]) if isinstance(pal[ch], str) else pal[ch])


def save(img, name):
    img.save(os.path.join(SPR, name))


# --------------------------------------------------------------------------- owl
OWL_PAL = {"K": OUTLINE, "B": "#6b4a3a", "W": "#e8d8b8", "Y": "#f2c84b", "E": "#130d1d",
           "O": "#e08a3a", "L": "#c9a27a", "D": "#8a6a4f"}
OWL_BODY = [
    ".K......K.",
    ".KBK..KBK.",
    ".KBBBBBBK.",
    "KBWWBBWWBK",
    "KWYEWWEYWK",
    "KBWWOOWWBK",
    "KBBLLLLBBK",
    "KBLDLLDLBK",
    ".KBLLLLBK.",
    "..KKOOKK..",
]
OWL_WINGS = {
    "up": (2, ["K..", "KK.", "BBK", ".BB", "..B"]),
    "mid": (7, ["KKK", "BBB", ".KB"]),
    "down": (9, ["..B", ".BB", ".BK", "KB.", "K.."]),
    "tuck": (8, ["..K", "..B", "..B", "..K"]),
}


def mirror(rows):
    return [r[::-1] for r in rows]


def owl_frame(img, ox, oy, wing, body_dy=0):
    draw(img, ox + 3, oy + 4 + body_dy, OWL_BODY, OWL_PAL)
    wy, rows = OWL_WINGS[wing]
    draw(img, ox + 0 + (0 if wing != "tuck" else 1), oy + wy + body_dy, rows, OWL_PAL)
    draw(img, ox + 13 - (0 if wing != "tuck" else 1), oy + wy + body_dy, mirror(rows), OWL_PAL)


def gen_owl():
    img = Image.new("RGBA", (64, 32), (0, 0, 0, 0))
    for i, w in enumerate(["up", "mid", "down", "mid"]):
        owl_frame(img, i * 16, 0, w)
    owl_frame(img, 0, 16, "tuck", 1)
    owl_frame(img, 16, 16, "tuck", 2)
    owl_frame(img, 32, 16, "down", 1)
    owl_frame(img, 48, 16, "mid", 0)
    save(img, "owl.png")


# ----------------------------------------------------------------------- firefly
def gen_firefly():
    img = Image.new("RGBA", (32, 8), (0, 0, 0, 0))
    for i, glow in enumerate([0.45, 0.75, 1.0, 0.75]):
        ox = i * 8
        g = int(255 * glow)
        halo = int(110 * glow)
        for (x, y) in [(2, 4), (4, 4), (3, 3), (3, 5)]:
            img.putpixel((ox + x, y), (230, 255, 140, halo))
        img.putpixel((ox + 3, 4), (min(255, 200 + int(55 * glow)), 255, 120, g))
        img.putpixel((ox + 4, 3), rgba("#3a3340"))
        img.putpixel((ox + 5, 2), rgba("#3a3340"))
        img.putpixel((ox + 4, 2), (220, 230, 255, 180))
        img.putpixel((ox + 5, 1), (220, 230, 255, 140))
    save(img, "firefly.png")


def outline_pass(img, col=OUTLINE):
    """Adds a 1px outline around every opaque pixel."""
    w, h = img.size
    src = img.copy().load()
    dst = img.load()
    for y in range(h):
        for x in range(w):
            if src[x, y][3] == 0 and any(0 <= x + dx < w and 0 <= y + dy < h and src[x + dx, y + dy][3] > 0
                                         for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                dst[x, y] = rgba(col)


def crow_frame(wing, dive=False):
    """32x32 Crow King facing left. wing: -1 up, 0 mid, 1 down."""
    from PIL import ImageDraw
    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    body, shade, light = rgba("#2a2638"), rgba("#1a1724"), rgba("#4a4466")
    tilt = 4 if dive else 0
    # tail
    d.polygon([(22, 17 + tilt // 2), (30, 13 + tilt), (30, 21 + tilt), (23, 21 + tilt // 2)], fill=shade)
    # body + head
    d.ellipse((9, 12 + tilt // 2, 25, 24 + tilt // 2), fill=body)
    d.ellipse((5, 8, 14, 17), fill=body)
    d.ellipse((12, 15 + tilt // 2, 21, 22 + tilt // 2), fill=light)
    # beak, eye
    d.polygon([(5, 12), (0, 14), (5, 15)], fill=rgba("#e0a63a"))
    img.putpixel((8, 11), rgba("#e04848"))
    img.putpixel((8, 12), rgba("#ff8a8a"))
    # crown
    for x, top in ((6, 4), (9, 3), (12, 4)):
        d.line((x, top, x, 7), fill=rgba("#f2c84b"))
    d.line((6, 7, 12, 7), fill=rgba("#f2c84b"))
    img.putpixel((9, 6), rgba("#4d9be6"))
    # wing
    if dive:
        d.polygon([(12, 14), (24, 12), (28, 16), (16, 19)], fill=shade)
    elif wing < 0:
        d.polygon([(13, 15), (16, 1), (22, 3), (21, 15)], fill=shade)
        d.line((16, 2, 21, 4), fill=light)
    elif wing == 0:
        d.polygon([(12, 15), (27, 9), (29, 13), (20, 18)], fill=shade)
        d.line((27, 10, 29, 13), fill=light)
    else:
        d.polygon([(13, 16), (21, 16), (19, 30), (14, 29)], fill=shade)
        d.line((14, 28, 19, 29), fill=light)
    # feet
    d.line((15, 24 + tilt // 2, 15, 26 + tilt // 2), fill=rgba("#e0a63a"))
    d.line((19, 24 + tilt // 2, 19, 26 + tilt // 2), fill=rgba("#e0a63a"))
    outline_pass(img)
    return img


def gen_crow_king():
    sheet = Image.new("RGBA", (32 * 4, 32 * 2), (0, 0, 0, 0))
    for i, w in enumerate((-1, 0, 1, 0)):
        sheet.alpha_composite(crow_frame(w), (i * 32, 0))
    sheet.alpha_composite(crow_frame(0, dive=True), (0, 32))
    sheet.alpha_composite(crow_frame(1, dive=True), (32, 32))
    save(sheet, "crow_king.png")
    # dark feather projectile
    f = Image.new("RGBA", (6, 6), (0, 0, 0, 0))
    draw(f, 0, 0, ["....K.", "...KB.", "..KBK.", ".KBK..", "KBK...", "K....."], {"K": OUTLINE, "B": "#4a4466"})
    save(f, "dark_feather.png")


def gen_yarn():
    img = Image.new("RGBA", (8, 8), (0, 0, 0, 0))
    draw(img, 0, 0, ["..KKKK..", ".KPPQPK.", "KPQPPQPK", "KPPQPPQK", "KQPPQPPK", "KPQPPQPK", ".KPPQPK.", "..KKKK.."],
         {"K": OUTLINE, "P": "#e46fa0", "Q": "#a8406f"})
    save(img, "yarn.png")


def gen_feather():
    img = Image.new("RGBA", (4, 4), (0, 0, 0, 0))
    draw(img, 0, 0, ["..W.", ".WL.", ".WL.", "L..."], {"W": "#ffffff", "L": "#d8d0c8"})
    save(img, "feather.png")


# ----------------------------------------------------------------------- clothes
CLOTHES_PAL = {
    "R": "#d9475b", "W": "#f0e6dc", "K": "#26222e", "Y": "#f2c84b", "G": "#c9952b",
    "B": "#4d9be6", "P": "#e46fa0", "Q": "#a8406f", "S": "#4d9be6", "N": "#7a4b32",
    "L": "#ffe08a", "C": "#9fd8ff", "D": "#3a3a52", "E": "#e04848",
}
# (cell column, rows, top-left y) — hats anchor bottom-centre (8,12), neck (8,8), glasses lens (10,8)
HATS = [
    (["...W...", "..RRR..", ".RRRRR.", "RRRRRRR", "WWWWWWW"], 8),
    ([".KKKKK.", ".KKKKK.", ".KKKKK.", ".RRRRR.", ".KKKKK.", "KKKKKKK"], 7),
    (["Y.Y.Y.Y", "YYYYYYY", "YEYCYEY", "GGGGGGG"], 9),
]
NECK = [
    (["P...P", "PPQPP", "P...P"], 7, 6),
    (["SSSSSSS", "SWSSWSS", ".....SS", ".....S."], 7, 5),
    (["NNNNNNN", "...K...", "..KLK..", "..LLL..", "..KLK.."], 7, 5),
]
GLASSES = [
    (["..KKK", "KKKCK", "..KKK"], 7, 7),
    (["KKKKK", "..DDK", "..DDK"], 7, 7),
]


def gen_clothes():
    img = Image.new("RGBA", (48, 48), (0, 0, 0, 0))
    for i, (rows, top) in enumerate(HATS):
        draw(img, i * 16 + 5, top, rows, CLOTHES_PAL)
    for i, (rows, top, left) in enumerate(NECK):
        draw(img, i * 16 + left, 16 + top, rows, CLOTHES_PAL)
    for i, (rows, top, left) in enumerate(GLASSES):
        draw(img, i * 16 + left + 1, 32 + top, rows, CLOTHES_PAL)
    save(img, "clothes.png")


# ------------------------------------------------------------------ cat anchors
def gen_cat_anchors():
    sheet = Image.open(os.path.join(ROOT, "sprites", "Cat Sprite Sheet.png")).convert("RGBA")
    anchors = []
    for r in range(10):
        for c in range(8):
            f = sheet.crop((c * 32, r * 32, c * 32 + 32, r * 32 + 32))
            pts = [(x, y) for y in range(32) for x in range(32) if f.getpixel((x, y))[3] > 0]
            if not pts:
                anchors.append((-1, -1))
                continue
            right = max(x for x, _ in pts)
            region = [(x, y) for x, y in pts if x >= right - 7]
            hy = min(y for _, y in region)
            top = [x for x, y in region if y <= hy + 1]
            anchors.append((round(sum(top) / len(top)), hy))
    body = ",\n\t\t".join(", ".join("new(%d, %d)" % a for a in anchors[i:i + 8]) for i in range(0, 80, 8))
    src = (
        "// <auto-generated> by tools/art/generate.py — do not edit by hand.\n"
        "using Godot;\n\n"
        "public static class CatAnchors\n{\n"
        "\t// Head-top pixel (x, y) inside each 32x32 cat frame; (-1, -1) for empty frames.\n"
        "\tpublic static readonly Vector2I[] Head =\n\t{\n\t\t" + body + "\n\t};\n}\n"
    )
    with open(os.path.join(ROOT, "entity", "cat", "CatAnchors.g.cs"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(src)


# ------------------------------------------------------------------ backgrounds
DAY_SKY = ["#26aaff"] * 3 + ["#4bb8ff"] * 2 + ["#71c7ff"] * 2 + ["#8fd3ff"] * 2 + ["#9dd8ff"] * 30
NIGHT_SKY = ["#0b1026"] * 3 + ["#121a3a"] * 3 + ["#18224a"] * 3 + ["#1f2b58"] * 3 + ["#283566"] * 30
SKY_SET = {"#26aaff", "#4bb8ff", "#71c7ff", "#8fd3ff", "#9dd8ff"}
NAVY = (28, 34, 70)


def darken(c):
    return (int(c[0] * 0.42 + NAVY[0] * 0.4), int(c[1] * 0.42 + NAVY[1] * 0.4),
            int(c[2] * 0.45 + NAVY[2] * 0.45), c[3])


def nightify(day, is_sky, lit, glints=(), seed=7):
    """Turns a day background into night: sky → gradient + stars + moon, rest darkened, `lit` colours glow."""
    rnd = random.Random(seed)
    w, h = day.size
    out = Image.new("RGBA", day.size)
    src = day.load()
    dst = out.load()
    for y in range(h):
        for x in range(w):
            c = src[x, y]
            hx = hexs(c)
            if is_sky(x, y, c):
                dst[x, y] = rgba(NIGHT_SKY[min(y, len(NIGHT_SKY) - 1)])
            elif hx in lit:
                dst[x, y] = rgba(lit[hx])
            elif hx in glints and any(hexs(src[nx, ny]) in lit for nx, ny in
                                      [(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)]
                                      if 0 <= nx < w and 0 <= ny < h):
                dst[x, y] = rgba("#fff3c4")
            else:
                dst[x, y] = darken(c)
    for _ in range(40):
        x, y = rnd.randrange(w), rnd.randrange(30)
        if is_sky(x, y, src[x, y]):
            dst[x, y] = rgba(rnd.choice(["#ffffff", "#fff3c4", "#c9d6ff"]), rnd.choice([160, 220, 255]))
    moon(out, 58, 8)
    return out


def moon(img, ox, oy):
    rows = [".MMMM.", "MMMMMM", "MMNMMM", "MMMMNM", "MMMMMM", ".MMMM."]
    draw(img, ox, oy, rows, {"M": "#f4f1d0", "N": "#d8d3a8"})


def sky_gradient(img, pal, until):
    for y in range(until):
        for x in range(img.width):
            img.putpixel((x, y), rgba(pal[min(y, len(pal) - 1)]))


def rect(img, x0, y0, x1, y1, col):
    c = rgba(col) if isinstance(col, str) else col
    for y in range(max(0, y0), min(img.height, y1)):
        for x in range(max(0, x0), min(img.width, x1)):
            img.putpixel((x, y), c)


def gen_rooftops():
    rnd = random.Random(21)
    img = Image.new("RGBA", (128, 72))
    sky_gradient(img, DAY_SKY, 72)
    # distant skyline
    x = 0
    while x < 128:
        bw, bh = rnd.randint(8, 16), rnd.randint(10, 26)
        rect(img, x, 40 - bh, x + bw, 44, "#a9c4dc")
        for wy in range(40 - bh + 3, 42, 4):
            for wx in range(x + 2, x + bw - 2, 3):
                if rnd.random() < 0.5:
                    img.putpixel((wx, wy), rgba("#8fb0cc"))
        x += bw + rnd.randint(0, 3)
    # chimneys & antennas
    for cx in (14, 52, 97):
        rect(img, cx, 34, cx + 5, 44, "#9e4539")
        rect(img, cx - 1, 33, cx + 6, 35, "#7a3229")
    for ax in (32, 78, 118):
        rect(img, ax, 30, ax + 1, 44, "#433f46")
        rect(img, ax - 3, 32, ax + 4, 33, "#433f46")
    rect(img, 64, 26, 74, 38, "#8d6a4f")   # water tower
    rect(img, 63, 24, 75, 27, "#5e4636")
    rect(img, 65, 38, 66, 44, "#5e4636")
    rect(img, 72, 38, 73, 44, "#5e4636")
    # roof surface
    rect(img, 0, 44, 128, 72, "#b95017")
    for y in range(46, 72, 4):
        for x in range((y // 4 % 2) * 4, 128, 8):
            rect(img, x, y, x + 7, y + 1, "#9e4616")
    rect(img, 0, 44, 128, 45, "#e5e5e5")
    save(img, "bg_rooftops_day.png")
    night = nightify(img, lambda x, y, c: y < 44 and hexs(c) in set(DAY_SKY),
                     {"#8fb0cc": "#ffcf6b"}, seed=22)
    save(night, "bg_rooftops_night.png")


def tree(img, cx, base, r, leaf, dark):
    rect(img, cx - 1, base - r, cx + 2, base, "#6b4a3a")
    for y in range(-r, r + 1):
        for x in range(-r, r + 1):
            if x * x + y * y <= r * r:
                col = dark if (x + y) > r // 2 else leaf
                px, py = cx + x, base - r - r // 2 + y
                if 0 <= px < img.width and 0 <= py < img.height:
                    img.putpixel((px, py), rgba(col))


def gen_park():
    rnd = random.Random(33)
    img = Image.new("RGBA", (128, 72))
    sky_gradient(img, DAY_SKY, 72)
    for x in range(128):  # hills
        h = int(34 + 4 * math.sin(x / 13.0) + 2 * math.sin(x / 5.0))
        rect(img, x, h, x + 1, 44, "#7fc46a")
    for cx in (10, 40, 70, 104):
        tree(img, cx, 44, rnd.randint(7, 9), "#3f9a4a", "#2f7a3c")
    for lx in (25, 88):  # lamp posts
        rect(img, lx, 30, lx + 1, 44, "#433f46")
        rect(img, lx - 1, 28, lx + 2, 30, "#f6e7a8")
    rect(img, 0, 44, 128, 72, "#5fb04f")
    rect(img, 0, 46, 128, 53, "#e0c7af")
    rect(img, 0, 46, 128, 47, "#bdac9b")
    for _ in range(60):
        x, y = rnd.randrange(128), rnd.randrange(54, 72)
        img.putpixel((x, y), rgba(rnd.choice(["#4a9a3f", "#7fc46a", "#ffffff", "#f2c84b", "#de7586"])))
    save(img, "bg_park_day.png")
    night = nightify(img, lambda x, y, c: y < 44 and hexs(c) in set(DAY_SKY), {"#f6e7a8": "#ffe9a0"}, seed=34)
    save(night, "bg_park_night.png")


def gen_street():
    day = Image.open(os.path.join(ROOT, "background.png")).convert("RGBA")
    save(day, "bg_street_day.png")
    night = nightify(day, lambda x, y, c: y < 14 and hexs(c) in SKY_SET,
                     {"#396a9a": "#ffcf6b"}, glints={"#ffffff"}, seed=8)
    save(night, "bg_street_night.png")


# --------------------------------------------------------------------------- UI
UI_PAL = {"K": OUTLINE, "Y": "#f2c84b", "G": "#c9952b", "W": "#fff3c4", "C": "#f4ead8", "S": "#d9c7a8",
          "B": "#8fd3ff", "R": "#d9475b", "P": "#e46fa0", "O": "#e08a3a", "M": "#f4f1d0", "N": "#c9c39a",
          "D": "#4d9be6", "L": "#7fc46a", "E": "#5e4636", "X": "#ffffff", "Z": "#b5b5b5", "Q": "#a8406f", "V": "#4a4466"}

ICONS = {
    "coin": ["..KKKK..", ".KYYYYK.", "KYWYYGYK", "KYWYYGYK", "KYYYYGYK", "KYYYGGYK", ".KGGGGK.", "..KKKK.."],
    "sun": ["....Y.....Y.", ".Y........Y.", "....YYYY....", "...YWYYYY...", "..YWYYYYYY..", "Y.YYYYYYYY.Y",
            "..YYYYYYYY..", "..YYYYYYGY..", "...YYYYGY...", "....YYYY....", ".Y........Y.", "....Y.....Y."],
    "moon": ["...KKKKK....", "..KMMMMK....", ".KMMMMK.....", "KMMMMK......", "KMMMNK......", "KMMMMK......",
             "KMMMMK......", "KMMMNMK.....", "KMMMMMMK..K.", ".KMMMMMMKKMK", "..KMMMMMMMK.", "...KKKKKKK.."],
    "star": [".....KK.....", ".....KYK....", "....KYYK....", "KKKKKYWYKKKK", "KYYYYWYYYYGK", ".KYYYYYYYGK.",
             "..KYYYYYGK..", "..KYYYYYGK..", ".KYYYKKYYGK.", ".KYGK..KYGK.", "KYGK....KGK.", "KKK......KK."],
    "catnip": ["............", ".....KK.....", "....KLLK....", "...KLLLLK...", "..KLLKLLLK..", "..KLLLKLLK..",
               "...KLLLLK...", "....KLLK....", ".....KEK....", ".....KEK....", ".....KEK....", "......K....."],
    "bed": ["............", "............", "KK..........", "KCKKKKKKKKKK", "KCKWWWKPPPPK", "KCKWWWKPPPPK",
            "KCKKKKKKKKKK", "KCCCCCCCCCCK", "KSSSSSSSSSSK", "KKKKKKKKKKKK", "K.........K.", "............"],
    "dash": ["............", "............", "......KKK...", "..KK.KDDDK..", ".....KDDDDK.", "KKKK.KDDDDDK",
             ".....KDDDDK.", "..KK.KDDDK..", "......KKK...", "............", "............", "............"],
    "dive": ["....KDDK....", "....KDDK....", "....KDDK....", "....KDDK....", ".KKKKDDKKKK.", "..KDDDDDDK..",
             "...KDDDDK...", "....KDDK....", ".....KK.....", "............", ".O..O..O..O.", "OOOOOOOOOOOO"],
    "meow": ["............", ".K.......K..", ".KK.....KK..", ".KCKKKKKCK..", ".KCCCCCCCK..", "KCCECCCECCK.",
             "KCCCCPCCCCK.", "KCCCKKKCCCK.", ".KCCKRKCCK..", "..KKCCCKK...", "....KKK.....", "............"],
    "swipe": ["..........KK", "........KKDK", "......KKDDK.", ".....KDDDK..", "....KDDK....", "...KDDK.....",
              "..KDDK......", "..KDK.......", ".KDK..KK....", ".KK..KCCK...", ".....KCCK...", "......KK...."],
    "yarn": ["....KKKK....", "..KKPPPPKK..", ".KPPQQPPPPK.", ".KPQPPQQPPK.", "KPPPQPPPQPPK", "KPQQPPQPPQPK",
             "KPPPPQPPQPPK", "KPPQPPQQPPPK", ".KPPQPPPPPK.", ".KPPPQQPPPK.", "..KKPPPPKKQQ", "....KKKK...Q"],
    "cattime": ["KKKKKKKKKK..", ".KWWWWWWK...", ".KYYYYYYK...", "..KYYYYK....", "...KYYK.....", "....KK......",
                "...KWYK.....", "..KWWYYK....", ".KWWYYYYK...", ".KYYYYYYK...", "KKKKKKKKKK..", "............"],
    "glide": ["............", "KK........KK", "KWKK....KKWK", ".KWWKKKKWWK.", ".KWWWDDWWWK.", "..KWWDDWWK..",
              "...KKDDKK...", ".....KK.....", "............", "............", "............", "............"],
    "boss": ["....Y.Y.Y...", "....YYYYY...", "...KKKKKK...", "..KVVVVVVK..", ".KVRVVVVVVK.", "OOKVVVVVVVK.",
             ".KVVVVVVVK..", "..KVVWWVVK..", "...KVVVVK...", "....KKKK....", "...O....O...", "............"],
    "trade": ["..KKKK......", ".KYYYYK.....", "KYYWYYK..K..", "KYYYYYK.KYK.", ".KYYYYK.KYYK", "..KKKK..KYYK",
              "......KKKYK.", "..K..KYYYK..", ".KYKKYYYYK..", "KYYYYYYYK...", ".KYYYYYK....", "..KKKKK....."],
    "rebirth": ["....KKKK....", "..KKDDDDKK..", ".KDDKKKKDDK.", ".KDK....KDK.", "KDK......KK.", "KDK..K......",
                "KDK.KDK.....", ".KDKDDDK....", ".KDDDDK.....", "..KDDDK.....", "...KDK......", "....K......."],
    "lives": ["............", ".KKK...KKK..", "KRRRK.KRRRK.", "KRXRRKRRRRK.", "KRRRRRRRRRK.", "KRRRRRRRRRK.",
              ".KRRRRRRRK..", "..KRRRRRK...", "...KRRRK....", "....KRK.....", ".....K......", "............"],
    "lucky": ["..KKKKKK....", ".KYYYYYYK...", "KYYLLYLLYK..", "KYYLLYLLYK..", "KYYYYEYYYK..", "KYYLLYLLYK..",
              "KYYLLYLLYK..", ".KYYYYYGK...", "..KKKKKK....", "............", "............", "............"],
    "jump": [".....KK.....", "....KDDK....", "...KDDDDK...", "..KDDDDDDK..", ".KKKKDDKKKK.", "....KDDK....",
             "....KDDK....", "....KDDK....", "....KDDK....", "....KDDK....", "....KKKK....", "............"],
    "speed": ["............", "..KKKKK.....", "...KDDDK....", "....KDDDK...", ".....KDDDK..", "......KDDDK.",
              ".....KDDDK..", "....KDDDK...", "...KDDDK....", "..KKKKK.....", "............", "............"],
    "radius": ["....KKKK....", "..KK....KK..", ".K..DDDD..K.", ".K.D....D.K.", "K.D..RR..D.K", "K.D.RRRR.D.K",
               "K.D.RRRR.D.K", "K.D..RR..D.K", ".K.D....D.K.", ".K..DDDD..K.", "..KK....KK..", "....KKKK...."],
    "spawn": ["............", ".EEE........", "EOOEE.......", ".EEEEEE.....", "..EEEEEE....", "...EE.E.....",
              "......EEE...", ".....EOOEE..", "......EEEEEE", ".......EEEEE", "........EE.E", "............"],
    "golden": ["............", ".YYY........", "YOOYY.......", ".YYYYYY.....", "..YYYYGY....", "...YGGY.....",
               "....GG..W...", "........WW..", ".......WXW..", "........WW..", "............", "............"],
    "double": [".....KK.....", "....KDDK....", "...KDDDDK...", "..KKKDDKKK..", ".....KDDK...", "....KDDK....",
               "...KDDDDK...", "..KDDDDDDK..", ".KKKKDDKKKK.", "....KDDK....", "....KKKK....", "............"],
    "magnet": ["............", ".KKK...KKK..", ".KRK...KZK..", ".KRK...KZK..", ".KRK...KZK..", ".KRRK.KZZK..",
               "..KRRKZZK...", "...KRRZK....", "....KKK.....", "............", "............", "............"],
    "lantern": [".....K......", "....KKK.....", "...KYYYK....", "..KYWWYYK...", "..KYWYYYK...", "..KYYYYYK...",
                "..KYYYYGK...", "...KGGGK....", "....KKK.....", "............", "............", "............"],
    "shirt": ["............", "..KK...KK...", ".KPPK.KPPK..", "KPPPPKPPPPK.", "KKPPPPPPPKK.", "..KPPPPPK...",
              "..KPPPPPK...", "..KPPPPPK...", "..KPPPPPK...", "..KKKKKKK...", "............", "............"],
    "world": ["....KKKK....", "..KKDDLLKK..", ".KDDDLLLDDK.", ".KDDLLLDDDK.", "KDDDDLLDDDDK", "KLLDDDDDDLLK",
              "KLLLDDDDLLLK", "KDLLDDDLLDDK", ".KDDDDDLLDK.", ".KDDDDLLDDK.", "..KKDDDDKK..", "....KKKK...."],
    "bird": ["............", "..EEE.......", ".EOXEE......", "YYEEEEE.....", "..EEEEEEE...", "...EEEEEEE..",
             "....EEEEEE..", "......EE.EE.", "............", "............", "............", "............"],
}


def gen_ui():
    for name, rows in ICONS.items():
        img = Image.new("RGBA", (len(rows[0]), len(rows)), (0, 0, 0, 0))
        draw(img, 0, 0, rows, UI_PAL)
        save(img, ("ui_%s.png" if name in ("coin", "sun", "moon", "star") else "ui_icon_%s.png") % name)
    # nine-patch panel 24x24 (6px margins)
    p = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    rect(p, 1, 0, 23, 24, OUTLINE)
    rect(p, 0, 1, 24, 23, OUTLINE)
    rect(p, 1, 1, 23, 23, "#f4ead8")
    rect(p, 1, 20, 23, 23, "#d9c7a8")
    rect(p, 2, 2, 22, 3, "#fffaf0")
    save(p, "ui_panel.png")
    for name, face, shade, hi in [("button", "#8fd3ff", "#4d9be6", "#d6f0ff"),
                                  ("button_hover", "#b8e4ff", "#71c7ff", "#ffffff"),
                                  ("button_pressed", "#4d9be6", "#356da3", "#8fd3ff"),
                                  ("button_disabled", "#c9c4bd", "#9a948d", "#e5e1db")]:
        b = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        rect(b, 1, 0, 15, 16, OUTLINE)
        rect(b, 0, 1, 16, 15, OUTLINE)
        rect(b, 1, 1, 15, 15, face)
        rect(b, 1, 12, 15, 15, shade)
        rect(b, 2, 2, 14, 3, hi)
        save(b, "ui_%s.png" % name)


# ------------------------------------------------------------------------ audio
RATE = 22050


def write_wav(name, samples):
    with wave.open(os.path.join(AUD, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32000)) for s in samples))


def osc(kind, phase, duty=0.5):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < duty else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    return math.sin(2 * math.pi * p)


def tone(f0, f1, dur, kind="square", vol=0.35, attack=0.005, release=0.04, duty=0.5, vib=0.0):
    n = int(dur * RATE)
    out, ph = [], 0.0
    for i in range(n):
        t = i / RATE
        f = f0 + (f1 - f0) * (i / max(1, n - 1))
        if vib:
            f *= 1 + vib * math.sin(2 * math.pi * 6 * t)
        ph += f / RATE
        env = min(1.0, t / attack) * min(1.0, (dur - t) / release)
        out.append(osc(kind, ph, duty) * vol * max(0.0, env))
    return out


def noise(dur, vol=0.3, seed=1, decay=30.0):
    rnd = random.Random(seed)
    out, last = [], 0.0
    for i in range(int(dur * RATE)):
        last = last * 0.6 + rnd.uniform(-1, 1) * 0.4
        out.append(last * vol * math.exp(-decay * i / RATE))
    return out


def seq(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def silence(dur):
    return [0.0] * int(dur * RATE)


def gen_sfx():
    write_wav("jump.wav", tone(300, 720, 0.12, "square", 0.25, duty=0.25))
    write_wav("land.wav", noise(0.07, 0.5, seed=3, decay=50))
    write_wav("catch.wav", seq(tone(880, 880, 0.05, "square", 0.25, duty=0.25),
                               tone(1320, 1320, 0.09, "square", 0.25, duty=0.25)))
    write_wav("coin.wav", seq(tone(988, 988, 0.05, "square", 0.22), tone(1319, 1319, 0.16, "square", 0.22)))
    write_wav("combo.wav", seq(*[tone(f, f, 0.045, "square", 0.22, duty=0.125) for f in (660, 880, 1100, 1320)]))
    write_wav("buy.wav", seq(*[tone(f, f, 0.07, "tri", 0.45) for f in (523, 659, 784, 1046)]))
    write_wav("deny.wav", seq(tone(220, 180, 0.1, "square", 0.25), silence(0.04), tone(200, 150, 0.14, "square", 0.25)))
    write_wav("click.wav", tone(1500, 1200, 0.025, "square", 0.18, release=0.01))
    write_wav("meow.wav", seq(tone(520, 820, 0.12, "saw", 0.22, attack=0.02, vib=0.03),
                              tone(820, 430, 0.3, "saw", 0.22, release=0.12, vib=0.04)))
    write_wav("swipe.wav", [s * (1 - i / 3000) for i, s in enumerate(noise(0.14, 0.45, seed=5, decay=8))])
    write_wav("dash.wav", seq(noise(0.05, 0.3, seed=6, decay=10), tone(400, 900, 0.1, "square", 0.15, duty=0.125)))
    write_wav("dive.wav", tone(900, 200, 0.18, "square", 0.2, duty=0.25))
    write_wav("boom.wav", [a + b for a, b in zip(noise(0.3, 0.6, seed=7, decay=12),
                                                 tone(90, 40, 0.3, "sine", 0.6, release=0.2))])
    write_wav("throw.wav", tone(300, 650, 0.08, "tri", 0.45))
    write_wav("timeslow.wav", tone(900, 120, 0.6, "tri", 0.4, release=0.2, vib=0.05))
    write_wav("ready.wav", tone(1760, 1760, 0.05, "sine", 0.25, release=0.04))
    write_wav("event.wav", seq(*[tone(f, f, 0.08, "square", 0.2, duty=0.25) for f in (523, 659, 784)],
                               tone(1046, 1046, 0.22, "square", 0.2, duty=0.25)))
    write_wav("hoot.wav", seq(tone(390, 360, 0.22, "sine", 0.5, attack=0.03, release=0.08, vib=0.02),
                              silence(0.08),
                              tone(370, 330, 0.32, "sine", 0.5, attack=0.03, release=0.12, vib=0.02)))


NOTE = {n: i for i, n in enumerate(["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"])}


def freq(name, octave):
    return 440.0 * 2 ** ((NOTE[name] + 12 * (octave - 4) - 9) / 12)


def mix_into(buf, start, samples):
    for i, s in enumerate(samples):
        j = start + i
        if j < len(buf):
            buf[j] += s


def song(bpm, chords, scale, lead_kind, bass_kind, seed, lead_vol, bass_vol, density, echo):
    rnd = random.Random(seed)
    beat = 60.0 / bpm
    bars = len(chords)
    total = int(bars * 4 * beat * RATE)
    buf = [0.0] * total
    for b, (root, quality) in enumerate(chords):
        bar_start = b * 4 * beat
        for q in range(4):  # bass: root, alternating octaves
            f = freq(root, 2 if q % 2 == 0 else 3)
            mix_into(buf, int((bar_start + q * beat) * RATE),
                     tone(f, f, beat * 0.9, bass_kind, bass_vol, release=0.05))
        for e in range(8):  # lead in eighths
            if rnd.random() > density:
                continue
            n = rnd.choice(scale)
            oct_ = 5 if rnd.random() < 0.7 else 4
            mix_into(buf, int((bar_start + e * beat / 2) * RATE),
                     tone(freq(n, oct_), freq(n, oct_), beat / 2 * 0.85, lead_kind, lead_vol, duty=0.25,
                          release=0.06))
    if echo:
        d = int(beat * 0.75 * RATE)
        for i in range(d, total):
            buf[i] += buf[i - d] * 0.32
        for i in range(d):  # wrap echo tail for seamless loop
            buf[i] += buf[total - d + i] * 0.32 * 0.5
    peak = max(1e-6, max(abs(s) for s in buf))
    return [s / peak * 0.7 for s in buf]


def gen_music():
    day = song(120, [("C", "M"), ("G", "M"), ("A", "m"), ("F", "M")] * 2,
               ["C", "D", "E", "G", "A"], "square", "tri", 5, 0.16, 0.35, 0.7, False)
    write_wav("music_day.wav", day)
    night = song(84, [("A", "m"), ("F", "M"), ("C", "M"), ("E", "m")] * 2,
                 ["A", "C", "D", "E", "G"], "tri", "sine", 9, 0.28, 0.32, 0.4, True)
    write_wav("music_night.wav", night)


if __name__ == "__main__":
    gen_owl()
    gen_firefly()
    gen_feather()
    gen_yarn()
    gen_crow_king()
    gen_clothes()
    gen_cat_anchors()
    gen_street()
    gen_rooftops()
    gen_park()
    gen_ui()
    gen_sfx()
    gen_music()
    print("ok")
