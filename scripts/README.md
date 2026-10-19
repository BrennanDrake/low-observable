# Scripts

Grouped by the machine that runs them, like `turtlebot-rfid-slam/scripts/`.

| Path | Runs on | What it does |
|---|---|---|
| `pc/lint.sh` | dev box, CI | Build core with warnings as errors, `dotnet format` check, ruff on `tools/`, shellcheck |
| `pc/test_core.sh` | anywhere with .NET 8 | Core unit tests (`dotnet test`), extra args pass through |
| `pc/sim.sh` | anywhere with .NET 8 | Headless autopilot episodes for balance checks and run logs |
| `pc/build_unity.sh` | dev box with Unity | Batch-mode Unity build: `webgl`, `linux`, `windows` into `build/` |
| `pc/build_web_image.sh` | dev box with Docker | nginx image of the WebGL build, pushed to the NAS registry with `:<VERSION>`, `:git-<sha>`, `:latest` |
| `pc/registry_tags.sh` | anywhere on the LAN | What the NAS registry holds |
| `nas/run_web.sh` | NAS | Pull and serve the playtest build |
| `cloud/session_start.sh` | Claude Code cloud sessions | Installs .NET 8 so tests run in a fresh container (wired in `.claude/settings.json`) |

All of them `cd` to the repo root first, so they can be run from anywhere.
