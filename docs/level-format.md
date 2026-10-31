# Level format

Floors are ASCII text, one character per 1 m cell. The first row is the top (north, highest Y) of the
map. Parsed by `core/Runtime/World/FloorPlanParser.cs`; files live in `levels/`.

## Glyphs

| Glyph | Cell |
|---|---|
| `#` | Wall (a space also counts as wall) |
| `.` | Lit floor |
| `,` | Dark floor: your camera sees no landmarks here, and the Seeker only sees you within 1.5 m |
| `~` | Slick floor: odometry slips `SlickNoiseScale` times more |
| `;` | Dark *and* slick floor |
| `a`-`z` | Landmark on lit floor. The letter is its **appearance class**: same letter, same look to the camera |
| `A`-`Z` | Landmark on dark floor, same classes as lowercase (`A` looks like `a`). The ping still finds it; the camera can't |
| `S` or `>` | Player start facing east |
| `^` `<` `v` | Player start facing north, west, south |
| `U` | Uplink (the exit you dock with) |
| `K` | Seeker start |

Reserved letters: lowercase `v` is a start marker, not a landmark, and uppercase `S`, `U` and `K` are
markers, so classes `v`, `s`, `u` and `k` can't be used as dark landmarks. Landmarks, starts, the
uplink and the Seeker sit on lit floor unless stated otherwise; to put a marker in the dark, surround
it with dark cells (the cell itself is lit).

Lines whose first non-space character is `;` are comments, and blank lines are ignored. Because of
that, **a map row must not start with `;`**; border every map with walls and this never comes up.
Short rows are padded with walls on the right.

## Example

```
; Twin rooms: identical landmarks make the camera unreliable.
##########
#..a..a..#
#>......U#
##########
```

## Design guidance

The level is where the estimation lessons are set up. Some patterns that work:

- **Aliasing:** repeat an appearance class in two places that are far apart in the map but similar
  from inside (the twin labs in Annex B use `c`, `d` and `e` twice). The gate keeps wrong matches
  rare while the player is well localized and likely once they are lost.
- **Darkness on the short route:** a dark corridor hides the player from the Seeker and blinds the
  camera at the same time. Put a dark landmark (uppercase) in it so pinging is the only fix.
- **Slick floor where the player isn't expecting it:** the filter only inflates its noise on cells it
  believes are slick, so ice near a junction produces overconfidence that shows up at the dock.
- **Landmark spacing:** with `CameraRange` 6 m and `PingRange` 9 m, a lit room wants a landmark every
  6 to 8 m; a stretch with none is a stretch where only odometry works.
- **Two bearings beat one:** a single landmark only fixes position across its line of sight. Rooms
  where two landmarks are visible at different bearings give much tighter fixes.

## Built-in levels

`core/Runtime/Levels/BuiltInLevels.cs` compiles in `annex-b` (the MVP floor) and `open-room` (a
tutorial-sized test room) so tests, the simulator and a fresh Unity scene work without files.
`levels/*.txt` are copies; when you change a built-in level, change both and run `make sim`.

## Checking a level

```bash
make sim                                                 # built-in Annex B, all policies
./scripts/pc/sim.sh --level-file levels/my-floor.txt --policy all --seeds 30
```

A level is broken if the escape rate is 0% for every policy (unreachable uplink or an impossible
Seeker) or near 100% for the reckless bot (no pressure). See [tuning.md](tuning.md).
