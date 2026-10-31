using System.Collections.Generic;
using LowObservable.Core.Maths;

namespace LowObservable.Core.World
{
    /// <summary>
    /// 8-connected A* over the floor grid. Diagonal moves are only allowed when both adjacent
    /// orthogonal cells are free, so agents never cut wall corners. Used by the Seeker and the autopilot.
    /// </summary>
    public sealed class GridPathfinder
    {
        private readonly FloorPlan _plan;

        public GridPathfinder(FloorPlan plan)
        {
            _plan = plan;
        }

        /// <summary>
        /// Returns world-space waypoints (cell centers) from start to goal, excluding the start cell,
        /// or an empty list if the goal is unreachable.
        /// </summary>
        public List<Vec2> FindPath(Vec2 start, Vec2 goal)
        {
            _plan.WorldToCell(start, out int si, out int sj);
            _plan.WorldToCell(goal, out int gi, out int gj);
            var result = new List<Vec2>();
            // A belief can drift into a wall cell (the drone is not there, but the estimate is). Plan from the
            // nearest free cell instead of giving up, otherwise agents freeze whenever they are a bit lost.
            if (_plan.IsWall(si, sj) && !NearestFree(si, sj, out si, out sj)) return result;
            if (_plan.IsWall(gi, gj) && !NearestFree(gi, gj, out gi, out gj)) return result;
            if (si == gi && sj == gj)
            {
                result.Add(goal);
                return result;
            }

            int w = _plan.Width, h = _plan.Height;
            var gScore = new double[w, h];
            var cameFrom = new int[w, h];
            var closed = new bool[w, h];
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++) { gScore[i, j] = double.PositiveInfinity; cameFrom[i, j] = -1; }

            var open = new MinHeap();
            gScore[si, sj] = 0;
            open.Push(Heuristic(si, sj, gi, gj), si, sj);

            while (open.Count > 0)
            {
                open.Pop(out int ci, out int cj);
                if (closed[ci, cj]) continue;
                closed[ci, cj] = true;
                if (ci == gi && cj == gj) break;

                for (int di = -1; di <= 1; di++)
                {
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int ni = ci + di, nj = cj + dj;
                        if (!_plan.InBounds(ni, nj) || _plan.IsWall(ni, nj) || closed[ni, nj]) continue;
                        if (di != 0 && dj != 0 && (_plan.IsWall(ci + di, cj) || _plan.IsWall(ci, cj + dj))) continue;
                        double step = (di != 0 && dj != 0) ? 1.41421356 : 1.0;
                        double tentative = gScore[ci, cj] + step;
                        if (tentative < gScore[ni, nj])
                        {
                            gScore[ni, nj] = tentative;
                            cameFrom[ni, nj] = ci * h + cj;
                            open.Push(tentative + Heuristic(ni, nj, gi, gj), ni, nj);
                        }
                    }
                }
            }

            if (cameFrom[gi, gj] < 0) return result;

            var cells = new List<Vec2>();
            int pi = gi, pj = gj;
            while (!(pi == si && pj == sj))
            {
                cells.Add(_plan.CellCenter(pi, pj));
                int prev = cameFrom[pi, pj];
                pi = prev / h;
                pj = prev % h;
            }
            cells.Reverse();
            cells[cells.Count - 1] = goal;
            return cells;
        }

        /// <summary>Nearest non-wall cell within a few cells (ring search), or false if none.</summary>
        public bool NearestFree(int i, int j, out int fi, out int fj)
        {
            for (int r = 1; r <= 4; r++)
            {
                double best = double.PositiveInfinity;
                fi = fj = -1;
                for (int di = -r; di <= r; di++)
                {
                    for (int dj = -r; dj <= r; dj++)
                    {
                        if (System.Math.Max(System.Math.Abs(di), System.Math.Abs(dj)) != r) continue;
                        int ni = i + di, nj = j + dj;
                        if (!_plan.InBounds(ni, nj) || _plan.IsWall(ni, nj)) continue;
                        double d = di * di + dj * dj;
                        if (d < best) { best = d; fi = ni; fj = nj; }
                    }
                }
                if (fi >= 0) return true;
            }
            fi = i;
            fj = j;
            return false;
        }

        private static double Heuristic(int i, int j, int gi, int gj)
        {
            int dx = System.Math.Abs(i - gi), dy = System.Math.Abs(j - gj);
            return (dx + dy) + (1.41421356 - 2.0) * System.Math.Min(dx, dy);
        }

        /// <summary>Tiny binary heap keyed on f-score; avoids depending on PriorityQueue (not in netstandard2.1).</summary>
        private sealed class MinHeap
        {
            private readonly List<(double f, int i, int j)> _items = new List<(double, int, int)>();
            public int Count => _items.Count;

            public void Push(double f, int i, int j)
            {
                _items.Add((f, i, j));
                int c = _items.Count - 1;
                while (c > 0)
                {
                    int p = (c - 1) / 2;
                    if (_items[p].f <= _items[c].f) break;
                    (_items[p], _items[c]) = (_items[c], _items[p]);
                    c = p;
                }
            }

            public void Pop(out int i, out int j)
            {
                var top = _items[0];
                i = top.i;
                j = top.j;
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _items.RemoveAt(last);
                int p = 0;
                while (true)
                {
                    int l = 2 * p + 1, r = l + 1, m = p;
                    if (l < _items.Count && _items[l].f < _items[m].f) m = l;
                    if (r < _items.Count && _items[r].f < _items[m].f) m = r;
                    if (m == p) break;
                    (_items[p], _items[m]) = (_items[m], _items[p]);
                    p = m;
                }
            }
        }
    }
}
