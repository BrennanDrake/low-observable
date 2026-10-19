# LOW OBSERVABLE: Scope, Phases & Platforms

Companion to [concept.md](concept.md). Effort sizes below assume one developer working part-time and are rough guesses, not commitments.

---

## 1. End vision

A 2D top-down roguelite for PC and Steam Deck. You pilot a drone (and later a swarm) through a facility run by WARDEN, a hostile AI, and you see only your own belief about the world. Runs go through procedurally generated floors. WARDEN escalates its attacks on your estimator as you go deeper: drift, spoofed beacons, mirrored wings, kidnapping, rearranged rooms, and eventually its own filter hunting you.

**Systems at full scope**
- Belief representations unlocked through play: Gaussian (EKF) → particle cloud → EKF-SLAM with correlated landmarks → pose-graph SLAM with an inspectable spring map
- Sensor loadouts (lidar, camera, odometry, beacons, sonar, IMU) with distinct noise shapes and costs
- Manual and assisted data association, with loop closure you declare yourself
- A WARDEN "AI director" that picks attacks based on how you're estimating (for example, it builds mirrored rooms if you rely on a single Gaussian)
- **Two Filters** stealth layer: WARDEN's belief about you is visible, and you can manipulate it
- End-of-floor reveal of true vs. believed map and path, with consistency scoring (NEES)
- An optional codex that names the technique you just used and shows its equations
- Meta-progression, daily seeded runs, leaderboards ranked by consistency instead of speed

**Spin-offs that could share the estimation core:** TUNER (Kalman arcade game), THE SWARM (particles as RTS units), RUBBER BAND (loop-closure puzzle).

---

## 2. MVP: Evade the Seeker

*Revised 2026-10-17 at Brennan's request: the MVP is now about evading capture by a rogue AI agent.*

**The question it answers:** is it fun to escape a hunter when you're unsure where you are and it's unsure where you are, and do players end up reasoning about both beliefs?

### Premise
You're a small drone on one floor of the facility. A WARDEN hunter unit, **the Seeker**, is tracking you. Reach the uplink at the far end before it catches you. You navigate by your own belief, and the Seeker hunts you by its belief about you. Neither of you sees the truth.

### Two beliefs on screen
- **Yours (blue):** an uncertainty ellipse around your drone (EKF localization). It grows as you move and shrinks when you sense.
- **The Seeker's (red):** a particle cloud showing where it thinks you are. It grows while it hears nothing and collapses toward you whenever it detects you. Showing it openly in the MVP keeps the hunter readable; later phases can hide it behind a tool.
- **Capture:** the Seeker reaches your *true* position. **Win:** you dock at the uplink, which needs your own belief tight enough to line up.

### The central tension
The things that hide you also blind you, and the things that help you navigate also give you away:
- **Moving fast** gets you away, but it makes noise the Seeker can hear and makes your odometry drift more.
- **Active pinging** (range and bearing to landmarks) shrinks your ellipse sharply, but it broadcasts your position and collapses the red cloud onto you.
- **Repeating, symmetric corridors** confuse the Seeker's filter (its cloud splits), and they confuse yours in the same way.
- **Dark rooms** hide you from the Seeker's camera, and they make your own camera useless.

Every choice of where to go, what to use and how to move is a trade between your uncertainty and its uncertainty. That trade is the lesson.

### Tools (MVP set)
| Tool | What it does for you | What it costs |
|---|---|---|
| Wheel odometry | Always on, free prediction | Drifts; worse when you move fast or on slick floors |
| Passive camera | Recognizes landmarks nearby when lit (you match them yourself) | Useless in the dark; you can match the wrong landmark |
| Active ping | Precise fix on several landmarks at once | The Seeker hears it and gets a strong fix on you |
| Listen | Stop and get a noisy bearing on the Seeker | You're stationary while it closes in |
| Decoy (2 charges) | Throw a device that emits your signature | It splits the red cloud, which only helps if the Seeker can't tell them apart |

### Movement modes
- **Sprint:** fast, loud, high drift
- **Walk:** balanced
- **Creep:** silent, low drift, slow

### What players learn without being told
1. Prediction grows uncertainty; observation shrinks it (from watching both clouds)
2. Different sensors give differently shaped information (ping vs. camera vs. listening)
3. The quality of an estimate depends on what can be observed (corridors, darkness)
4. An estimator can be fooled by indistinguishable signals (decoys, wrong landmark matches)
5. Both sides are solving the same problem, which is the "Two Filters" idea, now the core of the MVP

### In scope
- One hand-built floor of 6–10 rooms, top-down 2D, with lit and dark areas and one symmetric wing
- A known but imperfect floor plan (an old blueprint), so the MVP is localization, not full mapping. Mapping unknown or rearranged space moves to phase 2.
- Hidden ground-truth simulation underneath both filters
- Your EKF, the Seeker's particle filter, the five tools and three movement modes above
- One Seeker with a simple policy: go to the most likely location in its cloud, and search when the cloud is spread out
- A reveal screen at the end: your true path, your estimated path, and the Seeker's belief about you over time, replayable with a scrubber
- Placeholder art plus a deliberate visual treatment for the two beliefs

**Out of scope for the MVP:** building a map from scratch, loop closure, multiple Seekers, procedural floors, upgrades, the codex, audio polish.

**Done when:** 5–10 playtesters who don't know estimation theory can (a) escape at least once in a few tries, (b) explain a capture afterward in terms of what the Seeker knew ("I pinged and it locked on"), and (c) choose sensors and paths on purpose by their second or third try. If (b) fails, the mechanic isn't teaching; if players won't retry, it isn't fun.

**Rough size:** 6–10 weeks part-time. The second filter adds work, but dropping mapping roughly cancels it out.

**Optional pre-MVP spike (1–2 weeks):** one room, no walls, just you, the Seeker and the ping and creep choices. It tests whether the two-belief tension feels good before building a level.

---

## 3. Phases after the MVP

| Phase | Goal | Adds | Exit test |
|---|---|---|---|
| **0. Spike** (optional) | Prove the two-belief tension feels good | One open room, you vs. the Seeker; estimation core library | Players make deliberate ping-or-creep choices |
| **1. MVP** | Prove evasion teaches and is fun | See section 2 | Playtest criteria above |
| **2. Unknown territory** | Teach mapping (the "M" in SLAM) | Unmapped or rearranged rooms; EKF-SLAM with landmark ellipses; loop closure from revisiting places | Players notice that revisiting known places tightens the map |
| **3. When one blob fails** | Teach multimodal beliefs for yourself | Particle filter upgrade for your own belief; resample and inject controls; kidnapping event; mirrored wing | Players who switch to particles survive the mirrored wing more often |
| **4. Roguelite structure** | Replayability | Procedural floors; runs and meta-progression; tool loadouts; WARDEN director (spoofed beacons, swapped landmarks, sensor flooding); hiding the Seeker's belief behind a counter-intel tool | Players do 3+ runs unprompted |
| **5. Smarter hunters** | Deepen the adversary | Multiple coordinating Seekers that share a belief; Seekers that set traps in places that give you good readings; pose-graph SLAM with false-closure traps; final boss | Players exploit what a Seeker can and can't observe |
| **6. Content & launch** | Ship | Art, audio, codex, tutorialization, settings, accessibility, Steam page, demo for Steam Next Fest | Demo wishlist and retention numbers |
| **7. Post-launch** | Extend | Daily seeds, leaderboards, SWARM / RUBBER BAND modes, educator mode (custom noise parameters, exported runs) | — |

Each phase should end in something playable. If a phase's exit test fails, fix that before moving on, since every later phase builds on the earlier one being understood.

---

## 4. Engine

**Decision (Brennan, 2026-10-17): Unity.** Keep the estimation core as a plain C# library so it can be built and unit-tested with `dotnet test` outside Unity (including by Claude Code in a cloud session), and set Unity to Force Text serialization with Git LFS for binary assets.

### Original recommendation: **Godot 4 (C#)**, or **Unity** if you already know it

| | Godot 4 | Unity | Unreal 5 |
|---|---|---|---|
| 2D top-down fit | Excellent; 2D is first-class | Very good | Weak; built for 3D, 2D (Paper2D) is barely maintained |
| Math-heavy custom code | C# or GDScript; easy to drop in a pure C# estimation library | C#; same benefit, more mature tooling | C++ or Blueprints; slower iteration for algorithm work |
| Custom belief rendering (fog, ellipses, particle clouds) | Shaders, 2D lights, MultiMesh for thousands of particles | Shader Graph, VFX Graph | Niagara is powerful but overkill here |
| Web build for sharing playtests | Yes (C# web export still limited; GDScript exports fine) | Yes (WebGL) | No practical option |
| Cost / licensing | Free, MIT | Free under the revenue threshold, then paid; history of pricing changes | Free until $1M, then 5% royalty |
| Iteration speed solo | Fast; small editor, quick launches | Medium | Slow; heavy editor and builds |
| Console ports later | Through third-party porting partners | Easiest of the three | Strong |

**Why not Unreal:** the game is 2D and the hard part is custom math and readable abstract visuals, not 3D fidelity. Unreal's strengths (lighting, large 3D worlds) don't help here, and C++ slows down the experimenting the estimation work needs. It would only make sense if the end vision changed to a 3D first-person drone.

**Godot vs. Unity:** the gap is small. Godot is lighter, free with no strings, and great for 2D. Unity has more tutorials, a bigger asset store and an easier route to consoles. Pick whichever you'll be faster in; if neither, pick Godot.

**One caveat with Godot C#:** web export is still limited for C#. If browser playtests matter early, either write the MVP in GDScript or do the TUNER spike as a plain TypeScript + canvas page.

### Architecture that keeps the engine choice reversible

- **Estimation core as a plain C# library** with no engine dependency: motion models, sensor models, EKF, particle filter, EKF-SLAM, pose graph, consistency metrics. Unit-test it against known results (for example, NEES staying inside its chi-squared bounds on a simulated run).
- **Two strictly separate worlds:** the hidden ground-truth simulation, and the belief the player sees. The renderer only reads belief; truth leaks only through explicit game events (collisions, doors, the reveal).
- **Seeded randomness everywhere** so runs are replayable for the reveal screen, bug reports and daily seeds.
- **Record every run** (inputs, true states, estimates) so the reveal screen and later analytics come free.

---

## 5. Platforms

| Platform | Fit | When |
|---|---|---|
| **PC (Steam)** | Primary. Mouse makes data association (dragging observations) natural; Steam supports demos and playtests | MVP through launch |
| **itch.io / web build** | Best for early playtests and for sharing with robotics or learning communities | MVP and spike |
| **Steam Deck** | Good fit for the genre; needs controller-friendly association (snap targets, radial menu) | Verify in phase 6 |
| **macOS / Linux** | Nearly free with Godot or Unity | At launch |
| **Switch / consoles** | Possible later; text and readability on small screens are the challenge | Post-launch, if it sells |
| **Mobile** | Poor fit for the full game (precision input, information density). TUNER could work well as a standalone mobile game | Spin-off only |
| **Education** | University robotics courses would love an "educator mode" with custom parameters and exported runs | Post-launch |

---

## 6. Decisions to make

1. **Engine:** Godot (recommended) or Unity, depending on what you already know
2. **Whether to do the one-room spike first**, or go straight to the MVP floor
3. **Solo or with collaborators**, which mostly changes how ambitious phase 6 art can be
