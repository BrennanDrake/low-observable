# Low Observable

A top-down stealth game where you never see the truth. You pilot a small drone through a facility
run by a rogue AI, and the screen shows only what your drone **believes**: an uncertainty ellipse
where it probably is, a blueprint it can't fully trust, and a red cloud showing where the AI's
hunter thinks *you* are. Win by managing both beliefs at once.

The mechanics are the math. Moving grows your uncertainty (the prediction step), sensing shrinks it
(the update), loud sensors give the hunter a fix on you, identical-looking landmarks make you match
the wrong one (data association), and decoys split the hunter's particle filter. Nobody explains
any of this in game; the reveal screen at the end of each floor shows where you really went, and
that is where it clicks.

> Status: **MVP groundwork**. The simulation core, headless simulator, tests, Unity integration
> package and infrastructure are in place. The Unity project still needs its first open in the
> editor (see [docs/unity-integration.md](docs/unity-integration.md)).

## Quick start

```bash
# Core tests and a balance run, no Unity needed (requires the .NET 8 SDK)
make test
make sim

# Unity: open unity/LowObservable in Unity Hub (Unity 6 LTS), then
#   menu Low Observable > Create Sample Scene, and press Play.
```

Controls: **WASD** drive, **Shift** sprint, **Ctrl** creep, **Space** ping, **F** listen,
**Q** throw decoy at the mouse, **E** dock, **R** restart, **F1** truth overlay (debug).

## Layout

| Path | What lives there |
|---|---|
| `core/` | Engine-free C# core (also a Unity package): EKF, particle filter, data association, sensors, Seeker AI, `Episode` loop |
| `dotnet/` | .NET projects that build `core/` for tests (`LowObservable.Core.Tests`) and the headless simulator (`LowObservable.Sim`) |
| `unity/LowObservable/` | Unity project; `Packages/com.brennandrake.lowobservable.unity/` holds the runner, views, HUD, reveal screen and editor tools |
| `levels/` | ASCII floor plans ([format](docs/level-format.md)) |
| `tools/` | Python: plot a run, summarise balance sweeps |
| `scripts/` | `pc/`, `nas/`, `cloud/` scripts, grouped by machine like the robot repo |
| `docker/` | Playtest web server image and a core-test image |
| `docs/` | Design, architecture, the estimation primer, Unity guide, tuning, infrastructure |
| `experiments/` | Balance sweep and playtest notes |

## Documentation

Start with [docs/README.md](docs/README.md). The most useful pages:

- [Game design (MVP)](docs/game-design.md): what the player does and why
- [Architecture](docs/architecture.md): truth vs belief, the tick, where to add things
- [Estimation primer](docs/estimation-primer.md): the math behind every mechanic, with code pointers
- [Unity integration](docs/unity-integration.md): first open, components, replacing placeholder art
- [Tuning and balance](docs/tuning.md): every parameter, and how to check balance with the simulator
- [Infrastructure](docs/infrastructure.md): GitHub + Gitea, CI, the NAS registry, playtest builds

## Versioning

`VERSION` holds the release (currently 0.1). Builds show `v<VERSION>+<git sha>` in the HUD, and
playtest images on the NAS are tagged `:<VERSION>`, `:git-<sha>` and `:latest`, the same scheme as
`turtlebot-rfid-slam`.
