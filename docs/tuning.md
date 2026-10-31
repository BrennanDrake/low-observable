# Tuning

Every number that shapes game feel lives in `core/Runtime/Simulation/TuningParameters.cs`. Units are
SI: meters, seconds, radians (the code writes degrees as `x * Angle.Deg2Rad`). In Unity, a
`TuningAsset` exposes the same fields in the Inspector; the simulator overrides them with
`--set Name=value`.

## Parameters

### Player motion

| Field | Default | Effect on play |
|---|---|---|
| `CreepSpeed` / `WalkSpeed` / `SprintSpeed` | 0.6 / 1.4 / 2.8 m/s | Speed per mode |
| `TurnRate` | 2.4 rad/s | Turning speed |
| `PlayerRadius` | 0.25 m | Collision radius |
| `OdomDistanceVariancePerMeter` | 0.0025 m²/m | Along-track drift |
| `OdomHeadingVariancePerMeter` | 0.0008 rad²/m | Heading drift while driving; becomes lateral error |
| `OdomHeadingVariancePerRadian` | 0.004 rad²/rad | Heading drift while turning |
| `CreepNoiseScale` / `WalkNoiseScale` / `SprintNoiseScale` | 0.5 / 1 / 3 | Odometry noise multiplier per mode |
| `SlickNoiseScale` | 8 | Extra slip on slick floor. The filter only applies it where it *believes* the floor is slick |

### Player sensors

| Field | Default | Effect on play |
|---|---|---|
| `PingRange` | 9 m | Ping reach |
| `PingRangeSigma` / `PingBearingSigma` | 0.05 m / 1° | Ping precision: a ping nearly pins you down |
| `PingCooldown` | 1.5 s | Minimum time between pings |
| `CameraRange` / `CameraFieldOfView` | 6 m / 100° | What the camera can recognize |
| `CameraRangeSigmaBase` / `CameraRangeSigmaPerMeter` | 0.15 m / 0.08 m/m | Camera range noise grows with distance, so fixes are long and thin |
| `CameraBearingSigma` | 2° | Camera bearing precision |
| `CameraPeriod` | 0.5 s | Camera frame interval |
| `ListenDuration` / `ListenRange` / `ListenBearingSigma` | 1 s / 16 m / 12° | Listen locks you in place for a noisy bearing to the Seeker |
| `DecoyCharges` / `DecoyThrowRange` | 2 / 5 m | Decoys per floor and how far they go |
| `DecoyBurstPeriod` / `DecoyBursts` | 2 s / 3 | Decoy emits a fake ping every 2 s, three times |
| `AssociationGate` | 9.21 | χ²(2, 99%) gate on squared Mahalanobis distance |
| `AmbiguityRatio` | 3 | Likelihood ratio below which a match is ambiguous and the player is asked |

### Acoustics

| Field | Default | Effect on play |
|---|---|---|
| `CreepLoudness` / `WalkLoudness` / `SprintLoudness` | 1.2 / 4.5 / 10 m | Footstep audible radius |
| `FootstepPeriod` | 0.5 s | Footstep interval |
| `PingLoudness` / `DecoyLoudness` | 26 m / 26 m | Keep these equal: decoys only work if they sound like pings |
| `FailedDockLoudness` | 6 m | The cost of a wrong dock |
| `WallMuffling` | 0.45 | Audible radius multiplier without line of sight |
| `HearingSigmaBase` / `HearingSigmaPerMeter` | 0.4 m / 0.12 m/m | How precisely the Seeker places a sound |

### Seeker

| Field | Default | Effect on play |
|---|---|---|
| `SeekerSearchSpeed` / `SeekerHuntSpeed` | 1.3 / 2.1 m/s | Search is slower than your walk; hunt is slower than your sprint |
| `SeekerTurnRate` | 3 rad/s | |
| `SeekerVisionRange` / `SeekerFieldOfView` | 7 m / 110° | Vision cone |
| `SeekerDarkVisionRange` | 1.5 m | It sees you in the dark only this close |
| `SeekerVisionSigma` / `SeekerVisionPeriod` | 0.25 m / 0.2 s | Precision and rate of visual fixes |
| `CaptureRadius` | 0.6 m | Catches you within this distance of your true position |
| `SeekerParticleCount` | 600 | Belief resolution (and CPU cost) |
| `SeekerAssumedPlayerSpeed` | 1.6 m/s | How fast its cloud spreads while you're quiet |
| `SeekerNegativeInfoWeight` | 0.08 | Weight kept by particles it looked at and saw nothing. Lower = it clears rooms faster |
| `SeekerInjectionFraction` | 0.01 | Uniform particles per look; lets it recover from a decoy |
| `SeekerHuntSpread` | 2 m | Belief spread below which it hunts |
| `SeekerReplanPeriod` | 0.5 s | Path replanning interval |
| `SeekerScanAmplitude` | 50° | Camera sweep while searching |

### Objective and start

| Field | Default | Effect on play |
|---|---|---|
| `DockRadius` | 0.6 m | How close to the uplink a dock must truly be |
| `DockCooldown` | 1 s | Stops dock spam (and its noise) |
| `MaxEpisodeSeconds` | 600 s | Time-out |
| `InitialPositionSigma` / `InitialHeadingSigma` | 0.1 m / 2° | How well you know your start |

`EpisodeConfig` (not tuning, but relevant): `SeekerStartsAlerted` (default false; when true, the
Seeker starts knowing roughly where you are), `Association` (`PauseForPlayer` or `AutoBest`).

## Balance workflow

1. Run the baseline: `make sim` (every autopilot policy, 30 seeds, Annex B).
2. Change a value with `--set`, no rebuild needed:
   ```bash
   ./scripts/pc/sim.sh --policy all --seeds 30 --set PingLoudness=16 --set SeekerHuntSpeed=2.4
   ```
3. When a number looks worth keeping, change the default in `TuningParameters.cs`, rerun `make sim`,
   and paste the before/after tables in the PR.
4. To see *why* a run went the way it did, write per-run CSVs and plot one:
   ```bash
   ./scripts/pc/sim.sh --policy cautious --seeds 5 --out experiments/runs
   python3 tools/plot_run.py experiments/runs/annex-b_cautious_seed3_samples.csv \
       --level levels/annex-b.txt --out build/run.png
   python3 tools/summarize.py experiments/runs/summary.csv
   ```
   `plot_run.py` is the reveal screen offline: true path, believed path with ellipses every few
   seconds, the Seeker's path and the events.

The autopilot profiles are stand-ins for player styles, not targets:

| Profile | Plays like |
|---|---|
| `reckless` | Sprints everywhere, pings whenever its spread passes 0.4 m, no decoys |
| `cautious` | Walks, creeps when the Seeker's cloud is near, pings past 0.8 m, throws decoys |
| `ghost` | Creeps, pings only past 1.4 m, throws decoys early |
| `passive` | Walks on camera and odometry only, never pings, feels for the uplink by docking (alias `blind`) |

Rough targets for the MVP floor: reckless rarely escapes, a thoughtful style escapes about half the
time, and nobody escapes every time.

## Current numbers (Annex B, seeds 1-30)

```
policy      escaped  captured  timeout   avg_time  avg_err  inside95  wrong_assoc  pings  seen
reckless       13 %      87 %      0 %      35.7s    9.25m     32 %          3.1   21.0   4.3
cautious       57 %      43 %      0 %      42.4s    1.73m     78 %          0.3   13.8   2.7
ghost          27 %      73 %      0 %      60.4s    0.58m     87 %          0.1    4.6   6.2
passive        73 %      23 %      3 %     119.4s    3.35m     53 %          8.6    0.0   4.0
```

`avg_err` is the final position error, `inside95` the fraction of time the truth was inside the 95%
ellipse, `wrong_assoc` wrong landmark matches per run, `seen` the number of times the Seeker saw you.

## Known balance issues (for playtests)

- **Passive is too strong.** Never pinging escapes 73% of the time despite a badly calibrated belief
  (8.6 wrong matches per run, truth inside the 95% ellipse only 53% of the time). The Seeker hears
  footsteps poorly and the camera alone is enough to reach the uplink by retrying docks. Candidate
  fixes to try with `--set`: louder failed docks (`FailedDockLoudness`), a longer `DockCooldown`,
  louder walking, or a smaller `DockRadius`. The bots aren't players, so confirm with playtests before
  committing to one.
- **Ghost is weaker than expected** (27%). Creeping takes long enough that the Seeker's search finds
  it. This may be the bot's route choice rather than the tuning.
- Ping loudness sweeps at 16 and 12 m made pinging nearly free; 26 m was kept.

## Escape campaign (`StageTuning`)

The escape game keeps its numbers in `core/Runtime/Stages/StageTuning.cs`, apart from the drone
prototype's `TuningParameters`. The stage runner overrides them the same way:
`dotnet run --project dotnet/LowObservable.Sim -- stage --set RunSpeed=4.0`. Run `make stages`
before and after a change and paste both tables in the PR.

Loudness is a radius in meters: a listener hears a sound within loudness × its sensitivity, and
only `WallMuffling` (0.45) of that when there is no line of sight.

### Player

| Field | Default | Effect on play |
|---|---|---|
| `SneakSpeed` / `WalkSpeed` / `RunSpeed` | 1.3 / 2.6 / 4.6 m/s | Speed per mode |
| `StaminaSeconds` / `StaminaRecovery` | 2.5 s / 0.5 per s | How long you can run, and how fast it comes back. Without it the runner bot cleared everything |
| `SneakLoudness` / `WalkLoudness` / `RunLoudness` | 0.8 / 3.5 / 9 m | Footstep radius per mode. Sneaking past a mic is fine; running past one is not |
| `StrideLength` | 0.9 m | Distance per footstep |
| `InteractRange` | 1.3 m | Reach for notes, lockers, terminals, switches |
| `HideLoudness` | 1.5 m | Noise of getting in or out of a locker |
| `CarryLimit` / `ThrowRange` / `ThrowLoudness` | 3 / 8 m / 7 m | Throwables: how many, how far, how loud the landing |
| `DoorLoudness` / `SwitchLoudness` | 5 / 2.5 m | Interacting is not silent |
| `TerminalUseSeconds` / `TerminalBeepLoudness` | 2.5 s / 6 m | Hold time at a terminal, and the beeps WARDEN can hear meanwhile |

### Tools

| Field | Default | Effect on play |
|---|---|---|
| `NoisemakerCharges` / `NoisemakerBursts` / `NoisemakerBurstPeriod` | 2 / 4 / 1.2 s | A moving decoy when it sticks to a roomba (`NoisemakerStickRadius` 0.8 m) |
| `NoisemakerLoudness` | 9 m | As loud as running, so sensors can't tell it from you |
| `JammerCharges` / `JammerRadius` / `JammerDuration` | 1 / 4 m / 20 s | Sensors inside report nothing, not even "nobody here" |
| `JammerInterferencePeriod` / `JammerInterferenceSigma` | 3 s / 3 m | WARDEN gets a vague fix on the jammer itself, so it isn't free |
| `MirrorCharges` / `MirrorLength` / `MirrorPlaceRange` | 2 / 1.2 m / 2 m | Mirrors, placed within reach |
| `ForgeRange` / `ForgeSigma` / `ForgeOutlierWeight` | 14 m / 0.3 m / 1e-5 | A forged fix is very precise and claims almost no chance of being wrong. That near-zero outlier weight is what starves SWARM |

### Sensors

| Field | Default | Effect on play |
|---|---|---|
| `CameraRange` / `CameraFieldOfView` / `CameraPeriod` | 8 m / 70° / 0.2 s | Fixed cameras |
| `SwivelPeriod` | 6 s | Full sweep time for swivel cameras unless the level sets `period=` |
| `DarkVisionRange` | 1.5 m | Cameras and guards only see this far into dark cells |
| `VisionSigma` | 0.3 m | Noise on a sighting |
| `MissLikelihood` | 0.08 | Chance a covering sensor misses you. Used by the grid and swarm to learn from "nobody here" |
| `MicRadius` / `UnitHearing` | 1.3 / 1.0 | Hearing multipliers on loudness for mics and guards |
| `HearingSigmaBase` / `HearingSigmaPerMeter` | 0.6 m / 0.15 m/m | Heard reports get vaguer with distance |
| `RoombaSpeed` / `RoombaBumpRange` / `RoombaSigma` / `RoombaCooldown` | 0.9 m/s / 0.8 m / 0.8 m / 3 s | Roombas report what they bump into, then apologize |
| `LaserSigma` | 0.3 m | Tripwire fix noise |
| `SensorOutlierWeight` | 0.01 | How much any honest report allows for being wrong |

### Units and dispatch

| Field | Default | Effect on play |
|---|---|---|
| `PatrolSpeed` / `InvestigateSpeed` / `ChaseSpeed` | 1.6 / 2.6 / 3.8 m/s | Chase is slower than running, faster than walking |
| `GuardVisionRange` / `GuardFieldOfView` | 7.5 m / 100° | Guard cones |
| `CaptureRadius` | 0.7 m | Catch distance |
| `SearchDuration` / `ChaseMemory` / `LockerCheckSeconds` | 6 / 1.5 / 1.2 s | How long a guard searches, keeps chasing out of sight, and spends on a locker |
| `DispatchPeriod` / `DispatchRadius` | 0.5 s / 3 m | How often WARDEN re-plans, and the radius it measures belief mass in |
| `DispatchConfidence` / `AllUnitsConfidence` | 0.3 / 0.75 | Belief mass needed to send one unit, or every unit |
| `LockerCheckMass` | 0.25 | Belief mass near a locker before a guard opens it |

### Trackers

| Field | Default | Effect on play |
|---|---|---|
| `TrackerAssumedSpeed` | 1.6 m/s | How fast the grid and swarm assume you move while unseen. Lower than your walk speed on purpose: outrunning the model is a lesson |
| `ParticleCount` / `ParticleInjectionPerSecond` | 500 / 0.02 | SWARM's size, and how fast it recovers from starvation |
| `KalmanAccelerationNoise` | 0.8 m/s² | How quickly KESTREL lets you change direction |
| `KalmanGate` | 13.8 | χ²(2, 99.9%) gate on squared Mahalanobis distance: reports further out are thrown away |
| `ExposureRadius` | 1.5 m | Exposure on the HUD is belief mass within this of you |
