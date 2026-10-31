# Development workflow

## Setup

| Need | For |
|---|---|
| .NET 8 SDK | Core build, tests, simulator (`make lint`, `make test`, `make sim`) |
| Python 3.10+ and `pip install -r requirements-dev.txt` | ruff, `tools/plot_run.py` (matplotlib) |
| Unity 6 LTS via Unity Hub | The game ([unity-integration.md](unity-integration.md)) |
| Docker | Web image and NAS deployment (optional) |
| shellcheck | Script linting (optional; skipped if missing) |
| Git LFS | Before the first art commit: `git lfs install` |

## Everyday commands

```bash
make lint        # build with warnings as errors, dotnet format check, ruff, shellcheck
make test        # core unit tests
make sim         # every autopilot policy on Annex B, 30 seeds: the balance table
make sim-runs    # 5 seeds per policy with per-run CSVs in experiments/runs
make unity       # WebGL build (local Unity needed)
make web-image   # nginx image to the NAS registry
```

`dotnet format LowObservable.sln` fixes formatting that `make lint` complains about.

## Branches and PRs

- `main` is always green. Work on a branch, open a PR, let `core.yml` pass, merge.
- One topic per PR. Commit subjects are imperative and under ~70 characters; the body says why.
- PR descriptions open with **Before:** and **After:** paragraphs describing what a player or
  developer would notice, then how.
- A change that affects game feel includes the `make sim` table before and after.
- A change to estimation or rules includes a test.
- Unity changes made without the editor (for example in a cloud session) say so in the PR.

## Where to make a change

| I want to... | Change | Then |
|---|---|---|
| Tweak game feel | `TuningParameters.cs` (or a `TuningAsset` in Unity to experiment) | `make sim`, update [tuning.md](tuning.md) |
| Add a sensor or tool | `Sensors/`, `Simulation/Episode.cs`, new tuning fields, `PlayerCommand` | Test in `EpisodeTests`; key binding in `PlayerInputSource`; HUD line |
| Change the Seeker | `Agents/SeekerAgent.cs` | `make sim` (all policies), `ParticleFilterTests` |
| Edit a level | `levels/*.txt` and `BuiltInLevels.cs` | `make sim` with `--level-file` |
| Change visuals | A view in the Unity package | Keep drawing beliefs, not truth |
| Analyze runs | `tools/` | `ruff check tools` |

## Debugging a run

Every episode is deterministic for a given seed and command sequence.

- **In Unity:** the HUD shows the seed. Set `Seed` on the bootstrap to start from that seed; in Play
  mode, untick `NewSeedOnRestart` on the Episode Runner object so R replays the same seed. F1 shows the truth overlay.
- **Headless:** `./scripts/pc/sim.sh --policy cautious --seed-start 17 --seeds 1 --out build/dbg`
  writes that run's samples and events; `tools/plot_run.py` draws it.
- **In a test:** construct an `Episode` with the seed and drive it with an `Autopilot` or a scripted
  command list, as `EpisodeTests` does.

## Working with Claude Code

`AGENTS.md` is the charter both Claude Code and Cursor read (`CLAUDE.md` imports it). The scoped rules
in `.cursor/rules/` apply to Claude Code too.

- **Cloud sessions** (claude.ai): the session start hook installs .NET, so Claude can build, test,
  run the simulator and open PRs. It can't run Unity or reach the NAS or Gitea. Good for core work,
  tuning sweeps, docs and tools.
- **Local sessions** on brennan-dev: everything, including Unity batch builds (`make unity`), Docker
  and the registry. Good for Unity work, since you can check the editor's Console together.
- Ask for balance work in terms of the simulator: "make passive escape less than 40% without dropping
  cautious below 50%" is a task Claude can verify on its own with `make sim`.

## Releasing a playtest build

1. Bump `VERSION`, commit, merge to `main`.
2. Either let the Gitea runner build it (`unity-build.yml`), or locally: `make unity && make web-image`.
3. On the NAS: `./scripts/nas/run_web.sh`. Share `http://192.168.1.203:8088/`.
4. Ask testers to include the version and seed from the HUD in their feedback.
