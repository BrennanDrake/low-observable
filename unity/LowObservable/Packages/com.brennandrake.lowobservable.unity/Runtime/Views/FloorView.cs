using System.Collections.Generic;
using LowObservable.Core.World;
using UnityEngine;

namespace LowObservable.Unity.Views
{
    /// <summary>
    /// Draws the blueprint: walls, lit/dark/slick floor, landmarks coloured by appearance, and the uplink.
    /// The blueprint is something the player legitimately knows, so it is drawn from the floor plan
    /// directly. One combined mesh, rebuilt only when the level changes.
    /// </summary>
    public sealed class FloorView : MonoBehaviour
    {
        public EpisodeRunner Runner;

        public Color WallColor = new Color(0.10f, 0.11f, 0.13f);
        public Color FloorColor = new Color(0.30f, 0.32f, 0.36f);
        public Color DarkColor = new Color(0.16f, 0.17f, 0.24f);
        public Color SlickColor = new Color(0.28f, 0.40f, 0.46f);
        public Color UplinkColor = new Color(0.35f, 1.00f, 0.55f);
        [Range(0.1f, 1f)] public float LandmarkSize = 0.45f;

        private FloorPlan _built;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Start()
        {
            if (Runner == null) Runner = FindFirstObjectByType<EpisodeRunner>();
            Build();
        }

        private void Update()
        {
            if (Runner != null && Runner.Plan != _built) Build();
        }

        private void Build()
        {
            foreach (var go in _spawned) Destroy(go);
            _spawned.Clear();
            var plan = Runner.Plan;
            _built = plan;

            var grid = new GameObject("Floor");
            grid.transform.SetParent(transform, false);
            _spawned.Add(grid);
            grid.AddComponent<MeshFilter>().sharedMesh = BuildGridMesh(plan);
            var mr = grid.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ViewKit.Unlit;
            mr.sortingOrder = Layers.Floor;

            foreach (var lm in plan.Landmarks)
            {
                var marker = ViewKit.CreateShape(transform, $"Landmark {lm.Glyph}#{lm.Id}", ViewKit.ColorForAppearance(lm.Appearance), LandmarkSize, Layers.Landmarks);
                marker.transform.position = lm.Position.ToVector3();
                marker.transform.rotation = Quaternion.Euler(0, 0, 45f); // diamonds read as "features", not floor
                _spawned.Add(marker);
            }

            var uplink = ViewKit.CreateShape(transform, "Uplink", UplinkColor, (float)plan.CellSize * 0.8f, Layers.Landmarks);
            uplink.transform.position = plan.Uplink.ToVector3();
            _spawned.Add(uplink);
        }

        private Mesh BuildGridMesh(FloorPlan plan)
        {
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            float s = (float)plan.CellSize;
            for (int i = 0; i < plan.Width; i++)
            {
                for (int j = 0; j < plan.Height; j++)
                {
                    var flags = plan.GetCell(i, j);
                    Color c = (flags & CellFlags.Wall) != 0 ? WallColor
                        : (flags & CellFlags.Dark) != 0 ? DarkColor
                        : (flags & CellFlags.Slick) != 0 ? SlickColor
                        : FloorColor;
                    // Slight checker so distance is readable on the blueprint.
                    if ((flags & CellFlags.Wall) == 0 && ((i + j) & 1) == 0) c *= 0.93f;
                    int b = verts.Count;
                    float x = i * s, y = j * s;
                    verts.Add(new Vector3(x, y));
                    verts.Add(new Vector3(x + s, y));
                    verts.Add(new Vector3(x + s, y + s));
                    verts.Add(new Vector3(x, y + s));
                    colors.Add(c); colors.Add(c); colors.Add(c); colors.Add(c);
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
                }
            }
            var mesh = new Mesh { name = "Floor grid" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
