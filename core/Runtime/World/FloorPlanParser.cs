using System;
using System.Collections.Generic;
using LowObservable.Core.Maths;

namespace LowObservable.Core.World
{
    /// <summary>
    /// Parses the ASCII level format (see docs/level-format.md). One character per cell, first line
    /// is the top (highest Y) row. Lines starting with ';' are comments; blank lines are ignored.
    ///
    /// <code>
    ///   #  wall              .  lit floor          ,  dark floor        ~  slick floor     ;  dark slick floor
    ///   a-z  landmark on lit floor (letter = appearance class)
    ///   A-Z  landmark on dark floor (same classes as a-z)
    ///   S  player start      U  uplink (exit)      K  Seeker start
    ///   &gt; &lt; ^ v  player start with a heading (east, west, north, south)
    /// </code>
    /// </summary>
    public static class FloorPlanParser
    {
        public static FloorPlan Parse(string name, string text, double cellSize = 1.0)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var rows = new List<string>();
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.Length == 0 || line.TrimStart().StartsWith(";")) continue;
                rows.Add(line);
            }
            if (rows.Count == 0) throw new FormatException("Level has no rows.");

            int width = 0;
            foreach (var r in rows) width = System.Math.Max(width, r.Length);
            int height = rows.Count;

            var cells = new CellFlags[width, height];
            var landmarks = new List<Landmark>();
            Vec2? start = null, uplink = null, seeker = null;
            double heading = 0;

            for (int row = 0; row < height; row++)
            {
                int j = height - 1 - row;
                string line = rows[row];
                for (int i = 0; i < width; i++)
                {
                    char ch = i < line.Length ? line[i] : '#';
                    var center = new Vec2((i + 0.5) * cellSize, (j + 0.5) * cellSize);
                    switch (ch)
                    {
                        case '#': cells[i, j] = CellFlags.Wall; break;
                        case ' ': cells[i, j] = CellFlags.Wall; break;
                        case '.': cells[i, j] = CellFlags.Floor; break;
                        case ',': cells[i, j] = CellFlags.Dark; break;
                        case '~': cells[i, j] = CellFlags.Slick; break;
                        case ';': cells[i, j] = CellFlags.Slick | CellFlags.Dark; break;
                        case 'S': start = center; heading = 0; break;
                        case '>': start = center; heading = 0; break;
                        case '^': start = center; heading = Angle.Pi / 2; break;
                        case '<': start = center; heading = Angle.Pi; break;
                        case 'v': start = center; heading = -Angle.Pi / 2; break;
                        case 'U': uplink = center; break;
                        case 'K': seeker = center; break;
                        default:
                            if (ch >= 'a' && ch <= 'z' && ch != 'v')
                            {
                                landmarks.Add(new Landmark(landmarks.Count, center, ch - 'a', ch));
                            }
                            else if (ch >= 'A' && ch <= 'Z' && ch != 'S' && ch != 'U' && ch != 'K')
                            {
                                cells[i, j] = CellFlags.Dark;
                                landmarks.Add(new Landmark(landmarks.Count, center, ch - 'A', ch));
                            }
                            else
                            {
                                throw new FormatException($"Unknown level glyph '{ch}' at row {row + 1}, column {i + 1}.");
                            }
                            break;
                    }
                }
            }

            if (start == null) throw new FormatException("Level needs a player start (S or > < ^ v).");
            if (uplink == null) throw new FormatException("Level needs an uplink (U).");
            if (seeker == null) throw new FormatException("Level needs a Seeker start (K).");

            return new FloorPlan(name, cells, cellSize, landmarks, start.Value, heading, uplink.Value, seeker.Value);
        }
    }
}
