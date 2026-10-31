using System.Collections.Generic;
using System.Globalization;
using LowObservable.Core.Maths;
using LowObservable.Core.Tracking;
using LowObservable.Core.World;

namespace LowObservable.Core.Stages
{
    public enum EntityType
    {
        Camera,
        Swivel,
        Guard,
        Roomba,
        Mic,
        Laser,
        Door,
        Terminal,
        Keycard,
        Switch,
        Pickup,
        Note,
    }

    /// <summary>One entity line from a stage file: a type, the map marker it sits on, and key=value attributes.</summary>
    public sealed class EntityDef
    {
        public EntityType Type;
        public char Marker;
        public Vec2 Position;
        public int CellI;
        public int CellJ;
        public readonly Dictionary<string, string> Attributes = new Dictionary<string, string>();

        public string Get(string key, string fallback = null) => Attributes.TryGetValue(key, out var v) ? v : fallback;

        public double GetDouble(string key, double fallback)
        {
            return Attributes.TryGetValue(key, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fallback;
        }

        public bool Has(string key) => Attributes.ContainsKey(key);
    }

    /// <summary>
    /// A parsed stage: geometry, WARDEN's tracker for this level, and every entity. Immutable after parsing;
    /// <see cref="Stage"/> copies the cells so doors and lights can change during play.
    /// </summary>
    public sealed class StageDefinition
    {
        public string Id;
        public string Title;
        public string Zone;
        public TrackerKind Tracker;
        /// <summary>A target time in seconds, shown on the results screen.</summary>
        public double Par = 180;
        /// <summary>One line shown on the WARDEN terminal when the level starts (environmental, not dialogue).</summary>
        public string Banner = "";
        /// <summary>Optional ordered list of markers for the headless bot to visit (keycards, terminals, exit).</summary>
        public string BotRoute = "";

        public CellFlags[,] Cells;
        public int Width;
        public int Height;
        public double CellSize = 1.0;

        /// <summary>Light circuit per cell (0 = not switchable).</summary>
        public int[,] Circuits;

        public Vec2 PlayerStart;
        public readonly List<Vec2> Exits = new List<Vec2>();
        public readonly List<Vec2> HideSpots = new List<Vec2>();
        public readonly List<Vec2> Throwables = new List<Vec2>();
        public readonly List<Vec2> Checkpoints = new List<Vec2>();
        public readonly List<EntityDef> Entities = new List<EntityDef>();
        public readonly Dictionary<char, Vec2> Markers = new Dictionary<char, Vec2>();

        /// <summary>A fresh, mutable floor plan for one play-through.</summary>
        public FloorPlan CreatePlan()
        {
            var cells = (CellFlags[,])Cells.Clone();
            var exit = Exits.Count > 0 ? Exits[0] : PlayerStart;
            return new FloorPlan(Id, cells, CellSize, new Landmark[0], PlayerStart, 0, exit, PlayerStart);
        }

        public IEnumerable<EntityDef> OfType(EntityType type)
        {
            foreach (var e in Entities) if (e.Type == type) yield return e;
        }
    }
}
