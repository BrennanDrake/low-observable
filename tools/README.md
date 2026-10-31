# Tools

Python helpers for looking at runs. They read the CSVs written by `RunRecorder` (the headless simulator
writes them with `--out`; the game can write them too).

| Script | What it does | Needs |
|---|---|---|
| `plot_run.py` | The reveal screen offline: true vs believed path, belief ellipses, Seeker path, pings, wrong matches | matplotlib |
| `summarize.py` | Escape/capture rates and consistency per autopilot policy from `summary.csv` | standard library |

```bash
make sim-runs                                            # writes experiments/runs/
python3 tools/summarize.py experiments/runs/summary.csv
python3 tools/plot_run.py experiments/runs/annex-b_cautious_seed1_samples.csv --level levels/annex-b.txt --out build/run.png
```

Lint with `ruff check tools` (same rules as the robot repo).
