# Unity integration

The Unity project at `unity/LowObservable/` is deliberately thin. It holds two packages and very
little else, so the first open generates most of the project files.

| Package | Where | What |
|---|---|---|
| `com.brennandrake.lowobservable.core` | `core/` (referenced from `Packages/manifest.json` as `file:../../../core`) | The whole game simulation. Engine-free (`noEngineReferences`). |
| `com.brennandrake.lowobservable.unity` | `unity/LowObservable/Packages/com.brennandrake.lowobservable.unity/` | Escape campaign (`Runtime/Escape/`), the drone prototype's runner and views, input, editor menu, build script. |

> The Unity code was written outside the editor. `make unity-check` compiles it against Unity's real
> reference assemblies (the `UnityEngine.Modules` and `Unity3D.SDK` NuGet packages), for both input
> backends and the editor scripts, so API and type errors are caught. It has not been run in a real
> editor yet: expect a round of visual and feel fixes the first time it opens. The core is fully tested.

## First open (Unity 6 LTS)

1. Unity Hub > **Add** > `unity/LowObservable`. Pick a Unity 6 LTS editor (6000.0.x). Unity creates
   `ProjectSettings/`, `Library/` and `.meta` files.
2. If Unity asks to enable the new Input System backends, choose **Yes** (it restarts). Either
   setting works: the input code compiles against both via `#if ENABLE_INPUT_SYSTEM`. "Both" under
   *Project Settings > Player > Active Input Handling* is the most forgiving.
3. Fix anything in the Console. Most likely suspects are package versions in `manifest.json`;
   let Unity's Package Manager pick the versions it offers for your editor.
4. Menu **Low Observable > Create Escape Scene (campaign)**. This writes
   `Assets/LowObservable/Scenes/Escape.unity` (an orthographic camera and an `EscapeBootstrap`
   object) and makes it the first build scene. (*Create Drone Prototype Scene* still builds the old
   drone mode.)
5. Press **Play**. The campaign menu appears; level 1 is unlocked.
6. Commit what Unity generated: `ProjectSettings/` (including `ProjectVersion.txt`, which
   `scripts/pc/build_unity.sh` reads to find the editor), `Packages/packages-lock.json`, all `.meta`
   files, and the scene. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` (already ignored).

## The escape campaign

`EscapeBootstrap` builds everything from code in `Awake`:

```
Low Observable (EscapeBootstrap: LevelsFolder, Seed, UnlockAllLevels, StartLevel)
├── Escape Game (EscapeGame + EscapeInput)
├── Escape Views (StageMapView, BeliefView, SecurityView, PlayerView, EscapeHud, EscapeMenus)
└── Main Camera (EscapeCamera)
```

- `EscapeGame.FixedUpdate` calls `Stage.Step(input)` at the core's 50 Hz and raises each
  `StageEvent` for the views. It owns the screen flow (menu, briefing, play, pause, results) and the
  save: `CampaignProgress` serialized to `PlayerPrefs` under `lowobservable.escape.progress.v1`.
  *Low Observable > Erase Escape Save* clears it.
- `BeliefView` draws WARDEN's belief per algorithm: an X (last known), a point-filtered texture
  (grid), 1σ/2σ ellipses and a velocity line (Kalman), a particle system (particles). It reads the
  tracker objects directly; it never changes them.
- `SecurityView` draws camera and guard cones as meshes clipped by walls (28 rays each), plus
  sensors, gadgets and sound rings. `PlayerView` draws the player and the aim preview, using
  `Stage.ToolTarget` and `Stage.ToolBlockedReason` so the preview matches the rules exactly.
- HUD and menus are IMGUI (`EscapeHud`, `EscapeMenus`, shared `EscapeStyles`) and scale with screen
  height.

Inspector options on the bootstrap: **UnlockAllLevels** for testing, **StartLevel** (e.g.
`04-open-plan`) to skip the menu, and **LevelsFolder** (e.g. `../../levels/stages`) to load the
`.lvl` files directly so level edits show up on the next Play without regenerating
`BuiltInStages.cs`. See [stage-format.md](stage-format.md).

Escape controls are listed in [game-design.md](game-design.md#your-verbs-and-tools).

## Drone prototype controls

| Key | Action |
|---|---|
| W/S or ↑/↓ | Forward / back |
| A/D or ←/→ | Turn |
| Shift (hold) | Sprint |
| Ctrl or C (hold) | Creep |
| Space | Ping |
| F | Listen |
| Q | Throw decoy toward the mouse |
| E | Dock |
| 1-9, 0 | Pick a landmark when the game asks "which one was that?" (0 ignores the measurement) |
| R | Restart (new seed if `NewSeedOnRestart`) |
| F1 | Toggle the truth overlay (debug) |

## How the drone prototype fits together

`LowObservableBootstrap` builds everything from code in `Awake`, so the scene file stays trivial:

```
Low Observable (LowObservableBootstrap: Level, Tuning, Seed, Autopilot, AutopilotProfile)
├── Episode Runner (EpisodeRunner + KeyboardMouseInput or AutopilotInput)
├── Views (FloorView, SeekerView, PlayerBeliefView, TruthOverlayView, RevealView, HudView)
└── Main Camera (CameraRig)
```

`EpisodeRunner.FixedUpdate` calls `Episode.Step(command)` once per physics tick. Set
*Project Settings > Time > Fixed Timestep* to **0.02** (the default) so game time matches the core's
50 Hz. Views read `runner.Episode` in `LateUpdate` and never change it. See
[architecture.md](architecture.md) for the full picture.

To build the scene by hand instead (for example to place your own camera), add an `EpisodeRunner`
and an input component to one object, and the views you want to another, setting each view's
`Runner` field.

## Assets

- **Level asset** (*Low Observable > Create Level Asset*): a `LevelAsset` takes a `TextAsset`
  (a `.txt` in [level format](level-format.md)), inline text, or a built-in name. Drop it on the
  bootstrap's `Level` field.
- **Tuning asset** (*Low Observable > Create Tuning Asset*): a `TuningAsset` wraps
  `TuningParameters`, so every field is editable in the Inspector. Changes apply on restart (R).
  See [tuning.md](tuning.md).
- **Autopilot**: tick `Autopilot` on the bootstrap and pick a profile (`reckless`, `cautious`,
  `ghost`, `passive`) to watch a bot play from its belief. Useful for checking visuals without
  playing.

## Replacing placeholder art

All visuals are generated by `ViewKit` (unlit meshes and `LineRenderer`s, one shared material).
Each view is self-contained, so art can be replaced one view at a time:

- Keep reading the same data (`Episode.Filter`, `Episode.Seeker.Belief`, `Episode.Plan`), and keep
  drawing **beliefs**, not truth. The truth-vs-belief rule in [architecture.md](architecture.md)
  applies to art too: a drone sprite goes at the belief mean, not at `TruePose`.
- Coordinates: core meters map 1:1 to Unity units on the XY plane (`UnityConvert`). Z orders layers
  (`Layers` constants).
- Uncertainty ellipses come from `CovarianceEllipse`; `ViewKit.SetEllipse` turns one into points.
  A shader-based ellipse or a fog-of-uncertainty effect only needs the same three numbers
  (semi-axes and angle).
- The HUD uses IMGUI because it needs no assets. Moving to UI Toolkit is a straightforward
  replacement of `HudView`; the association picker's state lives in `EpisodeRunner`.

## Render pipeline

The project starts on the built-in renderer. `ViewKit` looks for `Sprites/Default`, then the URP
2D and Unlit shaders, then `Unlit/Color`, so switching to URP (2D renderer recommended) needs no code
changes. In builds, Unity strips shaders nothing references: if the game renders pink or logs
"No unlit shader found", add `Sprites/Default` (or the URP equivalent) to
*Project Settings > Graphics > Always Included Shaders*.

## Building

- From the editor: *File > Build Settings* (the sample scene is already registered).
- From the command line: `make unity` (WebGL) or `./scripts/pc/build_unity.sh linux|windows`. Output
  goes to `build/<target>/` at the repo root; the log to `build/unity-<target>.log`.
- On CI: `.gitea/workflows/unity-build.yml` (see [infrastructure.md](infrastructure.md)).

WebGL notes: `BuildScript` enables Brotli compression and `docker/web/nginx.conf` serves the
`Content-Encoding` headers it needs. Without that server config, a WebGL build opened from disk or a
plain web server will fail to load.

## Working with Claude Code on Unity code

Cloud sessions can't run Unity, so Unity changes there are type-checked only: run `make unity-check`
(it needs nuget.org once to fetch the reference assemblies). Keep logic in
`core/` (tested) and the Unity side thin. Locally, Claude Code can edit the Unity scripts while the
editor is open; Unity recompiles on focus and the Console shows errors.
