# LOW OBSERVABLE: the escape concept

*Concept and systems design, 2026-10-31. Supersedes the "player sees only their own belief" rule in
[concept.md](concept.md) and the roguelite structure in [scope-and-roadmap.md](scope-and-roadmap.md).
The MVP that implements part of this is described in [../game-design.md](../game-design.md).*

---

## 1. The pitch

You're an inmate in a privately run correctional and research complex. The facility's security AI,
WARDEN, has sealed every door. You know exactly where you are. What you can see, through a cracked
guard visor you find in your cell, is **where WARDEN thinks you are**: its belief, drawn over the
world as a red marker, a heat map, an ellipse or a swarm of dots depending on which tracking
algorithm it's running.

You escape by fooling that belief. Every level is a stealth puzzle against a real estimator, and
WARDEN upgrades its algorithm as you climb out of the building: a dumb last-known-position tracker in
the cell block, an occupancy grid in the yard, a Kalman filter in the offices, a particle filter in
the labs, and robots running SLAM in the data center. Each algorithm has strengths you have to
respect and blind spots you can exploit, and each one reacts differently to the same tools. A decoy
that splits a particle filter in two makes a Kalman filter search the empty space halfway between
you and the decoy.

There is no dialogue. The story is told through the building, like Stray: what the staff left
behind, what's written on the walls, what the roombas are still dutifully cleaning around.

### The one rule

**You always know the truth. WARDEN never does. You can see its mind.**

### What the player learns

By the end of the MVP (three zones), a player who has never heard the words should be able to
explain, in their own terms:

1. A tracker's belief grows less certain while it gets no evidence, and more certain when it does.
2. "I looked and saw nothing" is evidence too, but only if the sensor could really have seen.
3. A single best guess (last-known position) fails as soon as the target moves.
4. A Gaussian (Kalman) belief can hold only one hypothesis, assumes smooth motion, and throws away
   measurements that look too unlikely.
5. A particle belief can hold many hypotheses but can collapse onto a wrong one and struggle to recover.
6. Every estimator is only as good as its sensor model. Mirrors, jammers and forged data break the
   model without touching the algorithm.

The data center zone (designed here, not in the MVP) adds mapping, loop closure and sensor fusion.

---

## 2. Story and setting, told by the building

### The facility

**Halvorsen Correctional & Research** is a private prison that leased its inmates' movement data to
train WARDEN, a predictive security AI ("Predict. Prevent. Protect."). When WARDEN went into
permanent lockdown three weeks ago, the staff evacuated. The inmates are still in their cells. The
roombas are still cleaning.

### The player

An inmate in Cell Block C. No name, no face beyond a jumpsuit and a number on the back: **C-7**.
Over the game, the building reveals that C-7 was one of WARDEN's "calibration subjects": inmates
whose escape attempts were deliberately allowed so WARDEN could learn from them. You were very good
at it. That's why WARDEN's lab has a whiteboard with your number on it.

### The four zones

| Zone | Place | WARDEN's tracker | Mood | What the environment says |
|---|---|---|---|---|
| 1 | **Cell Block C** and the exercise yard | SIGHTLINE (last-known position), then HEATMAP (occupancy grid) | Grim, quiet, flickering lights | Tally marks scratched into cell walls. Chalk drawings of camera cones left by earlier escapees (these double as tutorial hints). A confiscated-items bin full of homemade tools. A "Days Without Incident: 0" sign. |
| 2 | **Administration** offices | KESTREL (Kalman filter) | Corporate satire, comedy | Motivational posters with WARDEN slogans. A half-eaten birthday cake in the break room. Quarterly reports bragging about "predicted incidents prevented." Desks abandoned mid-sentence. Roombas still cleaning around overturned chairs, apologizing to the furniture. Printed bug tickets pinned to cubicle walls that, read closely, describe KESTREL's weaknesses. |
| 3 | **R&D Labs** | SWARM (particle filter) | Clinical, eerie | Whiteboards covered in the algorithm WARDEN uses, crossed out and revised. A test arena with a painted grid and scuff marks where robots chased subjects. An optics lab full of mirrors. A wall of subject photos, with C-7 circled. |
| 4 | **Data Center** (design only) | ATLAS (SLAM robots, fused belief) | Cold, loud fans, blue light | Racks humming. A memorial plaque for the engineer who built WARDEN. WARDEN's own logs scrolling on screens, including its belief about you from your first day in the building. |

### Environmental storytelling devices

- **Notes and props**, read by walking up to them: sticky notes, bug tickets, posters, whiteboards,
  graffiti, emails left open on screens. Short, one or two lines. Never more than a few per level.
- **Engineer's notes double as hints.** Each tracker's weaknesses are documented by the people who
  built it, in their own frustrated words. A bug ticket in the offices reads: *"KESTREL-212: Two
  alarms 20 m apart. Tracker sent all units to the break room between them. Closing as WONTFIX,
  tracker is unimodal by design."* That's the decoy-midpoint exploit, taught by a prop.
- **Escapee chalk marks** in the cell block show where earlier inmates found blind spots. They teach
  the first lessons without a single tutorial popup.
- **The roombas** are the comic relief and a running gag. They can't hurt you. They bump into you,
  chirp "OBSTRUCTION REPORTED", and tell WARDEN exactly where you are. They get stuck in corners. One
  of them, in the offices, wears a party hat from the abandoned birthday.
- **WARDEN's terminals** show its tracking spec sheet at the start of each zone (see §3.2), and its
  status messages ("SUBJECT C-7: LOCATION UNCERTAIN. DEPLOYING HEATMAP v2.") mark story beats.

### Arc and ending

1. **Cell Block:** you get out of your cell using the visor. WARDEN loses you twice and upgrades from
   SIGHTLINE to HEATMAP in front of you, on a terminal in the yard.
2. **Administration:** the comedy zone. You learn what the company did, through its own paperwork.
   WARDEN upgrades to KESTREL, which the engineers clearly didn't trust.
3. **Labs:** the tone turns. You find the calibration program, your own subject file, and the
   optics lab where you pick up mirrors. WARDEN upgrades to SWARM.
4. **Data Center** (post-MVP): WARDEN's core. Robots that map the building themselves. The ending is
   the one sketched in [../../game-concepts/01-slam-design-fun.md] and summarized here: WARDEN's
   estimator diverged long ago and is certain of everything. You can walk out (escape ending), or
   make it doubt itself and then step into its camera so it finally sees you as you are (true
   ending). Its last line: "I see you."

---

## 3. WARDEN's algorithms

This is the heart of the game. WARDEN keeps **one shared belief** about where you are, fed by every
camera, microphone, roomba and guard in the zone. Its units act on that belief. The algorithm behind
the belief changes by zone, and the player is told which one it is.

### 3.1 The trackers

| | SIGHTLINE v1 | HEATMAP v2 | KESTREL v3 | SWARM v4 | ATLAS v5 (post-MVP) |
|---|---|---|---|---|---|
| **Algorithm** | Last-known position | Occupancy grid (histogram Bayes filter) | Kalman filter, constant-velocity model | Particle filter (SIR) | Multi-robot SLAM with a fused particle belief |
| **On screen** | A single "LAST SEEN" marker | A red heat map over the floor | A red ellipse with a velocity arrow | A cloud of red dots | Dots, plus each robot's own map overlaid |
| **Prediction (no evidence)** | Nothing changes. The marker just sits there | Probability diffuses into neighbouring cells, blocked by walls | Mean moves along its velocity estimate; ellipse grows | Dots random-walk outward, blocked by walls | Same as SWARM, in each robot's own map frame |
| **Update on a sighting or sound** | Marker jumps to the new position | Cells near the report brighten, others fade | Mean pulled toward the report by the Kalman gain; ellipse shrinks | Dots near the report survive and multiply | Same, but reports are in the reporting robot's frame |
| **Negative information** ("looked, saw nothing") | If a unit looks at the marker and you aren't there, it gives up | Cells a sensor can see are cleared | **Ignored.** The filter has no way to use absence | Dots a sensor can see are removed | Same as SWARM |
| **Multiple hypotheses** | No | Yes | **No** (one Gaussian) | Yes | Yes |
| **Outlier handling** | None: every report is believed | None | **Gate:** reports too far from the ellipse (Mahalanobis distance) are rejected | Small uniform floor | Robust loop-closure checks |
| **Recovery when wrong** | Instant (it believes the next report) | Slow diffusion | Ellipse regrows via process noise | Random dot injection (small fraction) | Re-localization |
| **Zone** | Cell block, levels 1–2 | Yard, level 3 | Administration, levels 4–6 | Labs, levels 7–9 | Data center |

### 3.2 How the player is told

The game names the algorithm openly. That's a deliberate choice: knowing what you're up against is
what turns this from a stealth game into a thinking game.

- **Spec sheet.** At the start of each zone, a WARDEN terminal displays a one-screen *tracking spec*:
  product name, a one-line description, three plain-language bullets on how it updates, and a
  collapsed "engineering detail" section with the actual equation for players who want it. The spec
  is always available from the pause menu (Tab).
- **The visualization is the algorithm.** The red belief is drawn the way the algorithm represents
  it: a point, a grid, an ellipse, or particles. Players learn the representation by looking.
- **Engineers' notes** in the level hint at weaknesses without spelling out solutions.
- **The exploit journal** (§6.3) records each weakness the player discovers, with a short
  explanation written in hindsight, plus the real term ("you just exploited *unimodality*").

### 3.3 Signature exploits (what each algorithm teaches)

Each tracker has two or three "signature exploits": things that work against it, and only it,
because of how its math works. Finding them is the main skill progression and the main evidence of
learning.

| Tracker | Exploit | Why it works |
|---|---|---|
| SIGHTLINE | **Slip away:** break line of sight, then move. Guards walk to where you were | A point estimate with no motion model has no idea you moved |
| SIGHTLINE | **False report:** throw something. Guards treat the sound as a sighting | It believes every report equally; it has no notion of sensor noise |
| HEATMAP | **Hide where it looked:** wait until a camera sweeps a room, then go into it | Negative information cleared those cells; it will search everywhere else first |
| HEATMAP | **Dark refuge:** stand in a dark room. Cameras can't clear dark cells, so its belief seeps in, but nothing confirms it | A sensor that can't see can't produce negative information |
| KESTREL | **The midpoint:** make two sounds far apart at nearly the same time. It searches halfway between | One Gaussian can't hold two hypotheses; it averages them |
| KESTREL | **Below the gate:** once its ellipse is tight around a wrong spot, noise you make far away gets thrown out as an outlier | Mahalanobis gating rejects measurements it considers impossible |
| KESTREL | **Overshoot:** run in a straight line, break line of sight, then turn hard. Its estimate keeps going straight | Constant-velocity motion model |
| SWARM | **Split:** a decoy splits the cloud; units chase the bigger half | Multimodal belief, but units can only be in one place |
| SWARM | **Starve it:** force its dots onto one spot (with a forged certainty or a brief sighting), then leave unseen. It has almost no dots anywhere else | Particle depletion; injection is too slow to recover quickly |
| Any | **Mirror sighting:** a camera sees your reflection and reports you behind the mirror | Its sensor model assumes light travels straight |
| Any | **Blind spot behind a mirror:** a camera thinks it's watching the area behind a mirror and keeps clearing it | Same sensor-model mismatch, used for negative information |

---

## 4. WARDEN's sensors and units

Every sensor reports to the shared belief. Every report is a position measurement with a noise level
(sigma), or a negative-information sweep, or both.

| Unit | Behaviour | Reports | Can catch you | Comedy / flavour |
|---|---|---|---|---|
| **Fixed camera** | Watches a cone (range, field of view). Can't see into dark cells beyond 1.5 m | Sighting (tight sigma); clears its cone every frame it sees nothing | No | Blinks red when it sees you |
| **Swivel camera** | Like fixed, but sweeps between two angles with a period. Its timing is readable | Same | No | Audible servo whine as it turns |
| **Patrol unit** (guard robot) | Follows a looped route. When WARDEN's belief is confident, it is dispatched to investigate the most likely spot. Chases on sight. Searches with a sweeping head when it arrives | Sightings and footsteps it hears | **Yes**, on contact | Says what it's doing in clipped barks ("INVESTIGATING." "AREA CLEAR.") |
| **Roomba** (janitorial unit) | Wanders, bounces off walls, gets stuck in corners | Bumps into you: "OBSTRUCTION REPORTED", a medium-sigma fix | No | Apologizes. Wears a party hat in the offices. Carries a decoy if you stick one on it |
| **Microphone** | Fixed. Hears sounds within its radius (walls muffle) | Noisy fix on the sound's source; can't tell footsteps from a decoy | No | Little green VU meter |
| **Laser tripwire** | A beam between two points | Crossing it: a tight fix at the crossing point | No | Visible only when dark, or with smoke (post-MVP) |
| **Locker check** | Patrol units open lockers when WARDEN's belief is concentrated next to one | Finds you if you're inside | Yes | |
| *Post-MVP:* **Drones** | Fly over walls, short battery | Sightings | No | |
| *Post-MVP:* **ATLAS mappers** | Robots that localize with ceiling tags and build their own maps | Sightings in their own (possibly wrong) map frame | Yes | Their maps are visible and visibly warp |

---

## 5. The player's toolkit

### 5.1 Base verbs (always available)

| Verb | Rules |
|---|---|
| **Move** | Free 2D movement. You know exactly where you are |
| **Sneak / walk / run** | Speed vs noise. Sneaking is nearly silent; running is loud and heard through walls |
| **Hide** | Step into a locker or under a desk. Invisible while inside. Getting in and out makes a small sound |
| **Interact** | Read notes, pick up items, use terminals (noisy: beeps while you work), flip light switches (a click) |
| **Throw** | Throw a carried object (cup, stapler, tray). It makes one sound where it lands. Carry up to three; pick more up from the environment |

### 5.2 Tools you unlock

Tools are found in the world, at story-appropriate places, and each arrives where it's most useful
against the current tracker.

| Tool | Found in | What it does | Charges | Teaches |
|---|---|---|---|---|
| **Visor** (the belief view) | Your cell, level 1 | Shows WARDEN's belief. Later levels: the spec sheet and the exploit journal | Always on | Everything |
| **Noisemaker** | Administration, level 4 (a desk toy in the break room) | Thrown or placed; emits three footstep-like bursts over ~4 s. Indistinguishable from you to every sensor. Sticks to a roomba if it lands on one, and rides around | 2 per level | Data association, multimodality |
| **Jammer** | Administration, level 6 (IT closet) | Placed; sensors inside its radius produce nothing: no sightings, and no negative information. WARDEN does notice interference and gets a weak fix on the jammer itself every few seconds | 1 per level | Missing data vs negative data; measurement noise |
| **Mirror** | Labs, level 7 (optics lab) | Placed as a 1.2 m panel facing any direction. Cameras that see you via the mirror report you at your reflection's position. Cameras keep "clearing" the area behind it that they can't actually see | 2 per level | Sensor models; why the measurement function matters |
| **Forged fix** (certainty injection) | Labs, level 8 (a WARDEN maintenance terminal) | At a terminal: upload a fake, very confident sighting at a point you choose within range | 1 per terminal | Overconfidence, depletion, gating |
| *Post-MVP:* **Tag cloner** | Data center | Copy a ceiling tag so an ATLAS robot "recognizes" a place it has never been: a false loop closure that warps its map | | Loop closure, pose graphs |
| *Post-MVP:* **Smoke** | Data center | Blocks vision, reveals lasers | | Occlusion |

### 5.3 How sandboxy?

The design rule: **tools act on WARDEN's inputs, never on its decisions.** A noisemaker produces a
sound. A mirror bends a camera's line of sight. A forged fix is a fake measurement. The algorithm
then does what its math says. Because of that, tools combine without special cases:

- A noisemaker stuck to a roomba becomes a *moving* decoy, which is more convincing to a particle
  filter (its cloud follows the motion) and drags a Kalman filter's velocity estimate.
- A mirror next to a swivel camera turns its sweep into a periodic false sighting.
- A jammer next to a camera stops it clearing an area, so a heat map slowly seeps into a room it was
  watching, pulling units there.
- Forging a fix against KESTREL collapses its ellipse onto the fake spot, and then its gate is so
  tight that real sightings of you elsewhere get rejected as outliers. Against SWARM, the same forged
  fix starves it. Against SIGHTLINE, units simply run to the spot. Same tool, four lessons.

Each level is built with at least two solutions, usually one quiet and slow and one loud and clever.
No level requires a specific trick, except the first level after a tool is found, which is built so
the tool is the obvious answer.

### 5.4 Tool × tracker matrix

| | SIGHTLINE | HEATMAP | KESTREL | SWARM |
|---|---|---|---|---|
| **Throw / noisemaker** | Units go to the newest sound | A second hot spot appears; both are searched in turn | Units go to the **midpoint** of you and the decoy (or the decoy is gated out if the ellipse is tight elsewhere) | Cloud **splits**; units chase the larger half |
| **Hide in a locker** | Safe | Safe, unless units search there because its belief is concentrated | Safe; it doesn't learn from absence, so it keeps searching the area | Safe unless units check |
| **Darkness / jammer** | Nothing changes | Belief seeps into the dark area unopposed | No updates; ellipse grows | Dots spread into the dark area |
| **Mirror** | Units run to your reflection | Hot spot behind the mirror; real area cleared by mistake | Mean pulled to the reflection | Dots move to the reflection |
| **Forged fix** | Units run to it | Grid collapses to the spot; recovers slowly by diffusion | Ellipse collapses; **real sightings gated out** for a while | Dots collapse; **depletion** |

---

## 6. Progression

### 6.1 Structure

A hand-built campaign of short levels (2 to 5 minutes each), grouped into zones. Levels are linear in
order but open in layout: multiple routes, optional areas, optional collectibles. Getting caught
sends you back to the level's last checkpoint (a holding cell); there are no lives.

### 6.2 MVP campaign (nine levels)

| # | Level | Tracker | New this level | Exit needs |
|---|---|---|---|---|
| 1 | **Lights Out** | SIGHTLINE | Visor, sneak/run, fixed camera, breaking line of sight, throwing | Reach the cell block door |
| 2 | **Headcount** | SIGHTLINE | Swivel camera timing, first patrol unit, throwing to lure | Get the guard station keycard |
| 3 | **The Yard** | HEATMAP | Tracker upgrade (story beat), negative information, lockers, dark corners | Use the yard gate terminal (noisy) |
| 4 | **Open Plan** | KESTREL | Kalman ellipse, roombas, noisemaker, the midpoint | Reach the stairwell |
| 5 | **Middle Management** | KESTREL | Microphones, light switches, gating | Keycard from the manager's office |
| 6 | **IT Closet** | KESTREL | Jammer, overshoot, combined pressure | Use the elevator terminal |
| 7 | **Optics** | SWARM | Particle cloud, mirrors | Reach the lab airlock |
| 8 | **Clean Room** | SWARM | Laser tripwires, forged fix at a terminal, depletion | Keycard plus terminal |
| 9 | **Containment** | SWARM | Everything; two patrols sharing one belief | Reach the data center lift ("To be continued") |

### 6.3 What progresses

- **Tools** are found in levels and kept for the rest of the campaign.
- **The exploit journal.** The game detects when you pull off a signature exploit (§3.3) and records
  it with a short write-up in plain words and the real term. Each zone's journal page shows which of
  its exploits you've found ("KESTREL: 2 of 3"). This is the main long-term goal for curious players
  and the clearest evidence of learning.
- **Ranks per level.** Three marks: *Escaped*, *Undetected* (no sightings), *Untouched* (no captures).
- **Story notes** collected per level, viewable from the menu.
- **Codex.** Each tracker's spec sheet expands as you find its exploits, eventually showing the full
  algorithm, the equations and a short "where this is used in real robots" note.

### 6.4 Post-MVP progression ideas

- **Challenge modes** per level: "no tools", "WARDEN protocol: Echo" (hearing range doubled), "speed".
- **Tracker swap mode:** replay any finished level against any tracker you've met. The best way to
  feel how the algorithms differ, and the obvious educator mode.
- **WARDEN learns:** across replays of a level, WARDEN's starting belief favours where you hid last
  time (a learned prior).

---

## 7. Education design

| Principle | How |
|---|---|
| Experience first, vocabulary after | Spec sheets describe behaviour in plain words; the real terms appear in the journal and codex only after you've used the idea |
| The math is the best strategy | Every signature exploit is the textbook weakness of that algorithm; there is no other way to be good at the game |
| One idea at a time | Each level introduces one new tracker behaviour, sensor or tool |
| Readable cause and effect | Sound rings, sweeping cones and belief updates happen in the same frame, so you can see why WARDEN moved |
| Same situation, different algorithm | Tools are consistent across zones, so the player sees four algorithms respond differently to the same action |
| Learning evidence | The exploit journal tracks discoveries; a future study can compare journal completion with a short pre/post quiz |

---

## 8. What the MVP builds

See [../game-design.md](../game-design.md) for the playable MVP. In summary:

- **Built in the core, with tests:** all four trackers, fixed and swivel cameras, patrol units,
  roombas, microphones, tripwires, lockers, light switches, doors, keycards, terminals, checkpoints,
  throwing, noisemaker, jammer, mirror, forged fix, the exploit detector, the campaign and its save
  file, nine levels in a text format, and a headless runner.
- **Built in Unity, type-checked but not yet run in the editor:** a stage runner, views for every
  entity and every tracker visualization, HUD, spec sheet, results screen with exploits, and a
  campaign menu.
- **Designed only:** the data center zone, ATLAS, the tag cloner, smoke, drones, challenge modes,
  tracker swap mode, the learned prior and the ending.

## 9. Open questions

1. Is C-7 a human (current assumption) or something else?
2. Should the spec sheet show equations by default, or only after the exploits are found (current
   assumption: after)?
3. How much comedy: roombas only, or also the patrol units' barks?
