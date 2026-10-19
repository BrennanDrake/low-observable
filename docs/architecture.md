# Architecture

## The big split

```
            ┌──────────────────────── core/ (engine-free C#, also a Unity package) ────────────────────────┐
            │                                                                                              │
 input ───▶ │  Episode.Step(PlayerCommand)                                                                 │
            │     ├─ TRUTH:  TruePose, Seeker.Pose, FloorPlan, noise generation                            │
            │     └─ BELIEF: Filter (player EKF), Seeker.Belief (particle filter)                          │
            │                                                                                              │
            │  Recorder (samples + events) ──▶ reveal screen, CSV for tools/                               │
            └──────────────────────────────────────────────────────────────────────────────────────────────┘
                      ▲ read-only                                    ▲ read-only
                      │                                              │
   dotnet/LowObservable.Sim (headless)          unity/.../com.brennandrake.lowobservable.unity
   dotnet/LowObservable.Core.Tests              EpisodeRunner ─▶ views, HUD, reveal
```

Everything that decides what happens lives in `core/`. Unity only turns input into a
`PlayerCommand`, calls `Episode.Step` from `FixedUpdate`, and draws. Because the core has no engine
dependency, the same code runs in the editor, in unit tests, in the headless simulator, and in CI.

## Truth and belief

Two worlds live side by side in an `Episode` and must not leak into each other:

| Truth (hidden) | Belief (shown) |
|---|---|
| `Episode.TruePose` | `Episode.Filter.Mean`, `Episode.Filter.Covariance` |
| `Episode.Seeker.Pose` | `Episode.Seeker.Belief` (the Seeker's estimate of *you*) |
| `LandmarkObservation.TrueLandmarkId` | The landmark the association step picked |

Rules of thumb:

- Rendering draws beliefs. When the player perceives something real (the Seeker in camera view, a
  decoy they threw), it is drawn at the *truth's offset from the drone* applied to the *believed*
  pose, so a lost player also misplaces what they see. See `SeekerView`.
- The autopilot plays from belief only, which makes it a fair stand-in for a player in balance sweeps.
- Truth is allowed in: the simulation itself, the reveal screen, the F1 debug overlay, and tests.

## One tick

`Episode.Step(command)` at a fixed 50 Hz (`EpisodeConfig.FixedDt`):

1. **Action** (once): ping, listen, throw decoy, or dock. A ping generates measurements immediately
   and a loud noise event.
2. **Move** (unless listening): the commanded odometry `u` is perturbed by noise scaled by mode and
   by the *true* floor type, then applied to `TruePose` with wall collisions. The filter predicts
   with the *commanded* `u` and noise scaled by the floor type it *believes* it is on.
3. **Camera** (every 0.5 s): measurements of lit landmarks in view.
4. **Association and update**: each measurement is gated against the blueprint landmarks.
   Confident matches update the EKF; ambiguous ones either pause the episode (`PauseForPlayer`) or
   use the best guess (`AutoBest`); no-match measurements are dropped.
5. **Decoys** emit their bursts.
6. **Seeker**: predict its particle cloud (10 Hz), hear this tick's noises, look (5 Hz, with
   negative information), then plan and move.
7. **Outcome**: captured if the Seeker reaches the true position; timed out at 600 s.
8. **Record** a sample every 0.1 s and every event.

## Module map

| Namespace | Files | Responsibility |
|---|---|---|
| `Maths` | `Vec2`, `Pose2`, `Angle`, `Matrix`, `CovarianceEllipse`, `ChiSquare`, `Rng` | Small, dependency-free numerics. Doubles throughout. |
| `World` | `FloorPlan`, `FloorPlanParser`, `GridPathfinder` | The grid map, ASCII levels, line of sight (exact grid traversal), A*. |
| `Robot` | `PlayerCommand`, `OdometryModel` | Commands, the motion model and its Jacobians. |
| `Sensors` | `PlayerSensors`, `LandmarkObservation`, `NoiseEvent` | Turn truth into noisy measurements. |
| `Estimation` | `EkfLocalizer`, `ParticleFilter2D`, `DataAssociation`, `Consistency` | The filters and how we judge them. |
| `Agents` | `SeekerAgent` | The hunter: its belief, senses and behaviour. |
| `Simulation` | `Episode`, `EpisodeEvents`, `TuningParameters`, `RunRecorder`, `Autopilot` | The game loop, its configuration, recording, and a bot. |
| `Levels` | `BuiltInLevels` | Levels compiled in for tests, the simulator and a fresh scene. |

## Determinism

Every random draw comes from an `Rng` stream derived from the episode seed and a label
(`"player-motion"`, `"player-sensors"`, `"seeker"`). The same seed and the same commands replay
the same episode exactly (`EpisodeTests.SameSeedReplaysIdentically`). This is what makes the reveal
scrubber, bug reports ("seed 1234, I died at 41 s") and future daily seeds possible. Adding a new
random consumer? Give it its own derived stream so it doesn't shift everyone else's sequence.

## Unity side

| Component | Role |
|---|---|
| `EpisodeRunner` | Owns the `Episode`; steps it in `FixedUpdate`; raises `EpisodeStarted`, `EventRaised`, `EpisodeEnded`; handles restart and the association pause. |
| `PlayerInputSource` | Abstract input. `KeyboardMouseInput` (both input backends) and `AutopilotInput`. |
| `FloorView` | Blueprint mesh, landmarks, uplink. |
| `PlayerBeliefView` | Drone at the belief mean, sigma ellipses, heading fan, match flashes. |
| `SeekerView` | Seeker's particle cloud, the Seeker when visible, decoys. |
| `TruthOverlayView` | F1 debug overlay of the true pose. |
| `HudView` | IMGUI HUD, event log, listen compass, association picker. |
| `RevealView` | End-of-floor paths, stats and scrubber. |
| `CameraRig` | Follows the believed position; frames the floor on reveal. |
| `LowObservableBootstrap` | Builds all of the above from code. |
| `ViewKit` | Placeholder art: unlit material, lines, shapes, ellipse helper, palette. |

## Extension points

- **New sensor:** add a method to `PlayerSensors` producing `LandmarkObservation`s (or a new
  measurement type plus an EKF update), add tuning fields, call it from `Episode`, test it.
- **Map-building (phase 2):** an `EkfSlam` alongside `EkfLocalizer` with landmarks in the state;
  `Episode` chooses which filter the player uses. The view already draws from a covariance, so
  landmark ellipses are a small addition.
- **Particle filter for the player (phase 3):** `ParticleFilter2D` generalises to poses; a
  `PlayerBelief` interface over EKF and PF would let views draw either.
- **More hunters:** `SeekerAgent` is self-contained; `Episode` holds one today. A list plus a shared
  belief is the phase 5 work.
- **New level:** drop an ASCII file in `levels/` and point a `LevelAsset` at it.
