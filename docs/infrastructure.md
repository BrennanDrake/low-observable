# Infrastructure

Same shape as `turtlebot-rfid-slam`, so moving between the robot repo and the game repo feels the
same: an `AGENTS.md` charter, scripts grouped by machine, a thin `Makefile`, a `VERSION` file, and
the same NAS registry and tagging.

```
               push / PR                         pull mirror (every 10 min)
 developer ───────────────▶ GitHub ──────────────────────────────▶ Gitea (NAS, LAN)
 cloud Claude                 │                                       │
                              │ GitHub Actions: core.yml              │ Gitea Actions: unity-build.yml
                              │ build, format, tests, sim smoke       │ GameCI Unity WebGL build
                              ▼                                       ▼
                         PR checks                          192.168.1.203:5000/low-observable-web
                                                                      │  :<VERSION> :git-<sha> :latest
                                                                      ▼
                                                            NAS: run_web.sh ─▶ http://<nas>:8088
```

## Hosting: GitHub primary, Gitea mirror

- **GitHub** (`BrennanDrake/low-observable`) is the source of truth. Claude Code cloud sessions
  clone, push and open PRs here, and `core.yml` runs on every PR.
- **Gitea** holds a pull mirror: a local copy with no cloud dependency, and the home of the Unity
  build runner. One-time setup in Gitea: *+ > New Migration > GitHub*, URL
  `https://github.com/BrennanDrake/low-observable`, tick **This repository will be a mirror** and
  **LFS**, and set the sync interval (10 minutes is fine). For a private repo, give it a GitHub
  fine-grained token with read access to contents.

Push to GitHub, not to the mirror; Gitea overwrites mirror branches on each sync.

## Git LFS

`.gitattributes` routes Unity binaries (textures, audio, models, fonts, video, archives)
through LFS. Run `git lfs install` once per machine before the first art commit.

GitHub's free LFS quota is small (1 GB storage and bandwidth). When it gets close, move LFS objects
to Gitea's built-in LFS server and keep code on GitHub:

1. Enable LFS in Gitea's `app.ini` (`[server] LFS_START_SERVER = true`) if it isn't already.
2. `cp .lfsconfig.example .lfsconfig`, uncomment the URL (adjust host and port), and commit it.
3. `git lfs push --all origin` once, so existing objects land on Gitea.

The catch: cloud sessions can't reach the LAN, so they can't fetch LFS objects from Gitea. That's
fine because they never need art (the core and its tests are code only); set `GIT_LFS_SKIP_SMUDGE=1`
in the cloud environment if checkouts complain.

## CI

### GitHub Actions: `.github/workflows/core.yml`

Runs on pushes to `main` and every PR, no Unity license needed:

| Job | Steps |
|---|---|
| `core` | `dotnet restore`; build with warnings as errors; `dotnet format --verify-no-changes`; unit tests (TRX uploaded); 3-seed headless sim of every policy as a smoke test |
| `tools` | `ruff check tools` |

This is the green/red signal Claude Code acts on when it drives a PR.

### Gitea Actions: `.gitea/workflows/unity-build.yml`

Builds WebGL with GameCI's `unity-builder` and pushes the playtest image to the NAS registry. It is
an **untested template**: it needs a runner and a Unity license first.

1. **Runner.** Enable Actions in Gitea (`[actions] ENABLED = true`), then on the NAS or brennan-dev
   run `act_runner` (Docker image `gitea/act_runner`) and register it with a token from
   *Site Administration > Actions > Runners*. The runner needs Docker access (GameCI runs the Unity
   editor in a container) and must reach `192.168.1.203:5000`. Add that registry to the runner
   host's Docker `insecure-registries`, as for the robot images.
2. **Unity license.** For a Personal license, activate Unity once on any machine and copy the `.ulf`
   file (Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`). Add repo secrets in Gitea:
   `UNITY_LICENSE` (the file's contents), `UNITY_EMAIL`, `UNITY_PASSWORD`. GameCI's docs on
   activation cover the current steps if Unity has changed them.
3. **Variable** (optional): `NAS_REGISTRY` if the registry isn't at `192.168.1.203:5000`.
4. Push to `main` (or run the workflow by hand). The job builds `build/webgl` with
   `BuildScript.WebGLFromCi` and runs `scripts/pc/build_web_image.sh`.

Because it's a mirror, the workflow runs on what Gitea pulls from GitHub. If you'd rather not wait
for the sync, use *Synchronize now* on the mirror's settings page.

## Versioning

`VERSION` holds the release (`0.1`). Bump and commit it for each playtest build. Builds stamp
`Application.version` as `<VERSION>+<sha>` (`BuildScript`), and the HUD shows it, so feedback names an
exact build.

## NAS registry and playtest server

Same registry and tag scheme as the robot images:

```bash
make unity                      # build/webgl (needs a local Unity editor)
make web-image                  # nginx image, pushed as :0.1, :git-<sha>, :latest with OCI labels
make registry-tags              # list what's on the registry
```

On the NAS (or any Docker host on the LAN):

```bash
nas_reg=192.168.1.203:5000 PORT=8088 ./scripts/nas/run_web.sh       # TAG=git-abc1234 to pin a build
```

Playtesters open `http://192.168.1.203:8088/`. `docker/web/nginx.conf` serves Unity's Brotli and
gzip files with the right `Content-Encoding` and MIME types; without it a WebGL build won't load.

Desktop builds (`./scripts/pc/build_unity.sh linux|windows`) are folders, not images: copy
`build/<target>` to a versioned folder on the NAS share.

`docker/core-test/Dockerfile` runs the core tests and a short simulation anywhere Docker runs,
without installing .NET:

```bash
docker build -f docker/core-test/Dockerfile -t low-observable-core-test . && docker run --rm low-observable-core-test
```

## Claude Code cloud sessions

- `.claude/settings.json` runs `scripts/cloud/session_start.sh` on session start. In cloud sessions
  (`CLAUDE_CODE_REMOTE=true`) it installs `dotnet-sdk-8.0` from the Ubuntu archive and restores
  packages; on local machines it does nothing. (Microsoft's `dotnet-install` script downloads from a
  host the cloud proxy blocks; the apt package works.)
- Cloud sessions can run `make lint`, `make test` and `make sim`, but not Unity, and can't reach the
  NAS or Gitea.
- Locally on brennan-dev, Claude Code can do everything, including Unity batch builds and pushing
  images.
