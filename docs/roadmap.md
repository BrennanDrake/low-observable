# Roadmap

Phases from [design/scope-and-roadmap.md](design/scope-and-roadmap.md), with current status. Each
phase ends in something playable, and each phase's exit test should pass before the next starts.

| Phase | Goal | Status |
|---|---|---|
| 0. Spike | Prove the two-belief tension feels good | **Folded into the MVP.** `open-room` is the spike level; play it first |
| 1. MVP: Evade the Seeker | Prove evasion teaches and is fun | **In progress.** Core done and tested; Unity layer written, not yet opened in the editor |
| 2. Unknown territory | Teach mapping (EKF-SLAM, loop closure) | Not started |
| 3. When one blob fails | Particle filter for your own belief, kidnapping, mirrored wing | Not started |
| 4. Roguelite structure | Procedural floors, runs, WARDEN director | Not started |
| 5. Smarter hunters | Multiple Seekers with a shared belief, traps, pose-graph SLAM | Not started |
| 6. Content & launch | Art, audio, codex, Steam demo | Not started |
| 7. Post-launch | Daily seeds, educator mode | Not started |

## Phase 1 checklist

### Done (groundwork)

- [x] Repo infrastructure matching `turtlebot-rfid-slam`: charter, Cursor rules, scripts, Makefile,
      `VERSION`, LFS, Docker images, NAS registry scripts
- [x] GitHub Actions CI (build, format, tests, sim smoke test); Gitea Unity build template
- [x] Engine-free core: EKF localization, Seeker particle filter, data association with gating and
      ambiguity, consistency metrics, deterministic RNG
- [x] Ground-truth simulation: odometry noise by mode and floor, ping, camera, listen, decoys, dock,
      acoustics with wall muffling, Seeker vision with negative information
- [x] Seeker agent: belief-driven search and hunt, A* on the grid
- [x] Annex B (MVP floor) and Open Room levels; ASCII level format
- [x] Run recorder, autopilot with four play styles, headless simulator, Python plotting tools
- [x] 50 unit tests, including filter consistency (NEES) over Monte Carlo runs
- [x] Unity package: runner, input (both input systems), belief views, Seeker cloud, truth overlay,
      IMGUI HUD with association picker, reveal screen with scrubber, camera rig, bootstrap, editor
      menu, build script

### Next

- [ ] **Open the Unity project** in Unity 6 LTS, fix compile errors, commit the generated project
      files ([unity-integration.md](unity-integration.md))
- [ ] Play Open Room and Annex B; tune feel in the Inspector
- [ ] Decide what to do about the passive play style being strongest ([tuning.md](tuning.md))
- [ ] Visual treatment for the two beliefs beyond placeholders (the blue ellipse and red cloud are the
      game's core readability problem)
- [ ] Association picker UX: the scope doc imagines dragging an observation onto a landmark
- [ ] Audio cues for footsteps, pings and the Seeker's mode change (they carry information)
- [ ] Set up the Gitea runner and push the first playtest image to the NAS
- [ ] Playtest with 5 to 10 people against the done criteria in [game-design.md](game-design.md)

## Phase 2 preview: what changes in code

- `EkfSlam` alongside `EkfLocalizer`, with landmark positions in the state; `Episode` picks the
  filter per level.
- Levels gain "unknown" landmarks (not on the blueprint) and rearranged rooms.
- Views draw landmark ellipses from the same covariance helpers.
- The reveal screen adds map error (estimated vs true landmark positions).

See [architecture.md](architecture.md) "Extension points" for the other phases.
