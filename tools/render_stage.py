#!/usr/bin/env python3
"""Render an escape stage to PNG, or a frame dump to an animated GIF.

    python3 tools/render_stage.py levels/stages/01-lights-out.lvl --out build/01.png
    dotnet run --project dotnet/LowObservable.Sim -- stage --stage 01-lights-out --policy sneaker --dump build/frames
    python3 tools/render_stage.py levels/stages/01-lights-out.lvl --frames build/frames/01-lights-out_sneaker.jsonl --out build/01.gif

The GIF is the replay seen from above: the true player (white), WARDEN's belief in red (a point, a heat
map, an ellipse or particles, depending on the tracker), units (orange), cameras and their cones, roombas,
mirrors, jammers and sound rings. Needs Pillow.
"""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

CELL = 18
BUILTIN = set("#. ,SXHhoC")
COLORS = {
    "wall": (34, 36, 44),
    "floor": (92, 98, 112),
    "dark": (52, 56, 70),
    "exit": (80, 200, 120),
    "hide": (120, 110, 70),
    "throw": (190, 190, 150),
    "check": (90, 140, 200),
    "player": (245, 245, 245),
    "belief": (230, 40, 50),
    "unit": (255, 150, 40),
    "camera": (200, 80, 220),
    "roomba": (120, 220, 230),
    "note": (240, 220, 120),
    "door": (150, 100, 60),
    "terminal": (80, 160, 255),
    "key": (255, 230, 0),
    "pickup": (0, 255, 170),
    "mic": (100, 255, 100),
    "laser": (255, 60, 60),
    "switch": (255, 255, 255),
}


def parse_stage(path: Path):
    section = "header"
    header, rows, entities = {}, [], []
    for raw in path.read_text().splitlines():
        line = raw.rstrip()
        s = line.strip()
        if not s or s.startswith(";"):
            continue
        low = s.lower()
        if low in ("map:", "lights:", "entities:"):
            section = low[:-1]
            continue
        if section == "header":
            k, _, v = s.partition(":")
            header[k.strip()] = v.strip()
        elif section == "map":
            rows.append(line)
        elif section == "entities":
            parts = s.split()
            entities.append((parts[0], parts[1], " ".join(parts[2:])))
    width = max(len(r) for r in rows)
    rows = [r.ljust(width, "#") for r in rows]
    return header, rows, entities


def to_px(x: float, y: float, height: int):
    return x * CELL, (height - y) * CELL


def draw_map(draw: ImageDraw.ImageDraw, rows, entities, dark_cells=None, open_doors=None):
    h = len(rows)
    kinds = {m: t for t, m, _ in entities}
    attrs = {m: a for _, m, a in entities}
    for r, row in enumerate(rows):
        j = h - 1 - r
        for i, ch in enumerate(row):
            x0, y0 = i * CELL, r * CELL
            box = [x0, y0, x0 + CELL - 1, y0 + CELL - 1]
            if ch in "# ":
                color = COLORS["wall"]
            elif dark_cells is not None:
                color = COLORS["dark"] if (i, j) in dark_cells else COLORS["floor"]
            else:
                color = COLORS["dark"] if ch in ",h" or "dark=1" in attrs.get(ch, "") else COLORS["floor"]
            kind = kinds.get(ch)
            if kind == "door":
                color = COLORS["floor"] if open_doors and (i, j) in open_doors else COLORS["door"]
            draw.rectangle(box, fill=color)
            c = (x0 + CELL / 2, y0 + CELL / 2)
            def dot(col, rad=4, shape="ellipse"):
                b = [c[0] - rad, c[1] - rad, c[0] + rad, c[1] + rad]
                (draw.ellipse if shape == "ellipse" else draw.rectangle)(b, fill=col)
            if ch == "X":
                dot(COLORS["exit"], 7, "rect")
            elif ch in "Hh":
                dot(COLORS["hide"], 6, "rect")
            elif ch == "o":
                dot(COLORS["throw"], 3)
            elif ch == "C":
                dot(COLORS["check"], 5)
            elif kind == "note":
                dot(COLORS["note"], 4, "rect")
            elif kind == "terminal":
                dot(COLORS["terminal"], 6, "rect")
            elif kind == "keycard":
                dot(COLORS["key"], 5)
            elif kind == "pickup":
                dot(COLORS["pickup"], 6)
            elif kind == "mic":
                dot(COLORS["mic"], 4)
            elif kind == "switch":
                dot(COLORS["switch"], 3, "rect")
            elif kind in ("camera", "swivel"):
                dot(COLORS["camera"], 5)
            elif kind == "laser":
                dot(COLORS["laser"], 3)
    # Laser beams.
    markers = {}
    for r, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch not in BUILTIN:
                markers[ch] = (i + 0.5, h - 1 - r + 0.5)
    for t, m, a in entities:
        if t == "laser":
            to = [kv.split("=")[1] for kv in a.split() if kv.startswith("to=")][0]
            ax, ay = to_px(*markers[m], h)
            bx, by = to_px(*markers[to], h)
            draw.line([ax, ay, bx, by], fill=COLORS["laser"], width=1)


def cone(draw, x, y, heading, fov, rng, h, color):
    pts = [to_px(x, y, h)]
    for k in range(13):
        a = heading - fov / 2 + fov * k / 12
        pts.append(to_px(x + rng * math.cos(a), y + rng * math.sin(a), h))
    draw.polygon(pts, outline=color)


def render_frame(rows, entities, frame, font):
    h, w = len(rows), len(rows[0])
    img = Image.new("RGB", (w * CELL, h * CELL + 30), (0, 0, 0))
    draw = ImageDraw.Draw(img, "RGBA")
    dark = {tuple(c) for c in frame.get("dark", [])}
    doors = {tuple(c) for c in frame.get("doors", [])}
    draw_map(draw, rows, entities, dark, doors)

    b = frame["belief"]
    if b["kind"] == "OccupancyGrid":
        peak = max([c[2] for c in b.get("cells", [])] or [1])
        for i, j, p in b.get("cells", []):
            a = int(200 * min(1.0, p / peak))
            x0, y0 = i * CELL, (h - 1 - j) * CELL
            draw.rectangle([x0, y0, x0 + CELL - 1, y0 + CELL - 1], fill=(230, 40, 50, a))
    elif b["kind"] == "Particle":
        for x, y in b.get("particles", []):
            px, py = to_px(x, y, h)
            draw.ellipse([px - 1.5, py - 1.5, px + 1.5, py + 1.5], fill=(230, 40, 50, 200))
    elif b["kind"] == "Kalman":
        cx, cy, a, bb, th = b["ellipse"]
        for sig, alpha in ((2, 90), (1, 200)):
            pts = []
            for k in range(40):
                t = 2 * math.pi * k / 40
                ex = cx + sig * (a * math.cos(t) * math.cos(th) - bb * math.sin(t) * math.sin(th))
                ey = cy + sig * (a * math.cos(t) * math.sin(th) + bb * math.sin(t) * math.cos(th))
                pts.append(to_px(ex, ey, h))
            draw.polygon(pts, outline=(230, 40, 50, alpha))
        vx, vy = b.get("velocity", [0, 0])
        draw.line([to_px(cx, cy, h), to_px(cx + vx, cy + vy, h)], fill=(230, 40, 50), width=2)
    elif b["kind"] == "LastKnown" and b.get("has"):
        px, py = to_px(*b["point"], h)
        draw.line([px - 6, py - 6, px + 6, py + 6], fill=COLORS["belief"], width=3)
        draw.line([px - 6, py + 6, px + 6, py - 6], fill=COLORS["belief"], width=3)

    for x, y, heading, fov, rng in frame["cameras"]:
        cone(draw, x, y, heading, fov, rng, h, (200, 80, 220, 160))
    for x, y, look, mode in frame["units"]:
        cone(draw, x, y, look, math.radians(100), 7.5, h, (255, 150, 40, 120))
        px, py = to_px(x, y, h)
        draw.ellipse([px - 6, py - 6, px + 6, py + 6], fill=COLORS["unit"])
    for x, y in frame["roombas"]:
        px, py = to_px(x, y, h)
        draw.ellipse([px - 5, py - 5, px + 5, py + 5], outline=COLORS["roomba"], width=2)
    for ax, ay, bx, by in frame["mirrors"]:
        draw.line([to_px(ax, ay, h), to_px(bx, by, h)], fill=(180, 230, 255), width=3)
    for x, y in frame["jammers"]:
        px, py = to_px(x, y, h)
        r = 4 * CELL
        draw.ellipse([px - r, py - r, px + r, py + r], outline=(80, 255, 255, 140))
    for x, y in frame["decoys"]:
        px, py = to_px(x, y, h)
        draw.rectangle([px - 3, py - 3, px + 3, py + 3], fill=(255, 255, 0))
    for x, y, rad in frame.get("rings", []):
        px, py = to_px(x, y, h)
        r = rad * CELL
        draw.ellipse([px - r, py - r, px + r, py + r], outline=(255, 255, 255, 90))

    px, py = to_px(*frame["player"], h)
    col = (160, 160, 160) if frame["hidden"] else COLORS["player"]
    draw.ellipse([px - 6, py - 6, px + 6, py + 6], fill=col, outline=(0, 0, 0))

    caption = f"t={frame['t']:5.1f}s  {b['kind']}  " + " | ".join(frame.get("events", [])[:2])
    draw.text((4, h * CELL + 8), caption[:140], fill=(255, 255, 255), font=font)
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("stage", type=Path)
    ap.add_argument("--frames", type=Path)
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--every", type=int, default=1, help="use every Nth frame")
    ap.add_argument("--max-frames", type=int, default=600)
    args = ap.parse_args()

    header, rows, entities = parse_stage(args.stage)
    font = ImageFont.load_default()
    args.out.parent.mkdir(parents=True, exist_ok=True)
    if not args.frames:
        h, w = len(rows), len(rows[0])
        img = Image.new("RGB", (w * CELL, h * CELL + 30), (0, 0, 0))
        draw = ImageDraw.Draw(img, "RGBA")
        draw_map(draw, rows, entities)
        draw.text((4, h * CELL + 8), f"{header.get('title', '')}  [{header.get('tracker', '')}]", fill=(255, 255, 255), font=font)
        img.save(args.out)
        return
    frames = [json.loads(line) for line in args.frames.read_text().splitlines() if line.strip()]
    frames = frames[:: args.every][: args.max_frames]
    images = [render_frame(rows, entities, f, font) for f in frames]
    images[0].save(args.out, save_all=True, append_images=images[1:], duration=100, loop=0)


if __name__ == "__main__":
    main()
