using System.Collections.Generic;
using LowObservable.Core.Maths;
using LowObservable.Core.World;
using LowObservable.Unity.Views;
using UnityEngine;

namespace LowObservable.Unity.Escape
{
    /// <summary>Colours for the escape views. One place so a palette pass touches one file.</summary>
    public static class EscapePalette
    {
        public static readonly Color Player = new Color(0.96f, 0.96f, 0.96f);
        public static readonly Color PlayerHidden = new Color(0.55f, 0.55f, 0.6f);
        public static readonly Color Belief = new Color(1f, 0.22f, 0.25f, 1f);
        public static readonly Color BeliefSoft = new Color(1f, 0.22f, 0.25f, 0.45f);
        public static readonly Color Rejected = new Color(0.7f, 0.7f, 0.75f, 1f);
        public static readonly Color Camera = new Color(0.85f, 0.35f, 0.95f, 1f);
        public static readonly Color CameraCone = new Color(0.85f, 0.35f, 0.95f, 0.16f);
        public static readonly Color Alarm = new Color(1f, 0.15f, 0.15f, 0.35f);
        public static readonly Color Jammed = new Color(0.5f, 0.55f, 0.6f, 0.5f);
        public static readonly Color Unit = new Color(1f, 0.6f, 0.15f, 1f);
        public static readonly Color UnitCone = new Color(1f, 0.6f, 0.15f, 0.14f);
        public static readonly Color Roomba = new Color(0.5f, 0.9f, 0.95f, 1f);
        public static readonly Color Mic = new Color(0.45f, 1f, 0.45f, 0.8f);
        public static readonly Color Laser = new Color(1f, 0.2f, 0.2f, 0.9f);
        public static readonly Color Mirror = new Color(0.75f, 0.92f, 1f, 1f);
        public static readonly Color JammerField = new Color(0.3f, 1f, 1f, 0.5f);
        public static readonly Color Sound = new Color(1f, 1f, 1f, 0.5f);
        public static readonly Color Exit = new Color(0.35f, 0.95f, 0.55f, 1f);
        public static readonly Color Hide = new Color(0.6f, 0.52f, 0.3f, 1f);
        public static readonly Color Checkpoint = new Color(0.4f, 0.6f, 0.95f, 1f);
        public static readonly Color Throwable = new Color(0.85f, 0.85f, 0.7f, 1f);
        public static readonly Color Note = new Color(1f, 0.9f, 0.45f, 1f);
        public static readonly Color Terminal = new Color(0.35f, 0.65f, 1f, 1f);
        public static readonly Color ForgeTerminal = new Color(1f, 0.35f, 0.6f, 1f);
        public static readonly Color Keycard = new Color(1f, 0.9f, 0f, 1f);
        public static readonly Color Pickup = new Color(0f, 1f, 0.7f, 1f);
        public static readonly Color Door = new Color(0.6f, 0.4f, 0.22f, 1f);
        public static readonly Color Switch = new Color(1f, 1f, 1f, 1f);
        public static readonly Color AimOk = new Color(0.4f, 1f, 0.6f, 0.9f);
        public static readonly Color AimBlocked = new Color(0.6f, 0.6f, 0.6f, 0.6f);

        /// <summary>Floor tint per zone, so the cell block, offices and labs read differently at a glance.</summary>
        public static Color ZoneFloor(string zone)
        {
            string z = (zone ?? "").ToLowerInvariant();
            if (z.Contains("office")) return new Color(0.36f, 0.34f, 0.30f);
            if (z.Contains("lab")) return new Color(0.38f, 0.42f, 0.44f);
            if (z.Contains("server")) return new Color(0.22f, 0.26f, 0.32f);
            return new Color(0.30f, 0.32f, 0.31f); // cell block concrete
        }
    }

    /// <summary>Small drawing helpers shared by the escape views.</summary>
    public static class EscapeDraw
    {
        /// <summary>
        /// Points of a vision cone clipped by walls: the apex, then <paramref name="rays"/>+1 points along the
        /// arc, each pulled in to the first wall on its ray. Marches in 0.1 m steps, which is plenty at 1 m cells.
        /// </summary>
        public static void Fan(FloorPlan plan, Vec2 origin, double heading, double fov, double range, int rays, List<Vector3> into)
        {
            into.Clear();
            into.Add(origin.ToVector3());
            for (int k = 0; k <= rays; k++)
            {
                double a = heading - fov * 0.5 + fov * k / rays;
                var dir = Vec2.FromAngle(a);
                double reach = range;
                for (double s = 0.1; s <= range; s += 0.1)
                {
                    if (plan.IsWallAt(origin + dir * s)) { reach = s; break; }
                }
                into.Add((origin + dir * reach).ToVector3());
            }
        }

        /// <summary>Writes a triangle fan (apex first) into a mesh with one colour.</summary>
        public static void FillFan(Mesh mesh, List<Vector3> fan, Color color, List<int> trisScratch, List<Color> colorScratch)
        {
            mesh.Clear();
            if (fan.Count < 3) return;
            trisScratch.Clear();
            colorScratch.Clear();
            for (int k = 1; k < fan.Count - 1; k++)
            {
                trisScratch.Add(0);
                trisScratch.Add(k + 1);
                trisScratch.Add(k);
            }
            for (int k = 0; k < fan.Count; k++) colorScratch.Add(color);
            mesh.SetVertices(fan);
            mesh.SetColors(colorScratch);
            mesh.SetTriangles(trisScratch, 0, true);
        }

        public static GameObject CreateMeshObject(Transform parent, string name, int sortingOrder, out Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ViewKit.Unlit;
            mr.sortingOrder = sortingOrder;
            return go;
        }

        /// <summary>Sets a line renderer to a circle.</summary>
        public static void Circle(LineRenderer line, Vector3 center, float radius, int segments = 40)
        {
            line.loop = true;
            line.positionCount = segments;
            for (int k = 0; k < segments; k++)
            {
                float a = 2f * Mathf.PI * k / segments;
                line.SetPosition(k, center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        public static void Segment(LineRenderer line, Vector3 a, Vector3 b)
        {
            line.loop = false;
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
        }

        public static void SetColor(LineRenderer line, Color c)
        {
            line.startColor = c;
            line.endColor = c;
        }

        public static void SetColor(GameObject shape, Color c)
        {
            var mf = shape.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var mesh = mf.sharedMesh;
            var colors = new Color[mesh.vertexCount];
            for (int k = 0; k < colors.Length; k++) colors[k] = c;
            mesh.colors = colors;
        }
    }

    /// <summary>A grow-only pool of line renderers for things whose count changes every frame.</summary>
    public sealed class LinePool
    {
        private readonly Transform _parent;
        private readonly string _name;
        private readonly float _width;
        private readonly int _order;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private int _used;

        public LinePool(Transform parent, string name, float width, int order)
        {
            _parent = parent;
            _name = name;
            _width = width;
            _order = order;
        }

        public void Begin() => _used = 0;

        public LineRenderer Next(Color color)
        {
            if (_used == _lines.Count) _lines.Add(ViewKit.CreateLine(_parent, $"{_name} {_lines.Count}", color, _width, _order));
            var line = _lines[_used++];
            line.enabled = true;
            EscapeDraw.SetColor(line, color);
            return line;
        }

        public void End()
        {
            for (int k = _used; k < _lines.Count; k++) _lines[k].enabled = false;
        }

        public void Clear()
        {
            foreach (var l in _lines) if (l != null) Object.Destroy(l.gameObject);
            _lines.Clear();
            _used = 0;
        }
    }

    /// <summary>A grow-only pool of flat shapes (quads or triangles).</summary>
    public sealed class ShapePool
    {
        private readonly Transform _parent;
        private readonly string _name;
        private readonly float _size;
        private readonly int _order;
        private readonly bool _triangle;
        private readonly List<GameObject> _shapes = new List<GameObject>();
        private readonly List<Color> _colors = new List<Color>();
        private int _used;

        public ShapePool(Transform parent, string name, float size, int order, bool triangle = false)
        {
            _parent = parent;
            _name = name;
            _size = size;
            _order = order;
            _triangle = triangle;
        }

        public void Begin() => _used = 0;

        public GameObject Next(Color color, Vector3 position, float rotationDegrees = 0f, float scale = 1f)
        {
            if (_used == _shapes.Count)
            {
                _shapes.Add(ViewKit.CreateShape(_parent, $"{_name} {_shapes.Count}", color, _size, _order, _triangle));
                _colors.Add(color);
            }
            var go = _shapes[_used];
            if (_colors[_used] != color)
            {
                EscapeDraw.SetColor(go, color);
                _colors[_used] = color;
            }
            _used++;
            go.SetActive(true);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            return go;
        }

        public void End()
        {
            for (int k = _used; k < _shapes.Count; k++) _shapes[k].SetActive(false);
        }

        public void Clear()
        {
            foreach (var s in _shapes) if (s != null) Object.Destroy(s);
            _shapes.Clear();
            _colors.Clear();
            _used = 0;
        }
    }
}
