# Concept 01: State Estimation & SLAM

*Title: **LOW OBSERVABLE** (renamed from the working title DEAD RECKONING on 2026-10-19)*

## The one rule that makes it a game, not a lesson

**The player never sees the truth. The screen shows only the robot's belief.**

Everything in the game follows from that. You don't see the map, you see *your estimate* of the map. You don't see your robot, you see a cloud or an ellipse of where you might be. The world is real and simulated underneath, and it only shows itself at a few moments (a door that is or isn't there, a wall you hit, an extraction point you reach or miss). When the real world disagrees with your belief, that's the punishment. No quiz, no grade, no explainer popup.

If the player loses because they trusted a bad estimate, they've learned the thing the textbook says. They've also felt *why* it matters.

## Framing: an AI that attacks your estimator

A sentient facility AI ("WARDEN") has taken over an automated research complex. You're a small maintenance drone (later a swarm) sent in to reach its core. WARDEN can't easily destroy you, but it controls the building's sensors, lights, doors and radio. So it fights you **by corrupting your inference**:

| WARDEN's attack | Real estimation failure it teaches |
|---|---|
| Jams your odometry, makes floors slippery | Process noise / drift; why prediction grows uncertainty |
| Plants fake beacons | Outliers; gating; Mahalanobis distance |
| Builds mirrored, repeating corridors | Multimodal belief; perceptual aliasing; why a single Gaussian (Kalman) fails |
| Physically picks you up and drops you elsewhere | Kidnapped robot problem; particle depletion; random particle injection |
| Swaps two identical-looking landmarks | Data association errors; joint compatibility |
| Rearranges rooms mid-run | Static-world assumption; map updates vs. map rejection |
| Floods a sensor with confident garbage | Overconfident R; filter divergence |
| Watches *you*, with its own filter | Observability; information gain; deception |

The fiction isn't decoration. Each enemy behavior is a known failure mode of real estimators, so the "boss fights" are the curriculum.

---

## Core loop (the main concept)

Real-time-with-pause, top-down, roguelite runs through procedurally generated facility floors.

**1. Move = predict.** When you move, your belief smears outward along your motion. Bigger moves, faster moves, and turning on slippery tiles smear it more. That's the motion model and process noise Q, felt as "the longer I go blind, the less I know where I am."

**2. Sense = update, and sensing costs something.** You have several sensors with different shapes of uncertainty:
- **Lidar ping:** precise range, poor at telling identical walls apart. Loud: WARDEN hears it.
- **Camera:** recognizes landmarks (good for association), but noisy range and useless in the dark.
- **Wheel odometry:** free and always on, but drifts.
- **Radio beacon triangulation:** global fix, but WARDEN can spoof beacons.

Each ping collapses your belief in a characteristic way (a range sensor squashes it into a ring, a bearing sensor into a wedge). Players learn by looking that **the posterior is the overlap of prior and likelihood**, and that two weak sensors with different shapes beat one strong one. Sensing makes noise or uses energy, so *when* and *which* to sense is the real decision. That's active sensing / information gain, without the words.

**3. Associate.** When the camera sees "a red valve," the game doesn't tell you which red valve. You drag the observation onto the landmark in your map you think it is, or mark it as new. Wrong guesses quietly corrupt your map, and you find out later. Higher difficulty adds auto-association you can override, so the player learns to distrust it exactly when ambiguity is high.

**4. Close the loop.** When you think you're back somewhere you've been, you can declare a **loop closure**. The map visibly snaps: accumulated drift is redistributed along your whole path like a rubber band pulled tight. A correct closure makes everything sharpen. A false one (thanks to WARDEN's mirrored corridors) warps the whole map into a plausible but wrong shape, and you might not know until a door isn't where the map says.

**5. Act on the belief.** Objectives need certainty thresholds. A door hack only works if you're within 0.5 m of the panel, so you need your ellipse that tight before you commit. An extraction pad needs the map correct, not just your pose. Moving through a laser grid needs you to know where *you* are better than a threshold. This is where risk tolerance shows up: do you spend a loud lidar ping to tighten up, or gamble?

**Scoring:** at the end of each floor, the game reveals the true map overlaid on yours, with your true path versus your estimated path. That reveal screen is the "aha" moment; it's where players see their drift, their bad association, their phantom loop closure. Score is based on NEES-style consistency (were you *as* uncertain as you should have been?), not just accuracy, so overconfidence is punished as much as being wrong.

### Representation as an unlockable, not a setting

You start with a **single Gaussian** belief (an ellipse, a Kalman filter). It's clean, cheap, and works great in open rooms. Then WARDEN builds a mirrored wing, and the ellipse confidently picks the wrong side. The player loses.

The next upgrade is a **particle cloud**. Now the belief can split into two blobs. But particles cost compute (a run resource), and the player has to manage them:
- **Resample** button: kill unlikely particles, clone likely ones. Do it too often and diversity collapses; your cloud becomes one tight wrong point (particle degeneracy).
- **Inject** button: sprinkle random particles across the map. Costs compute, but it's the only thing that saves you after a kidnapping.
- Particle count slider: more particles = better belief, slower recharge.

Later upgrades: a full **EKF-SLAM** mode where the landmarks themselves have ellipses that correlate with yours (seeing one landmark tightens the others: the covariance matrix made visible through linked ellipses), and **graph-SLAM**, where your map is a spring network you can inspect, remove bad edges from, and re-relax.

Players earn the vocabulary after the fact. The upgrade is called "Splitter" in play; the codex entry unlocked afterward says "you just used a particle filter" with the real equations, for people who want them.

---

## Alternative concepts

These share the same idea (the belief is the interface) but stress different parts of the math. Any could be a mode inside LOW OBSERVABLE or its own game.

### A. TWO FILTERS (asymmetric stealth)

WARDEN is running its own particle filter on *your* position, and you can see its belief about you: a red cloud on your screen. Your goal isn't to be invisible, it's to be **ambiguous**. Make noise in one corridor and slip into another so its cloud splits. Move in a way its motion model doesn't expect so its prediction drifts away from you. Stay in symmetric areas where its observations can't discriminate.

You're playing the estimation problem from the other side, which teaches observability and information gain more intuitively than anything else: you learn which measurements are informative by trying to deny them. Late game, you both chase and hide, juggling your filter on WARDEN's mobile units and its filter on you.

### B. THE SWARM (you are the filter)

A tactics/RTS game where your units *are* particles. You command 200 cheap probe drones scattered through an unknown space; each one is a hypothesis about where a hidden enemy core is. Sensor reports come in and drones in unlikely positions fade out. You choose when to resample (pull drones toward the strong ones), when to keep scouts spread, and when to commit everything to an attack.

This makes resampling, degeneracy and exploration vs. exploitation into army-management decisions. It's the most "game-y" of the set and probably the most fun to watch.

### C. TUNER (Kalman as a rhythm/control game)

A tight arcade game. You're tracking a fast-moving WARDEN drone with a turret, seeing only noisy radar blips. Two knobs: **how much you trust your prediction (Q)** and **how much you trust the sensor (R)**. Your aim point is the filter's output. WARDEN keeps changing the game: smooth flight (trust the model), sudden jinks (raise Q), sensor flooding (raise R). Get the ratio right and the reticle locks on; wrong, and it lags or jitters.

Small scope, great as a first prototype, and it teaches the Kalman gain as a feeling: "trust the blip more right now."

### D. RUBBER BAND (loop closure puzzle)

A slower puzzle game built only on graph-SLAM. You're given a robot's drifting trajectory as a spring-connected chain and a pile of candidate "I've been here before" matches, some real and some planted. You pick which constraints to add, and the map relaxes in real time with physics. The puzzle is figuring out which closures are consistent with each other. Teaches pose-graph optimization, robust kernels, and the danger of a single bad closure.

---

## Learning arc (what a run teaches, in order)

1. Moving without sensing makes you less sure (prediction, process noise)
2. Sensing makes you more sure, in a shape that depends on the sensor (likelihood, Bayes update)
3. Combining different sensors beats one great one (sensor fusion)
4. Uncertainty can be lopsided and correlated (covariance)
5. One blob isn't always enough (multimodal beliefs, particle filters)
6. Knowing *which* thing you saw is half the problem (data association)
7. Errors accumulate and returning to known places fixes them (loop closure)
8. Being confidently wrong is worse than being honestly unsure (consistency)
9. An adversary can exploit every one of these assumptions (robust estimation)

Each step corresponds to a WARDEN escalation, so difficulty rises along the curriculum rather than just enemy HP.

## Why this should actually be fun

- **Tension from information, not reflexes.** The most stressful moment is deciding whether to ping and get caught, or keep going half-blind.
- **The reveal screen.** Seeing true vs. believed paths at the end of a floor is inherently satisfying, like a replay in a racing game, and it's where learning gets cemented.
- **Betrayal by your own map.** A false loop closure that warps everything is a great dramatic beat, and the player caused it.
- **Readable emergent stories.** "I thought I was in the east wing, my cloud split, I injected particles, and I was actually upstairs." That's a story players tell friends, and it's also a correct description of a particle filter recovering.

## Risks

- **Abstraction overload.** Showing ellipses, clouds and correlated landmarks at once could be unreadable. Mitigation: introduce one layer per floor, and keep strong art direction (belief rendered as fog/light, not math plots).
- **Feels unfair if the truth is hidden too long.** Need frequent small "ground truth" moments (bumping a wall, a door opening) so players get feedback inside a floor, not just at the end.
- **Players may brute-force with maximum sensing.** Sensing has to cost something real (detection by WARDEN, energy) or the decision disappears.

## Smallest prototype worth building

**TUNER** first (a week of work in a browser, it validates whether Kalman intuition can feel good), then a single LOW OBSERVABLE floor with: odometry drift, one range sensor, a Gaussian belief, a noise cost to pinging, and the end-of-floor true-vs-believed reveal. If the reveal screen is satisfying, the concept works.

## Open questions for Brennan

1. Which part of SLAM did you personally struggle with most? (e.g. covariance intuition, data association, why particle filters work, graph optimization) That should be the game's centerpiece.
2. Solo-dev scale (browser prototype, 2D) or a bigger dream game? This changes which concept leads.
3. Should the real math ever surface in-game (an optional codex with equations), or stay entirely implicit?
