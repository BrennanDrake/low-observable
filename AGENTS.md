# Agent charter

Rules for AI agents (Claude Code, Cursor) working in this repo. Same shape as the charter in
`turtlebot-rfid-slam`. When this file conflicts with an explicit instruction for the current task,
the instruction wins.

1. **This file**: repo map, hard rules, quality gates.
2. **`.cursor/rules/*.mdc`**: scoped rules by path (also worth reading for Claude Code).
3. **`docs/`**: design and architecture. Read `docs/architecture.md` before changing the core.

## Layout (where to work)

| Area | Purpose |
|---|---|
| `core/Runtime/` | Engine-free simulation and estimation. **No `UnityEngine`** (the asmdef sets `noEngineReferences`). C# 9 only. |
| `dotnet/` | Builds `core/` for tests and the headless simulator. New tests go in `dotnet/LowObservable.Core.Tests/`. `LowObservable.Unity.TypeCheck`/`.EditorCheck` compile the Unity package against Unity's reference assemblies (`make unity-check`). |
| `unity/LowObservable/Packages/com.brennandrake.lowobservable.unity/` | Unity glue. `Runtime/Escape/` is the escape campaign (the game); the rest is the drone prototype. Reads the core; never re-implements game rules. |
| `unity/LowObservable/Assets/` | Scenes and assets created in the editor. |
| `levels/` | `levels/stages/*.lvl` are the escape levels: run `make stage-gen` after editing one to regenerate `core/Runtime/Stages/BuiltInStages.cs` (a test checks they match). Top-level files are drone floors, mirrored in `core/Runtime/Levels/BuiltInLevels.cs`. |
| `tools/`, `scripts/`, `docker/` | Python analysis, machine-grouped scripts, images. |

## Hard rules

- **Truth vs belief (escape campaign).** The player is shown the truth and WARDEN's belief
  (`Stage.Tracker`). WARDEN's units and dispatch act **only** on the tracker's belief, never on
  `Stage.Player`; sensors are the only path from truth to belief, through `ReportFix`/`ReportMiss`.
  `ExploitDetector` may read truth (it judges what the player pulled off). `StageBot` may use only what
  a player sees on screen.
- **Truth vs belief (drone prototype).** Gameplay and rendering read the player's belief
  (`Episode.Filter`) and the Seeker's belief (`Episode.Seeker.Belief`). Ground truth
  (`Episode.TruePose`, `Seeker.Pose`, `LandmarkObservation.TrueLandmarkId`) is only for the
  simulation itself, the reveal screen, the F1 debug overlay and tests. The autopilot must never read truth.
- **Determinism.** All randomness goes through `Maths.Rng` streams derived from the episode seed.
  Never use `System.Random`, `UnityEngine.Random` or wall-clock time in the core.
- **C# 9.** Unity compiles the core with C# 9: no file-scoped namespaces, no `record struct`, no
  global usings, no `required`. The dotnet build pins `LangVersion` 9.0 and treats warnings as errors.
- **Tuning lives in `StageTuning`** (escape) **or `TuningParameters`** (drone). No magic numbers for
  game feel elsewhere; add a field with a doc comment and update `docs/tuning.md`.
- **Tools act on WARDEN's inputs, never on its decisions.** A new tool creates, hides or distorts
  sensor reports; it must not reach into a tracker or a unit directly. That rule is what keeps every
  tool working against every algorithm.
- **Comment the math.** Estimation code explains the equation it implements and why (see
  `EkfLocalizer`). Players never see it, but the next person tuning it will.
- **Never commit** `bin/`, `obj/`, Unity `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or build output.

## Quality gates

- `make lint`: build with warnings as errors, `dotnet format --verify-no-changes`, ruff, shellcheck.
- `make test`: core unit tests. Add a test for every estimation or rules change.
- `make stages`: every escape level reachable with sensors off, plus a bot balance table. Paste the
  before/after table in the PR when a change affects game feel or a level.
- `make unity-check`: compile the Unity scripts against Unity's reference assemblies.
- `make sim`: the drone prototype's headless balance check.
- Unity code can't be compiled outside the editor; keep it thin and keep logic in the core.

## Commits and PRs

Imperative subject lines under ~70 characters, a body that says why. One topic per PR. PR
descriptions open with "Before:" and "After:" paragraphs describing what a player or developer would
notice.
