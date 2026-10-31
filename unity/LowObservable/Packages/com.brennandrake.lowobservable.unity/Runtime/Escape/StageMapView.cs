using System.Collections.Generic;
using LowObservable.Core.Stages;
using LowObservable.Core.World;
using LowObservable.Unity.Views;
using UnityEngine;

namespace LowObservable.Unity.Escape
{
    /// <summary>Draw order for the escape views (higher draws on top).</summary>
    public static class EscapeLayers
    {
        public const int Floor = 0;
        public const int Props = 10;
        public const int Cones = 15;
        public const int Belief = 20;
        public const int BeliefMarks = 25;
        public const int Gadgets = 30;
        public const int Units = 40;
        public const int Sound = 45;
        public const int Player = 50;
        public const int Aim = 60;
    }

    /// <summary>
    /// The facility as the player sees it: floor tinted by zone, walls, dark cells, closed doors, and every
    /// prop (exits, lockers, checkpoints, throwables, notes, terminals, keycards, tool crates, light switches).
    /// The floor mesh is rebuilt when a door opens or a light circuit flips, since both change the plan.
    /// </summary>
    public sealed class StageMapView : MonoBehaviour
    {
        public EscapeGame Game;
        public Color WallColor = new Color(0.09f, 0.10f, 0.12f);
        public Color DarkColor = new Color(0.12f, 0.13f, 0.19f);

        private Stage _built;
        private bool _dirty;
        private GameObject _floor;
        private Mesh _floorMesh;
        private ShapePool _props;
        private ShapePool _propsSmall;
        private float _pulse;

        private void Start()
        {
            if (Game == null) Game = FindAnyObjectByType<EscapeGame>();
            _floor = EscapeDraw.CreateMeshObject(transform, "Floor", EscapeLayers.Floor, out _floorMesh);
            _props = new ShapePool(transform, "Prop", 0.6f, EscapeLayers.Props);
            _propsSmall = new ShapePool(transform, "Prop small", 0.3f, EscapeLayers.Props + 1);
            Game.EventRaised += OnEvent;
        }

        private void OnDestroy()
        {
            if (Game != null) Game.EventRaised -= OnEvent;
        }

        private void OnEvent(StageEvent e)
        {
            if (e.Type == StageEventType.DoorOpened || e.Type == StageEventType.LightsToggled) _dirty = true;
        }

        private void LateUpdate()
        {
            var stage = Game.Stage;
            if (stage == null) { _floor.SetActive(false); return; }
            _floor.SetActive(true);
            if (stage != _built || _dirty)
            {
                BuildFloor(stage);
                _built = stage;
                _dirty = false;
            }
            _pulse += Time.deltaTime;
            DrawProps(stage);
        }

        private void BuildFloor(Stage stage)
        {
            var plan = stage.Plan;
            var floor = EscapePalette.ZoneFloor(stage.Definition.Zone);
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            float s = (float)plan.CellSize;
            for (int i = 0; i < plan.Width; i++)
            {
                for (int j = 0; j < plan.Height; j++)
                {
                    var flags = plan.GetCell(i, j);
                    Color c = (flags & CellFlags.Wall) != 0 ? WallColor : (flags & CellFlags.Dark) != 0 ? DarkColor : floor;
                    if ((flags & CellFlags.Wall) == 0 && ((i + j) & 1) == 0) c *= 0.94f;
                    c.a = 1f;
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
            _floorMesh.Clear();
            if (verts.Count > 65000) _floorMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _floorMesh.SetVertices(verts);
            _floorMesh.SetColors(colors);
            _floorMesh.SetTriangles(tris, 0);
            _floorMesh.RecalculateBounds();
        }

        private void DrawProps(Stage stage)
        {
            _props.Begin();
            _propsSmall.Begin();
            float beat = 1f + 0.12f * Mathf.Sin(_pulse * 4f);

            foreach (var x in stage.Exits) _props.Next(EscapePalette.Exit, x.ToVector3(), 0f, 1.4f * beat);
            for (int k = 0; k < stage.HideSpots.Count; k++)
            {
                var c = stage.HideSpotIndex == k ? EscapePalette.PlayerHidden : EscapePalette.Hide;
                _props.Next(c, stage.HideSpots[k].ToVector3(), 0f, 1.1f);
            }
            foreach (var c in stage.Checkpoints) _propsSmall.Next(EscapePalette.Checkpoint, c.ToVector3(), 45f, 1.3f);
            foreach (var t in stage.Throwables) if (!t.Taken) _propsSmall.Next(EscapePalette.Throwable, t.Position.ToVector3(), 0f, 0.8f);
            foreach (var n in stage.Notes)
            {
                var c = n.Read ? EscapePalette.Note * 0.6f : EscapePalette.Note;
                c.a = 1f;
                _propsSmall.Next(c, n.Position.ToVector3(), 10f, n.Read ? 1f : 1.2f * beat);
            }
            foreach (var t in stage.Terminals)
            {
                var c = t.Forge ? (t.ForgeUsed ? EscapePalette.Jammed : EscapePalette.ForgeTerminal)
                    : (t.Done ? EscapePalette.Exit : EscapePalette.Terminal);
                _props.Next(c, t.Position.ToVector3(), 0f, 0.9f);
            }
            foreach (var k in stage.Keycards) if (!k.Taken) _propsSmall.Next(EscapePalette.Keycard, k.Position.ToVector3(), 45f, 1.1f * beat);
            foreach (var p in stage.Pickups) if (!p.Taken) _props.Next(EscapePalette.Pickup, p.Position.ToVector3(), 45f, 0.9f * beat);
            foreach (var sw in stage.Switches)
            {
                var c = stage.CircuitToggled(sw.Circuit) ? EscapePalette.Switch * 0.5f : EscapePalette.Switch;
                c.a = 1f;
                _propsSmall.Next(c, sw.Position.ToVector3(), 0f, 0.6f);
            }
            foreach (var d in stage.Doors)
            {
                if (d.Open) continue;
                var c = string.IsNullOrEmpty(d.Key) ? EscapePalette.Door : Color.Lerp(EscapePalette.Door, EscapePalette.Keycard, stage.Keys.Contains(d.Key) ? 0.7f : 0.25f);
                _props.Next(c, d.Position.ToVector3(), 0f, 1.6f);
            }
            _props.End();
            _propsSmall.End();
        }
    }
}
