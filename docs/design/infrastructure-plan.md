# LOW OBSERVABLE: Repo & Infrastructure Plan

Modeled on `turtlebot-rfid-slam` (read from `/home/brennan/turtlebot-rfid-slam` on brennan-dev, 2026-10-19). The goal is that moving between the robot repo and the game repo feels the same: same charter files, same script layout, same versioning, and the same NAS registry.

## What the robotics repo does today

| Convention | In `turtlebot-rfid-slam` |
|---|---|
| Hosting | GitHub (`BrennanDrake/turtlebot-rfid-slam`), Git LFS enabled, `main` plus a `Cursor-Dev` branch |
| Agent charter | `AGENTS.md` (repo map, rule index), root `.cursorrules`, scoped `.cursor/rules/*.mdc` |
| Scripts | Grouped by machine: `scripts/pc/` and `scripts/robot/`, with a thin `Makefile` (`lint`, `build`, `test`) |
| Quality gate | `scripts/pc/lint.sh` (ruff E/F/W, compileall, xmllint) plus colcon tests |
| Containers | `robot/` and `server/` Dockerfiles, built from the repo root |
| Registry | NAS at `192.168.1.203:5000`; images tagged `:<VERSION>`, `:git-<sha>`, `:latest`, with OCI version/revision labels; `robot/VERSION` holds the release |
| Experiments | `experiments/` with trial metadata committed, while bags, maps and pose graphs are gitignored |
| CI | None that I could see (no `.github/` or `.gitea/` workflows) |

## Hosting: GitHub primary, Gitea mirror

**Recommendation:** create `BrennanDrake/low-observable` on GitHub as the main repo, and set up a **pull mirror** of it in your Gitea.

- **Why GitHub:** it matches the robot repo, and it's what Claude Code cloud sessions (like this project) can clone, push and open PRs against. A Gitea instance on your LAN isn't reachable from the cloud.
- **Why the Gitea mirror:** a local copy on the NAS with no cloud dependency, and a home for the Gitea Actions runner (below).
- **Unity's large files:** Unity art, audio and builds go in Git LFS. GitHub's free LFS quota is small, so if it fills up, point LFS at Gitea's built-in LFS server on the NAS with a committed `.lfsconfig`. The code stays on GitHub and the heavy binaries stay home.

## Repo layout

```
low-observable/
├── AGENTS.md              # charter: repo map, rule index (same shape as the robot repo)
├── CLAUDE.md              # one line: "See AGENTS.md" so Claude Code and Cursor share rules
├── .cursorrules
├── .cursor/rules/         # scoped: estimation-core.mdc, unity.mdc, docker.mdc, shell-scripts.mdc
├── VERSION                # release number, as robot/VERSION
├── Makefile               # lint / test / build / web
├── core/                  # engine-free C# estimation library (EKF, particle filter, sensor models, NEES)
│   ├── LowObservable.Core.csproj      # netstandard2.1, compiles the same .cs files Unity uses
│   └── tests/LowObservable.Core.Tests.csproj   # xUnit; runs with `dotnet test`, no Unity needed
├── unity/                 # Unity project
│   ├── Packages/com.brennandrake.lowobservable.core/   # UPM package pointing at core/ sources + asmdef
│   └── Assets/
├── tools/                 # Python: tuning noise parameters, plotting run logs (ruff, same pyproject rules)
├── experiments/           # playtest run logs (seed, inputs, true vs estimated states); raw logs gitignored
├── docker/
│   ├── core-test/Dockerfile           # dotnet SDK image for running core tests anywhere
│   └── web/Dockerfile                 # nginx serving the Unity WebGL build for playtests
└── scripts/
    ├── pc/                # lint.sh, test_core.sh, build_unity.sh (batch mode), build_web_image.sh, registry_tags.sh
    └── nas/               # run_web.sh (start the playtest server on the NAS)
```

**The important split:** `core/` has no Unity dependency. It holds the filters and the ground-truth simulation, so it can be unit-tested with `dotnet test` on any machine, in Docker or in a Claude Code cloud session. Unity only renders the belief and handles input.

## Builds, versioning and the NAS

Same pattern as the robot image, so `registry_tags.sh` works for both repos:

- `VERSION` holds the release (start at `0.1`). Bump and commit for each playtest build.
- `scripts/pc/build_unity.sh` runs Unity in batch mode (`-batchmode -nographics -executeMethod BuildScript.WebGL`) and writes to `build/` (gitignored).
- `scripts/pc/build_web_image.sh` wraps the WebGL build in an nginx image and pushes `192.168.1.203:5000/low-observable-web:<VERSION>`, `:git-<sha>` and `:latest` with OCI labels.
- `scripts/nas/run_web.sh` pulls `:latest` on the NAS and serves it on your LAN, so playtesters open a URL instead of installing anything.
- The game shows `v<VERSION> (git <sha>)` in a corner, as the robot container logs it on start, so playtest feedback is tied to an exact build.
- Desktop (Windows/Linux) builds go to a versioned folder on the NAS share; they're files, not images.

## CI (new for both repos)

| Job | Where | Runs |
|---|---|---|
| Core tests | GitHub Actions (free, no license needed) | `dotnet test core/tests`, plus ruff on `tools/` |
| Unity build + web image | Gitea Actions runner on the NAS or brennan-dev | GameCI `unityci/editor` image (personal Unity license activated once), then `build_web_image.sh` |

Core tests on every PR give Claude Code a green or red signal it can act on. Unity builds stay on your LAN because they need a license and are heavy.

## Running in parallel with the robot project

The two projects share more than tooling, and that's worth using:

- **Real noise models.** Fit the game's odometry drift and range noise from your TurtleBot's logs in `experiments/`, so the game's Q and R come from real hardware.
- **Same math, two hosts.** The EKF and particle filter in `core/` mirror what slam_toolbox and your `rfid_relocalization` work do. Writing them yourself for the game is good practice for the robot, and the robot's RFID landmarks map directly onto the game's landmarks.
- **Later (phase 3 or so):** Unity's ROS-TCP-Connector could replay real ROS 2 bags as game levels, or let the Seeker run against a real robot's map. Optional, but a strong demo.

## What's needed to start

1. Create an empty `BrennanDrake/low-observable` repo on GitHub and connect it to this project. Then I can scaffold everything above except the Unity project itself.
2. Create the Unity project locally (Unity Hub, 2D URP template) inside `unity/`, or let a Claude Code session on brennan-dev do it.
3. Optional: set up the Gitea pull mirror and a Gitea Actions runner.
