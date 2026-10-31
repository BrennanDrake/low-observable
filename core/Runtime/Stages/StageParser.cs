using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LowObservable.Core.Maths;
using LowObservable.Core.Tracking;
using LowObservable.Core.World;

namespace LowObservable.Core.Stages
{
    /// <summary>
    /// Parses the stage format (docs/stage-format.md):
    /// <code>
    /// title: Lights Out
    /// zone: Cell Block C
    /// tracker: lastknown | grid | kalman | particle
    /// map:
    /// ##########
    /// #S..a...X#
    /// ##########
    /// lights:            (optional, same size as the map; digits name light circuits)
    /// entities:
    /// camera a dir=270 fov=70 range=8
    /// </code>
    /// Map glyphs: <c>#</c> or space wall, <c>.</c> lit floor, <c>,</c> dark floor, <c>S</c> start,
    /// <c>X</c> exit, <c>H</c> hiding spot, <c>h</c> hiding spot in the dark, <c>o</c> throwable,
    /// <c>C</c> checkpoint. Any other character is a marker that an entity line must name.
    /// </summary>
    public static class StageParser
    {
        private enum Section { Header, Map, Lights, Entities }

        public static StageDefinition Parse(string id, string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var def = new StageDefinition { Id = id, Title = id, Zone = "", Tracker = TrackerKind.LastKnown };
            var mapRows = new List<string>();
            var lightRows = new List<string>();
            var entityLines = new List<(string line, int number)>();
            var section = Section.Header;
            int lineNo = 0;

            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                lineNo++;
                string line = raw.TrimEnd();
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith(";")) continue;
                string lower = trimmed.ToLowerInvariant();
                if (lower == "map:") { section = Section.Map; continue; }
                if (lower == "lights:") { section = Section.Lights; continue; }
                if (lower == "entities:") { section = Section.Entities; continue; }
                switch (section)
                {
                    case Section.Header:
                        ParseHeader(def, trimmed, lineNo);
                        break;
                    case Section.Map:
                        mapRows.Add(line);
                        break;
                    case Section.Lights:
                        lightRows.Add(line);
                        break;
                    default:
                        entityLines.Add((trimmed, lineNo));
                        break;
                }
            }

            if (mapRows.Count == 0) throw new FormatException($"{id}: stage has no map.");
            BuildMap(def, mapRows, lightRows);
            var referenced = new HashSet<char>();
            foreach (var (line, number) in entityLines) def.Entities.Add(ParseEntity(def, line, number, referenced));
            foreach (var kv in def.Markers)
            {
                if (!referenced.Contains(kv.Key))
                    throw new FormatException($"{id}: map marker '{kv.Key}' is not used by any entity.");
            }
            Validate(def);
            return def;
        }

        private static void ParseHeader(StageDefinition def, string line, int lineNo)
        {
            int colon = line.IndexOf(':');
            if (colon <= 0) throw new FormatException($"{def.Id} line {lineNo}: expected 'key: value'.");
            string key = line.Substring(0, colon).Trim().ToLowerInvariant();
            string value = line.Substring(colon + 1).Trim();
            switch (key)
            {
                case "title": def.Title = value; break;
                case "zone": def.Zone = value; break;
                case "banner": def.Banner = value; break;
                case "botroute": def.BotRoute = value.Replace(" ", ""); break;
                case "par": def.Par = double.Parse(value, CultureInfo.InvariantCulture); break;
                case "tracker": def.Tracker = ParseTracker(value, def.Id, lineNo); break;
                default: throw new FormatException($"{def.Id} line {lineNo}: unknown header '{key}'.");
            }
        }

        public static TrackerKind ParseTracker(string value, string id = "", int lineNo = 0)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "lastknown":
                case "sightline": return TrackerKind.LastKnown;
                case "grid":
                case "heatmap": return TrackerKind.OccupancyGrid;
                case "kalman":
                case "kestrel": return TrackerKind.Kalman;
                case "particle":
                case "swarm": return TrackerKind.Particle;
                default: throw new FormatException($"{id} line {lineNo}: unknown tracker '{value}'.");
            }
        }

        private static void BuildMap(StageDefinition def, List<string> rows, List<string> lightRows)
        {
            int width = 0;
            foreach (var r in rows) width = System.Math.Max(width, r.Length);
            int height = rows.Count;
            def.Width = width;
            def.Height = height;
            def.Cells = new CellFlags[width, height];
            def.Circuits = new int[width, height];
            bool hasStart = false;
            for (int row = 0; row < height; row++)
            {
                int j = height - 1 - row;
                string line = rows[row];
                for (int i = 0; i < width; i++)
                {
                    char ch = i < line.Length ? line[i] : '#';
                    var c = new Vec2((i + 0.5) * def.CellSize, (j + 0.5) * def.CellSize);
                    CellFlags flags = CellFlags.Floor;
                    switch (ch)
                    {
                        case '#':
                        case ' ': flags = CellFlags.Wall; break;
                        case '.': break;
                        case ',': flags = CellFlags.Dark; break;
                        case 'S': def.PlayerStart = c; hasStart = true; break;
                        case 'X': def.Exits.Add(c); break;
                        case 'H': def.HideSpots.Add(c); break;
                        case 'h': def.HideSpots.Add(c); flags = CellFlags.Dark; break;
                        case 'o': def.Throwables.Add(c); break;
                        case 'C': def.Checkpoints.Add(c); break;
                        default:
                            if (def.Markers.ContainsKey(ch))
                                throw new FormatException($"{def.Id}: marker '{ch}' appears more than once on the map.");
                            def.Markers[ch] = c;
                            break;
                    }
                    def.Cells[i, j] = flags;
                }
            }
            if (!hasStart) throw new FormatException($"{def.Id}: map needs a player start (S).");
            if (def.Exits.Count == 0) throw new FormatException($"{def.Id}: map needs at least one exit (X).");

            if (lightRows.Count > 0)
            {
                if (lightRows.Count != height) throw new FormatException($"{def.Id}: lights section must have {height} rows.");
                for (int row = 0; row < height; row++)
                {
                    int j = height - 1 - row;
                    string line = lightRows[row];
                    for (int i = 0; i < width && i < line.Length; i++)
                    {
                        char ch = line[i];
                        if (ch >= '1' && ch <= '9') def.Circuits[i, j] = ch - '0';
                    }
                }
            }
        }

        private static EntityDef ParseEntity(StageDefinition def, string line, int lineNo, HashSet<char> referenced)
        {
            var tokens = Tokenize(line, def.Id, lineNo);
            if (tokens.Count < 2) throw new FormatException($"{def.Id} line {lineNo}: expected '<type> <marker> key=value...'.");
            var e = new EntityDef { Type = ParseType(tokens[0], def.Id, lineNo) };
            if (tokens[1].Length != 1) throw new FormatException($"{def.Id} line {lineNo}: marker must be a single character.");
            e.Marker = tokens[1][0];
            if (!def.Markers.TryGetValue(e.Marker, out var pos))
                throw new FormatException($"{def.Id} line {lineNo}: marker '{e.Marker}' is not on the map.");
            e.Position = pos;
            e.CellI = (int)System.Math.Floor(pos.X / def.CellSize);
            e.CellJ = (int)System.Math.Floor(pos.Y / def.CellSize);
            referenced.Add(e.Marker);
            for (int k = 2; k < tokens.Count; k++)
            {
                int eq = tokens[k].IndexOf('=');
                if (eq <= 0) throw new FormatException($"{def.Id} line {lineNo}: expected key=value, got '{tokens[k]}'.");
                string key = tokens[k].Substring(0, eq).ToLowerInvariant();
                string value = tokens[k].Substring(eq + 1);
                e.Attributes[key] = value;
            }

            // Markers named inside attributes (routes, laser ends, doors a terminal opens) count as used.
            string markerKey = e.Type == EntityType.Guard ? "route" : e.Type == EntityType.Laser ? "to" : e.Type == EntityType.Terminal ? "opens" : null;
            if (markerKey != null)
            {
                string key = markerKey;
                string v = e.Get(key);
                foreach (char m in v ?? "")
                {
                    if (!def.Markers.ContainsKey(m))
                        throw new FormatException($"{def.Id} line {lineNo}: '{key}' names marker '{m}', which is not on the map.");
                    referenced.Add(m);
                }
            }

            if (e.Get("dark") == "1") def.Cells[e.CellI, e.CellJ] |= CellFlags.Dark;
            if (e.Type == EntityType.Door) def.Cells[e.CellI, e.CellJ] = CellFlags.Wall;
            return e;
        }

        private static EntityType ParseType(string token, string id, int lineNo)
        {
            switch (token.ToLowerInvariant())
            {
                case "camera": return EntityType.Camera;
                case "swivel": return EntityType.Swivel;
                case "guard": return EntityType.Guard;
                case "roomba": return EntityType.Roomba;
                case "mic": return EntityType.Mic;
                case "laser": return EntityType.Laser;
                case "door": return EntityType.Door;
                case "terminal": return EntityType.Terminal;
                case "keycard": return EntityType.Keycard;
                case "switch": return EntityType.Switch;
                case "pickup": return EntityType.Pickup;
                case "note": return EntityType.Note;
                default: throw new FormatException($"{id} line {lineNo}: unknown entity type '{token}'.");
            }
        }

        /// <summary>Splits on spaces, keeping double-quoted values together (text="two words").</summary>
        private static List<string> Tokenize(string line, string id, int lineNo)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            foreach (char ch in line)
            {
                if (ch == '"') { quoted = !quoted; continue; }
                if (ch == ' ' && !quoted)
                {
                    if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
                    continue;
                }
                sb.Append(ch);
            }
            if (quoted) throw new FormatException($"{id} line {lineNo}: unterminated quote.");
            if (sb.Length > 0) tokens.Add(sb.ToString());
            return tokens;
        }

        private static void Validate(StageDefinition def)
        {
            foreach (var e in def.Entities)
            {
                switch (e.Type)
                {
                    case EntityType.Camera:
                        if (!e.Has("dir")) throw new FormatException($"{def.Id}: camera '{e.Marker}' needs dir=<degrees>.");
                        break;
                    case EntityType.Swivel:
                        if (!e.Has("from") || !e.Has("to")) throw new FormatException($"{def.Id}: swivel '{e.Marker}' needs from= and to=.");
                        break;
                    case EntityType.Guard:
                        if (string.IsNullOrEmpty(e.Get("route"))) throw new FormatException($"{def.Id}: guard '{e.Marker}' needs route=<markers>.");
                        break;
                    case EntityType.Laser:
                        if (string.IsNullOrEmpty(e.Get("to")) || e.Get("to").Length != 1) throw new FormatException($"{def.Id}: laser '{e.Marker}' needs to=<marker>.");
                        break;
                    case EntityType.Terminal:
                        string kind = e.Get("kind", "door");
                        if (kind == "door" && string.IsNullOrEmpty(e.Get("opens"))) throw new FormatException($"{def.Id}: terminal '{e.Marker}' needs opens=<door markers> or kind=forge.");
                        if (kind != "door" && kind != "forge") throw new FormatException($"{def.Id}: terminal '{e.Marker}' kind must be door or forge.");
                        break;
                    case EntityType.Keycard:
                        if (string.IsNullOrEmpty(e.Get("name"))) throw new FormatException($"{def.Id}: keycard '{e.Marker}' needs name=.");
                        break;
                    case EntityType.Switch:
                        if (!int.TryParse(e.Get("circuit", ""), out int circuit) || circuit < 1 || circuit > 9) throw new FormatException($"{def.Id}: switch '{e.Marker}' needs circuit=1..9.");
                        break;
                    case EntityType.Pickup:
                        if (!ToolInfo.TryParse(e.Get("tool", ""), out _)) throw new FormatException($"{def.Id}: pickup '{e.Marker}' needs tool=noisemaker|jammer|mirror|forge.");
                        break;
                    case EntityType.Note:
                        if (string.IsNullOrEmpty(e.Get("text"))) throw new FormatException($"{def.Id}: note '{e.Marker}' needs text=\"...\".");
                        break;
                }
            }
            if (!string.IsNullOrEmpty(def.BotRoute))
            {
                foreach (char m in def.BotRoute)
                    if (m != 'X' && !def.Markers.ContainsKey(m)) throw new FormatException($"{def.Id}: botroute names unknown marker '{m}'.");
            }
        }
    }
}
