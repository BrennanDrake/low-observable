#!/usr/bin/env python3
"""Plot one recorded run: true path vs believed path, belief ellipses, and the Seeker.

This is the reveal screen, offline. Use it to study a run from the headless simulator or a
playtest:

    python3 tools/plot_run.py experiments/runs/annex-b_cautious_seed3_samples.csv \
        --level levels/annex-b.txt --out build/run.png

Needs matplotlib (pip install -r requirements-dev.txt).
"""

import argparse
import csv
import math
import sys
from pathlib import Path


def read_samples(path):
    with open(path, newline="") as f:
        return [{k: float(v) for k, v in row.items()} for row in csv.DictReader(f)]


def read_events(path):
    if not Path(path).exists():
        return []
    with open(path, newline="") as f:
        return list(csv.DictReader(f))


def read_level(path):
    """Returns rows of the ASCII level, top row first, comments and blank lines removed."""
    rows = []
    for line in Path(path).read_text().splitlines():
        line = line.rstrip()
        if not line or line.lstrip().startswith(";"):
            continue
        rows.append(line)
    return rows


def ellipse_points(cx, cy, cxx, cxy, cyy, sigmas=2.0, n=48):
    half = 0.5 * (cxx + cyy)
    diff = 0.5 * (cxx - cyy)
    root = math.sqrt(diff * diff + cxy * cxy)
    a = math.sqrt(max(half + root, 0.0)) * sigmas
    b = math.sqrt(max(half - root, 0.0)) * sigmas
    rot = 0.5 * math.atan2(2 * cxy, cxx - cyy)
    pts = []
    for k in range(n + 1):
        t = 2 * math.pi * k / n
        x, y = a * math.cos(t), b * math.sin(t)
        c, s = math.cos(rot), math.sin(rot)
        pts.append((cx + x * c - y * s, cy + x * s + y * c))
    return pts


def draw_level(ax, rows):
    from matplotlib.patches import Rectangle

    colors = {"#": "#1a1c21", ",": "#2a2c3d", ";": "#2a3540", "~": "#476672"}
    height = len(rows)
    for r, line in enumerate(rows):
        j = height - 1 - r
        for i, ch in enumerate(line):
            col = colors.get(ch)
            if col is None and ch.isupper() and ch not in "SUK":
                col = colors[","]
            if col is not None:
                ax.add_patch(Rectangle((i, j), 1, 1, color=col, zorder=0))
            if ch.isalpha() and ch not in "SUKv":
                ax.plot(i + 0.5, j + 0.5, marker="D", color="#e0b040", markersize=4, zorder=1)
            if ch == "U":
                ax.plot(i + 0.5, j + 0.5, marker="s", color="#59ff8c", markersize=8, zorder=1)


def main(argv=None):
    p = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("samples", help="*_samples.csv written by the simulator or the game")
    p.add_argument("--events", help="matching *_events.csv (default: from the samples name)")
    p.add_argument("--level", help="ASCII level file to draw underneath")
    p.add_argument("--every", type=float, default=5.0, help="seconds between belief ellipses")
    p.add_argument("--out", help="write an image instead of opening a window")
    args = p.parse_args(argv)

    import matplotlib

    if args.out:
        matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    samples = read_samples(args.samples)
    events_path = args.events or args.samples.replace("_samples.csv", "_events.csv")
    events = read_events(events_path)

    fig, ax = plt.subplots(figsize=(12, 7))
    ax.set_facecolor("#4d525c")
    if args.level:
        draw_level(ax, read_level(args.level))

    ax.plot([s["true_x"] for s in samples], [s["true_y"] for s in samples],
            color="white", lw=2, label="true path")
    ax.plot([s["est_x"] for s in samples], [s["est_y"] for s in samples],
            color="#5ab2ff", lw=2, label="believed path")
    ax.plot([s["seeker_x"] for s in samples], [s["seeker_y"] for s in samples],
            color="#ff5a4a", lw=1.2, alpha=0.8, label="Seeker")

    next_t = 0.0
    for s in samples:
        if s["time"] + 1e-9 < next_t:
            continue
        next_t = s["time"] + args.every
        pts = ellipse_points(s["est_x"], s["est_y"], s["cov_xx"], s["cov_xy"], s["cov_yy"])
        ax.plot([q[0] for q in pts], [q[1] for q in pts], color="#5ab2ff", lw=0.8, alpha=0.7)

    for e in events:
        if e["type"] == "PingFired":
            ax.plot(float(e["x"]), float(e["y"]), marker="o", mfc="none", color="#7fd0ff", ms=10)
        elif e["type"] == "ObservationApplied" and e["association_correct"] == "0":
            ax.plot(float(e["x"]), float(e["y"]), marker="x", color="#ff9a6a", ms=10, mew=2)

    last = samples[-1]
    ax.set_title(f"Run ending at {last['time']:.1f}s. Blue ellipses: 2-sigma belief every "
                 f"{args.every:g}s. Circles: pings. Orange x: wrong landmark matches.")
    ax.set_aspect("equal")
    ax.legend(loc="upper right")
    ax.grid(False)
    if args.out:
        Path(args.out).parent.mkdir(parents=True, exist_ok=True)
        fig.savefig(args.out, dpi=130, bbox_inches="tight")
        print(f"wrote {args.out}")
    else:
        plt.show()
    return 0


if __name__ == "__main__":
    sys.exit(main())
