# Drone prototype (superseded by the escape campaign)

> The first MVP: you were a drone that only saw its own belief. Since 2026-10-31 the game is the escape
> campaign in [game-design.md](game-design.md), where you see the truth and WARDEN's belief. The drone
> mode still builds and runs (`make sim`) and its particle filter is reused by SWARM in the escape core.

## One rule

**The player never sees the truth.** The screen shows the drone's belief about itself and the
hunter's belief about the drone. The real world exists underneath and only shows itself through
bumps, failed docks, the Seeker appearing in your camera, and the reveal screen at the end.

## Premise

You are a maintenance drone in Annex B, a wing of a facility taken over by WARDEN, a rogue AI. One
of its hunter units, the **Seeker**, is looking for you. Reach the uplink on the far side of the
floor and dock with it before the Seeker reaches you.

You have the floor's blueprint (walls, landmarks, lighting), so the MVP is a **localization**
problem: knowing where you are on a known map. Building the map yourself comes in phase 2.

## What is on screen

| Element | What it is | The math underneath |
|---|---|---|
| Blue triangle | Where the drone believes it is, pointing where it believes it faces | EKF mean |
| Blue ellipses | 1-sigma and 2-sigma uncertainty of that position | EKF position covariance |
| Blue fan | How unsure the heading is | EKF heading variance |
| Red cloud | Where the Seeker thinks you are, one dot per hypothesis | Seeker's particle filter |
| Red triangle and cone | The Seeker, only while your camera can actually see it | Truth, perceived through your sensors |
| Blueprint | Walls, lit/dark/slick floor, landmarks coloured by appearance, the uplink | The known map |

## Your tools

| Tool | Key | Helps you | Costs you |
|---|---|---|---|
| Odometry | always on | Tracks your motion for free | Drifts; much worse sprinting and on slick floor |
| Camera | always on (2 Hz) | Recognizes landmarks nearby, so matching is easier | Useless in the dark; poor at range; identical-looking landmarks fool it |
| Ping | Space | Precise range and bearing to every landmark in sight | Loud: the Seeker hears it across most of the floor. Cooldown 1.5 s |
| Listen | F | A noisy bearing to the Seeker | You stand still for a second |
| Decoy | Q (aims at the mouse) | Broadcasts a fake ping, splitting the Seeker's belief | Two per floor. Thrown relative to where you *think* you are |
| Dock | E | Wins the floor if you're really at the uplink | A failed dock is noisy and tells you that you were wrong |

Movement modes trade speed for noise and drift: **creep** (Ctrl) is silent and precise, **walk** is
balanced, **sprint** (Shift) is fast, loud, and the wheels slip.

## The central tension

Everything that hides you also blinds you, and everything that helps you navigate gives you away:

- Dark rooms hide you from the Seeker's camera and make your own camera useless.
- The dark, slick service corridor is the shortest route, but the floor slips more than your filter
  assumes, so you have to ping, and the Seeker's hub is right next door.
- The twin labs on the north loop are hidden from the hub, but their landmarks look identical, so
  your camera can match the wrong lab's valve and your belief jumps somewhere wrong.
- Pinging fixes your position and hands the Seeker your position in the same instant.

## The Seeker

The Seeker runs a particle filter on your position and acts on it:

- **Hearing:** footsteps (radius depends on your mode), pings, failed docks and decoys give it a
  noisy fix. Walls muffle sound.
- **Seeing you:** a strong fix when you're in its cone, in range, in line of sight and lit (or very close).
- **Not seeing you:** where it looks and finds nothing, it removes hypotheses. This is why it
  sweeps its camera while searching, and why hiding in a spot it just checked works.
- **Behaviour:** searches toward the most likely area while its belief is spread out; hunts at
  speed once its belief is tight. It catches you when it reaches your *true* position.

The red cloud is shown openly in the MVP so players can learn how their noise moves it. Later
phases can hide it behind a counter-intelligence tool.

## Data association

Measurements don't say which landmark they came from. When more than one landmark could explain a
measurement, the game pauses and asks you to pick (or ignore it). This is the "real-time with pause"
part of the design. A wrong pick quietly corrupts your belief and you find out later, usually at
the dock. Set `Association` to `AutoBest` on the `EpisodeRunner` to let the filter guess instead.

## The reveal screen

When the floor ends, the camera pulls back to show where you really went (white) against where you
thought you were (blue), the Seeker's path, your error, how often the truth was inside your 95%
ellipse, and how many landmark matches were wrong. A scrubber replays the belief over time. This is
where the learning lands: the score rewards an honest belief, not just a correct one.

## What a player learns, without being told

1. Moving without sensing makes you less sure (prediction grows covariance).
2. Sensing makes you more sure, in a shape that depends on the sensor (likelihoods differ).
3. The quality of an estimate depends on what can be observed (darkness, aliasing).
4. A filter can be confidently wrong when its model is wrong (slick floors; wrong matches).
5. Indistinguishable signals split a belief (decoys against the Seeker's particle filter).
6. Absence of evidence is evidence (the Seeker clearing places it looked).
7. Both sides are solving the same problem.

## MVP done criteria

From the scope doc: 5 to 10 playtesters who don't know estimation theory can (a) escape at least
once in a few tries, (b) explain a capture in terms of what the Seeker knew ("I pinged and it
locked on"), and (c) choose sensors and routes on purpose by their second or third try.
