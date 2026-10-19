#!/usr/bin/env python3
"""Summarise a simulator summary.csv by policy (escape rates, error, consistency).

    python3 tools/summarize.py experiments/runs/summary.csv

Standard library only, so it runs anywhere Python 3 does.
"""

import csv
import sys
from collections import defaultdict


def main(argv=None):
    argv = argv if argv is not None else sys.argv[1:]
    if not argv:
        print(__doc__)
        return 2
    groups = defaultdict(list)
    with open(argv[0], newline="") as f:
        for row in csv.DictReader(f):
            groups[row["policy"]].append(row)

    header = (f"{'policy':<10} {'n':>4} {'escaped':>8} {'captured':>9} {'avg_err':>8} "
              f"{'inside95':>9} {'wrong':>6}")
    print(header)
    print("-" * len(header))
    for policy, rows in groups.items():
        n = len(rows)

        def frac(outcome):
            return sum(r["outcome"] == outcome for r in rows) / n

        def mean(key):
            return sum(float(r[key]) for r in rows) / n

        print(f"{policy:<10} {n:>4} {frac('Escaped'):>8.0%} {frac('Captured'):>9.0%} "
              f"{mean('avg_error'):>7.2f}m {mean('fraction_inside'):>9.0%} "
              f"{mean('wrong_assoc'):>6.1f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
