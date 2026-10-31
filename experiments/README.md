# Experiments

Balance sweeps and playtest notes. Raw run logs (`runs/`, `playtests/raw/`) are gitignored because the
simulator regenerates them from a seed; commit the notes and summaries that explain a decision.

Suggested layout, mirroring the trial metadata in `turtlebot-rfid-slam/experiments/`:

```
experiments/
  balance/2026-10-19-annex-b-baseline.md   # what was run, the numbers, what changed because of it
  playtests/2026-11-02-first-five.md       # observations per tester, quotes, what confused them
```

Reproduce any sweep with the exact command recorded in its note, e.g.
`./scripts/pc/sim.sh --policy all --seeds 30 --set PingLoudness=16`.
