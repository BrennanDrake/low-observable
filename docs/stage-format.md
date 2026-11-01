# Stage format (escape campaign)

Escape levels are text files in `levels/stages/`, one per level, named `NN-slug.lvl`. The file name
(without `.lvl`) is the level id, and the campaign order is the sorted file order. They are parsed by
`core/Runtime/Stages/StageParser.cs`, which gives a line number for every mistake.

After editing a level, run `make stage-gen` to regenerate `core/Runtime/Stages/BuiltInStages.cs`
(a unit test fails if the two drift apart), then `make stages` to check every level is still
reachable and see the bot balance table. To iterate without regenerating, point the Unity
`EscapeBootstrap.LevelsFolder` at `../../levels/stages` and press Play.

The drone prototype's floors use a different format: see [level-format.md](level-format.md).

## Layout of a file

```
; Comment lines start with ';'.
title: Open Plan
zone: Administration
tracker: kalman
par: 210
banner: CELL BLOCK BREACH. ADMINISTRATION TRACKING: KESTREL v3.
botroute: NX
map:
#########
#S..N..X#
#########
lights:            (optional)
.........
..11111..
.........
entities:
pickup N tool=noisemaker
```

### Header

| Key | Meaning |
|---|---|
| `title` | Shown on the briefing, HUD and level select |
| `zone` | Groups levels in the menu and tints the floor (`Cell Block`, `Administration`, `Research Labs`, ...) |
| `tracker` | WARDEN's algorithm: `lastknown`, `grid`, `kalman`, `particle`, or the product names `sightline`, `heatmap`, `kestrel`, `swarm` |
| `par` | Target time in seconds, shown on the briefing and results |
| `banner` | One line of WARDEN flavour on the briefing |
| `botroute` | Markers the headless bot visits in order before the exit (keycards, terminals, tool pickups, doors). Not used by players |

## Map glyphs

One character per 1 m cell, first row is the top (north). Rows may differ in length; short rows are
padded with wall.

| Glyph | Cell |
|---|---|
| `#` or space | Wall |
| `.` | Lit floor |
| `,` | Dark floor: cameras and guards see only `DarkVisionRange` (1.5 m) into it |
| `S` | Player start (exactly one). Also the first checkpoint |
| `X` | Exit (at least one) |
| `H` / `h` | Locker to hide in, on lit / dark floor |
| `o` | Something throwable on the floor |
| `C` | Checkpoint: reaching it moves where you respawn after capture |
| anything else | A **marker**: one cell named by a single character, given meaning in `entities:` |

Every marker must appear exactly once on the map and be used by an entity (or by another entity's
route), so typos fail loudly. Markers are floor unless they are doors, and `dark=1` on any entity makes
its cell dark.

## Lights

Optional. Same size as the map. A digit `1`-`9` puts that cell on a light circuit; a `switch` entity
with the same `circuit=` flips every cell on the circuit between lit and dark. Any other character is
ignored.

## Entities

One per line: `type marker key=value ...`. Values with spaces go in double quotes. Angles are degrees,
counter-clockwise from east (0 = east, 90 = north).

| Type | Attributes | Notes |
|---|---|---|
| `camera` | `dir=` (required), `fov=` 70, `range=` 8 | Fixed camera |
| `swivel` | `from=`, `to=` (required), `period=` 6, `phase=` 0, `fov=`, `range=` | Sweeps between two headings with eased turnarounds |
| `guard` | `route=` (required, marker letters), `speed=` 1.0, `role=` `patrol` or `hunter` | Walks the route in a loop, starting on its own marker. A patrol only answers dispatches within `PatrolLeash` (8 m). A hunter follows WARDEN's belief anywhere and, when nothing is confident, sweeps its best guess |
| `roomba` | `hat=` (any word, e.g. `party`), `dir=` random | Wanders, reports what it bumps into, apologizes. Can't catch you |
| `mic` | `sensitivity=` 1.3 | Hears sounds within loudness × sensitivity |
| `laser` | `to=` (required, one marker) | Tripwire between the two markers |
| `door` | `key=` (optional keycard name) | A wall until opened: by a terminal, or automatically when you walk up holding the named keycard |
| `keycard` | `name=` (required) | Opens doors with the same `key=` |
| `terminal` | `opens=` door markers, or `kind=forge` | Hold interact to open its doors (it beeps). A forge terminal is where the Forged fix tool works |
| `switch` | `circuit=` 1-9 (required) | Flips a light circuit (makes a little noise) |
| `pickup` | `tool=` `noisemaker`, `jammer`, `mirror` or `forge` (required) | Unlocks the tool for the rest of the campaign |
| `note` | `text=` (required), `kind=` `sticky`, `poster`, `whiteboard`, `graffiti`, `ticket`, `screen`, `chalk` | Environmental story. Bug tickets and chalk marks are where level hints live |

## Writing a level

- Teach one idea per level. The briefing names the algorithm; the room should make its weakness
  findable without being told. Put the hint in a note (a bug ticket, a whiteboard, chalk by a
  prisoner who tried before you).
- Run `dotnet run --project dotnet/LowObservable.Sim -- stage --stage <id> --check` until it passes,
  then look at the balance table. A runner that clears it means there is no pressure; a sneaker that
  can't clear a level that needs no tools usually means a camera covers a choke point completely.
- Render it: `python3 tools/render_stage.py levels/stages/<id>.lvl --out build/<id>.png`, or dump a
  bot run with `--dump build/frames` and render a GIF with `--frames`.
