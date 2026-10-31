using System.Collections.Generic;
using UnityEngine;

namespace LowObservable.Unity.Views
{
    /// <summary>
    /// Placeholder-art helpers: an unlit vertex-colour material that works in both the built-in
    /// render pipeline and URP, plus line and quad factories. Everything the package draws goes
    /// through here, so swapping in real art later means changing this file and the views, not the sim.
    /// </summary>
    public static class ViewKit
    {
        private static Material _unlit;

        // Tried in order. "Sprites/Default" ships with the built-in pipeline and is in the Always Included
        // Shaders list; the URP sprite shader covers URP 2D projects.
        private static readonly string[] ShaderCandidates =
        {
            "Sprites/Default",
            "Universal Render Pipeline/2D/Sprite-Unlit-Default",
            "Universal Render Pipeline/Unlit",
            "Unlit/Color",
        };

        public static Material Unlit
        {
            get
            {
                if (_unlit != null) return _unlit;
                Shader shader = null;
                foreach (var name in ShaderCandidates)
                {
                    shader = Shader.Find(name);
                    if (shader != null) break;
                }
                if (shader == null)
                {
                    Debug.LogError("[LowObservable] No unlit shader found. Add 'Sprites/Default' to Project Settings > Graphics > Always Included Shaders.");
                    shader = Shader.Find("Hidden/InternalErrorShader");
                }
                _unlit = new Material(shader) { name = "LowObservable Unlit (generated)" };
                return _unlit;
            }
        }

        public static LineRenderer CreateLine(Transform parent, string name, Color color, float width, int sortingOrder, bool loop = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Unlit;
            line.useWorldSpace = true;
            line.loop = loop;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sortingOrder = sortingOrder;
            line.positionCount = 0;
            return line;
        }

        /// <summary>A coloured quad (or triangle when <paramref name="triangle"/> is true) centred on the origin.</summary>
        public static GameObject CreateShape(Transform parent, string name, Color color, float size, int sortingOrder, bool triangle = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Unlit;
            mr.sortingOrder = sortingOrder;
            mf.sharedMesh = triangle ? TriangleMesh(size, color) : QuadMesh(size, color);
            return go;
        }

        public static Mesh QuadMesh(float size, Color color)
        {
            float h = size * 0.5f;
            var mesh = new Mesh { name = "quad" };
            mesh.vertices = new[] { new Vector3(-h, -h), new Vector3(h, -h), new Vector3(h, h), new Vector3(-h, h) };
            mesh.colors = new[] { color, color, color, color };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Isoceles triangle pointing along +X: the drone's heading marker.</summary>
        public static Mesh TriangleMesh(float size, Color color)
        {
            var mesh = new Mesh { name = "triangle" };
            mesh.vertices = new[] { new Vector3(size * 0.6f, 0), new Vector3(-size * 0.4f, size * 0.4f), new Vector3(-size * 0.4f, -size * 0.4f) };
            mesh.colors = new[] { color, color, color };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Fills a reusable buffer with an ellipse outline and applies it to a line.</summary>
        public static void SetEllipse(LineRenderer line, List<Vector3> buffer, Core.Maths.CovarianceEllipse e, double sigmas, float z, int segments = 48)
        {
            buffer.Clear();
            for (int k = 0; k < segments; k++)
            {
                double t = k * 2.0 * System.Math.PI / segments;
                var p = e.PointAt(t, sigmas);
                buffer.Add(new Vector3((float)p.X, (float)p.Y, z));
            }
            line.loop = true;
            line.positionCount = buffer.Count;
            line.SetPositions(buffer.ToArray());
        }

        public static readonly Color[] AppearancePalette =
        {
            new Color(0.95f, 0.35f, 0.30f), // a red valve
            new Color(0.30f, 0.65f, 0.95f), // b blue panel
            new Color(0.95f, 0.80f, 0.25f), // c yellow pipe
            new Color(0.55f, 0.85f, 0.40f), // d green vent
            new Color(0.85f, 0.45f, 0.90f), // e purple console
            new Color(0.95f, 0.60f, 0.25f), // f orange tank
            new Color(0.40f, 0.90f, 0.85f), // g teal beacon
            new Color(0.90f, 0.90f, 0.90f), // h white sign
        };

        public static Color ColorForAppearance(int appearance)
        {
            if (appearance < 0) return Color.gray;
            return AppearancePalette[appearance % AppearancePalette.Length];
        }
    }
}
