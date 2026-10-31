using System;
using System.Collections.Generic;
using LowObservable.Core.Maths;

namespace LowObservable.Core.World
{
    [Flags]
    public enum CellFlags
    {
        Floor = 0,
        Wall = 1,
        /// <summary>Unlit cell: cameras (yours and the Seeker's) cannot see into it.</summary>
        Dark = 2,
        /// <summary>Slippery floor: real wheel slip is much higher than the odometry model assumes.</summary>
        Slick = 4,
    }

    /// <summary>A mapped feature the drone can range to. Several landmarks may share an appearance.</summary>
    public sealed class Landmark
    {
        public int Id { get; }
        public Vec2 Position { get; }

        /// <summary>
        /// Visual class the camera reports ("red valve" = 0, "blue panel" = 1, ...). Landmarks with
        /// the same appearance are indistinguishable by camera, which is what makes data association hard.
        /// </summary>
        public int Appearance { get; }

        /// <summary>The ASCII glyph that defined this landmark, kept for debugging and editors.</summary>
        public char Glyph { get; }

        public Landmark(int id, Vec2 position, int appearance, char glyph)
        {
            Id = id;
            Position = position;
            Appearance = appearance;
            Glyph = glyph;
        }
    }

    /// <summary>
    /// The ground-truth floor: a grid of cells plus landmarks and special points. In the MVP the
    /// player is given this as the "blueprint", so the problem is localization against a known map.
    /// Grid cell (i, j) covers [i, i+1) x [j, j+1) times <see cref="CellSize"/>, with +Y up.
    /// </summary>
    public sealed class FloorPlan
    {
        private readonly CellFlags[,] _cells;
        private readonly List<Landmark> _landmarks;

        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public double CellSize { get; }
        public IReadOnlyList<Landmark> Landmarks => _landmarks;
        public Vec2 PlayerStart { get; }
        public double PlayerStartHeading { get; }
        public Vec2 Uplink { get; }
        public Vec2 SeekerStart { get; }

        public FloorPlan(string name, CellFlags[,] cells, double cellSize, IEnumerable<Landmark> landmarks,
            Vec2 playerStart, double playerStartHeading, Vec2 uplink, Vec2 seekerStart)
        {
            Name = name;
            _cells = cells ?? throw new ArgumentNullException(nameof(cells));
            Width = cells.GetLength(0);
            Height = cells.GetLength(1);
            CellSize = cellSize;
            _landmarks = new List<Landmark>(landmarks);
            PlayerStart = playerStart;
            PlayerStartHeading = playerStartHeading;
            Uplink = uplink;
            SeekerStart = seekerStart;
        }

        public Vec2 WorldSize => new Vec2(Width * CellSize, Height * CellSize);

        public bool InBounds(int i, int j) => i >= 0 && j >= 0 && i < Width && j < Height;

        public CellFlags GetCell(int i, int j) => InBounds(i, j) ? _cells[i, j] : CellFlags.Wall;

        /// <summary>
        /// Changes a cell at runtime (doors opening, lights switching). Only the escape mode does this;
        /// systems that cache free cells (trackers, particle filters) must be told to refresh.
        /// </summary>
        public void SetCell(int i, int j, CellFlags flags)
        {
            if (InBounds(i, j)) _cells[i, j] = flags;
        }

        public CellFlags GetCellAt(Vec2 p)
        {
            WorldToCell(p, out int i, out int j);
            return GetCell(i, j);
        }

        public void WorldToCell(Vec2 p, out int i, out int j)
        {
            i = (int)System.Math.Floor(p.X / CellSize);
            j = (int)System.Math.Floor(p.Y / CellSize);
        }

        public Vec2 CellCenter(int i, int j) => new Vec2((i + 0.5) * CellSize, (j + 0.5) * CellSize);

        public bool IsWall(int i, int j) => (GetCell(i, j) & CellFlags.Wall) != 0;
        public bool IsWallAt(Vec2 p) => (GetCellAt(p) & CellFlags.Wall) != 0;
        public bool IsDarkAt(Vec2 p) => (GetCellAt(p) & CellFlags.Dark) != 0;
        public bool IsSlickAt(Vec2 p) => (GetCellAt(p) & CellFlags.Slick) != 0;

        /// <summary>True if a disc of the given radius at p overlaps no wall cell (checks the center and 8 rim points).</summary>
        public bool IsFree(Vec2 p, double radius)
        {
            if (IsWallAt(p)) return false;
            if (radius <= 0) return true;
            for (int k = 0; k < 8; k++)
            {
                double a = k * Angle.TwoPi / 8.0;
                if (IsWallAt(p + Vec2.FromAngle(a, radius))) return false;
            }
            return true;
        }

        /// <summary>
        /// Line of sight between two points: false if the segment crosses any wall cell.
        /// Walks the grid exactly (Amanatides-Woo voxel traversal), so thin diagonal gaps behave predictably.
        /// </summary>
        public bool LineOfSight(Vec2 a, Vec2 b)
        {
            WorldToCell(a, out int i, out int j);
            WorldToCell(b, out int iEnd, out int jEnd);
            if (IsWall(i, j)) return false;

            double dx = (b.X - a.X) / CellSize;
            double dy = (b.Y - a.Y) / CellSize;
            int stepI = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
            int stepJ = dy > 0 ? 1 : (dy < 0 ? -1 : 0);

            double ax = a.X / CellSize, ay = a.Y / CellSize;
            double tMaxX = stepI != 0 ? ((stepI > 0 ? (i + 1 - ax) : (ax - i)) / System.Math.Abs(dx)) : double.PositiveInfinity;
            double tMaxY = stepJ != 0 ? ((stepJ > 0 ? (j + 1 - ay) : (ay - j)) / System.Math.Abs(dy)) : double.PositiveInfinity;
            double tDeltaX = stepI != 0 ? 1.0 / System.Math.Abs(dx) : double.PositiveInfinity;
            double tDeltaY = stepJ != 0 ? 1.0 / System.Math.Abs(dy) : double.PositiveInfinity;

            // An exact traversal crosses exactly |di| + |dj| cell boundaries, so step that many times.
            // Counting steps (rather than testing for the end cell) keeps round-off from looping forever.
            int steps = System.Math.Abs(iEnd - i) + System.Math.Abs(jEnd - j);
            for (int n = 0; n < steps; n++)
            {
                if (tMaxX < tMaxY) { tMaxX += tDeltaX; i += stepI; }
                else { tMaxY += tDeltaY; j += stepJ; }
                if (IsWall(i, j)) return false;
            }
            return true;
        }

        /// <summary>All non-wall cells, for uniform sampling (e.g. particle injection).</summary>
        public List<(int i, int j)> FreeCells()
        {
            var list = new List<(int, int)>();
            for (int i = 0; i < Width; i++)
                for (int j = 0; j < Height; j++)
                    if (!IsWall(i, j)) list.Add((i, j));
            return list;
        }

        public Landmark GetLandmark(int id)
        {
            foreach (var l in _landmarks) if (l.Id == id) return l;
            return null;
        }
    }
}
