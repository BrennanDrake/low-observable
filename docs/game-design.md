# Game design: the escape MVP

The full concept (story, all five WARDEN models, the toolkit, post-MVP ideas) is in
[design/escape-concept.md](design/escape-concept.md). This page is what is built and playable.
The earlier drone mode is described in [drone-prototype.md](drone-prototype.md).

## One rule

**You always know the truth. WARDEN never does. You can see its mind.**

The player sees the real facility, the real guards and cameras, and themselves. Over that, in red, is
WARDEN's belief about where they are, drawn the way WARDEN's current algorithm represents it. The game
is making that belief wrong in useful ways.

## Premise

You are C-7, a "calibration subject" at Halvorsen Correctional & Research, a private prison that rents
its inmates to its own AI lab. WARDEN, the facility AI, is the lab's product; you are its test data.
Escape through the cell block, the administration offices and the research labs. No cutscenes: the
story is in notes, whiteboards, bug tickets and chalk marks left by people who tried before you.

## What is on screen

| Element | What it is |
|---|---|
| White diamond | You (grey when hidden in a locker). A stamina bar appears under you while running |
| Red belief | WARDEN's estimate of you: an X, a heat map, an ellipse with a velocity arrow, or a particle cloud |
| Red rings | A report WARDEN just took in, where it placed it. Grey rings and a grey X: a report it threw away |
| Purple and orange fans | Camera and guard vision, clipped by walls. Red when alarmed, grey when jammed |
| Orange line from a guard | Where WARDEN sent it: the consequence of its belief |
| White rings | Sounds, drawn at the radius they carry |
| Cyan circles | Roombas (some wear party hats). They report what they bump into, then apologize |
| Green dots and rings | Microphones and their hearing range |
| Red lines | Tripwires |
| HUD, top left | Level, WARDEN's model, and what it thinks right now ("it's looking elsewhere", "it knows where you are") with the belief mass on you |

## WARDEN's four models (the MVP campaign)

| Zone | Model | Algorithm | What it can't do (the lesson) |
|---|---|---|---|
| Cell Block (1-2) | SIGHTLINE v1 | Last-known position | No motion model and no noise model: it waits where you were, and believes any noise |
| Cell Block (3) | HEATMAP v2 | Occupancy grid (histogram Bayes filter) | Learns from absence, so freshly swept rooms are safe; heat spreads only as fast as it assumes you walk |
| Administration (4-6) | KESTREL v3 | Kalman filter, constant velocity | One Gaussian: two reports become one estimate in the empty middle; reports far outside the ellipse are discarded; it extrapolates straight lines |
| Research Labs (7-9) | SWARM v4 | Particle filter | Can split between hypotheses; a report that claims to be certain starves every particle that disagrees |

Before each level a briefing shows the model's spec sheet in plain words, with the textbook name and
one line of maths. Tab shows it again in play. Each model has hidden weaknesses ("exploits"); pulling
one off adds an entry to the **exploit journal** with what happened, the real term, and why it works.

## Your verbs and tools

| Input | Verb |
|---|---|
| WASD, Shift, Ctrl | Move, run (stamina), sneak (quiet) |
| E | Read, take, hide, flip a switch, take a mirror back; hold at terminals |
| 1-5, Q, right click | Pick a tool |
| Left click | Use it at the cursor (a preview shows where it lands, green if it can be used) |
| Scroll or [ ] | Rotate the mirror before placing it |
| Tab, M, R, Esc | Spec sheet, map overview, restart, pause |

| Tool | Found | What it does to WARDEN's inputs |
|---|---|---|
| Throw | Always (pick things up first) | One landing noise. A false report |
| Noisemaker | Level 4 | Four bursts as loud as running. Sticks to roombas, making a moving decoy |
| Jammer | Level 6 | Sensors inside report nothing, not even "nobody here". WARDEN gets a vague fix on the jammer |
| Mirror | Level 7 | Cameras that see you in it report your reflection; their naive coverage model also thinks it sees through it |
| Forged fix | Level 8 | At a maintenance terminal: upload one near-certain fake sighting |

Tools act on WARDEN's inputs, never on its decisions. That rule is what keeps them a sandbox: any tool
works against any model, and the same action has a different effect on each.

## Patrols and hunters

Guards come in two kinds. **Patrols** walk their rounds and only respond to trouble near them: obstacles
to read and lure. **Hunters** (drawn red, from level 5) are WARDEN's belief made physical: they go
wherever the tracker is confident you are, and when it isn't, they sweep its best guess. Their own
"nobody here" then cools that spot, so a hunter works through the belief hottest place first. That is
where the algorithm becomes the pathing: a hunter against HEATMAP clears the heat, against KESTREL it
heads for the ellipse's centre, against SWARM it follows the densest cluster.

## Progression

- **Campaign:** nine levels, unlocked in order. Each records three marks: Escaped, Unseen (never
  sighted), Untouched (never caught), plus best time against par.
- **Tools** found in a level stay for the rest of the campaign.
- **Exploit journal:** 12 entries, most tied to one model and a few (mirrors, the jammer) to any. The menu shows which you
  have found per model, and the results screen nudges you when a model still has undiscovered ones.
- **Codex:** spec sheets for every model you have met.
- **Sandbox replays:** replay a cleared level against any model you have met. Same room, different
  maths. Marks aren't recorded; new exploits still are.
- **Capture** sends you back to the last checkpoint and resets WARDEN's belief. No lives.

## What a player learns, without being told

| After | They can say, in their own words |
|---|---|
| Cell Block | "It goes to where it last saw me." "It believes any noise." "If it checks a room and I'm not there, it stops looking there." |
| Administration | "It thinks I'm somewhere between two alarms." "It ignored a camera that saw me because it was too sure I was elsewhere." "It keeps going in the direction I was running." |
| Labs | "It can think I'm in two places." "If I make it certain of the wrong place, it can't find me again for a while." |

The journal then attaches the real terms: point estimate, sensor noise model, negative information,
unimodal Gaussian, innovation gating, constant-velocity model, multimodal belief, particle depletion.

## Comedy

Roombas wear party hats and announce "OBSTRUCTION REPORTED. SORRY." over their heads. Guards bark
"INVESTIGATING." and "TRAIL LOST." Bug tickets in the offices are written by engineers who know about
every weakness you are about to use and closed them as WONTFIX.

## MVP done criteria

- [x] Four trackers with tests for the weakness each level is built around
- [x] Nine levels, every one reachable with sensors off (`make stages`)
- [x] Unity campaign loop, views, HUD and menus compile against Unity's reference assemblies (`make unity-check`)
- [ ] Opened and played in the Unity editor
- [ ] Five to ten playtesters finish the cell block without help, and can explain one exploit per model
      in their own words afterwards
