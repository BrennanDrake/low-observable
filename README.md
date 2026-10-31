# Low Observable

A top-down stealth game about escaping a facility run by an AI that has to guess where you are. You
always see the truth: yourself, the guards, the cameras. Over it, in red, you see **WARDEN's belief**
about where you are, drawn the way its current algorithm represents it. Escape by making that belief
wrong in useful ways.

Each zone of the facility runs a different tracking algorithm, and each one has the weaknesses the
textbooks warn about. The cell block's SIGHTLINE only remembers where you were last seen. HEATMAP is a
grid Bayes filter that learns from empty rooms. The offices' KESTREL is a Kalman filter: one ellipse,
so two alarms make it look in the empty space between them, and it throws away reports that disagree
with it. The labs' SWARM is a particle filter that can split between two guesses, and starves if you
feed it one fake sighting it's sure of. Noisemakers, roombas in party hats, jammers, mirrors and
forged sightings are how you exploit them. Each exploit you pull off goes in a journal with its real
name.

> Status: **escape MVP**. Nine levels across the cell block, offices and labs, four trackers,
> tools, a campaign with an exploit journal, a headless runner and 75 core tests. The Unity layer
> compiles against Unity's reference assemblies but still needs its first open in the editor (see
> [docs/unity-integration.md](docs/unity-integration.md)). The earlier drone prototype is described in
> [docs/drone-prototype.md](docs/drone-prototype.md).

## Quick start

```bash
# No Unity needed (requires the .NET 8 SDK)
make test          # core unit tests
make stages        # every escape level reachable, plus a bot balance table
make unity-check   # compile the Unity scripts against Unity's reference assemblies

# Unity: open unity/LowObservable in Unity Hub (Unity 6 LTS), then
#   menu Low Observable > Create Escape Scene (campaign), and press Play.
```

Controls: **WASD** move, **Shift** run, **Ctrl** sneak, **E** interact (hold at terminals),
**1-5** tools, **left click** use the tool at the cursor, **scroll** rotate a mirror, **Tab**
WARDEN's spec sheet, **M** map, **R** restart, **Esc** pause.

## Layout

| Path | What lives there |
|---|---|
| `core/` | Engine-free C# core (also a Unity package). `Tracking/` the four WARDEN trackers, `Stages/` the escape simulation, tools and levels, `Campaign/` progress and saves; plus the drone prototype's EKF, particle filter and `Episode` loop |
| `dotnet/` | .NET projects that build `core/` for tests (`LowObservable.Core.Tests`), the headless runner (`LowObservable.Sim`, `stage` subcommand for the escape levels), and the Unity type checks |
| `unity/LowObservable/` | Unity project; `Packages/com.brennandrake.lowobservable.unity/Runtime/Escape/` is the escape campaign front end |
| `levels/` | `stages/` holds the escape levels ([format](docs/stage-format.md)); the top level holds drone floors ([format](docs/level-format.md)) |
| `tools/` | Python: render a level or a bot replay (`render_stage.py`), plot a run, summarise balance sweeps |
| `scripts/` | `pc/`, `nas/`, `cloud/` scripts, grouped by machine like the robot repo |
| `docker/` | Playtest web server image and a core-test image |
| `docs/` | Design, architecture, the estimation primer, Unity guide, tuning, infrastructure |
| `experiments/` | Balance sweep and playtest notes |

## Documentation

Start with [docs/README.md](docs/README.md). The most useful pages:

- [Game design (MVP)](docs/game-design.md): what the player does and why
- [Escape concept](docs/design/escape-concept.md): story, all of WARDEN's models, the toolkit, progression
- [Stage format](docs/stage-format.md): writing escape levels
- [Architecture](docs/architecture.md): truth vs belief, the tick, where to add things
- [Estimation primer](docs/estimation-primer.md): the math behind every mechanic, with code pointers
- [Unity integration](docs/unity-integration.md): first open, components, replacing placeholder art
- [Tuning and balance](docs/tuning.md): every parameter, and how to check balance with the simulator
- [Infrastructure](docs/infrastructure.md): GitHub + Gitea, CI, the NAS registry, playtest builds

## Versioning

`VERSION` holds the release (currently 0.1). Builds show `v<VERSION>+<git sha>` in the HUD, and
playtest images on the NAS are tagged `:<VERSION>`, `:git-<sha>` and `:latest`, the same scheme as
`turtlebot-rfid-slam`.
