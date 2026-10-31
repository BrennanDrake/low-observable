using LowObservable.Core.Maths;
using LowObservable.Core.Stages;
using LowObservable.Unity.Views;
using UnityEngine;

namespace LowObservable.Unity.Escape
{
    /// <summary>
    /// C-7, where they really are, plus a preview of the selected tool at the cursor: where a throw lands,
    /// where a jammer field or mirror would go (and at what angle), or where a forged sighting would be
    /// planted. The preview is green when the tool can be used and grey when it can't.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        public EscapeGame Game;
        public float Size = 0.55f;

        private GameObject _body;
        private GameObject _heading;
        private LineRenderer _aimLine;
        private LineRenderer _aimShape;
        private LineRenderer _mirrorPreview;
        private LineRenderer _stamina;
        private float _flashUntil;
        private Color _bodyColor = EscapePalette.Player;

        private void Start()
        {
            if (Game == null) Game = FindAnyObjectByType<EscapeGame>();
            _body = ViewKit.CreateShape(transform, "Player", EscapePalette.Player, Size, EscapeLayers.Player);
            _body.transform.rotation = Quaternion.Euler(0, 0, 45f);
            _heading = ViewKit.CreateShape(transform, "Player heading", EscapePalette.Player, Size * 0.9f, EscapeLayers.Player, triangle: true);
            _aimLine = ViewKit.CreateLine(transform, "Aim line", EscapePalette.AimOk, 0.03f, EscapeLayers.Aim);
            _aimShape = ViewKit.CreateLine(transform, "Aim target", EscapePalette.AimOk, 0.05f, EscapeLayers.Aim, true);
            _mirrorPreview = ViewKit.CreateLine(transform, "Mirror preview", EscapePalette.Mirror, 0.12f, EscapeLayers.Aim);
            _stamina = ViewKit.CreateLine(transform, "Stamina", new Color(1f, 1f, 1f, 0.8f), 0.07f, EscapeLayers.Aim);
            Game.EventRaised += e => { if (e.Type == StageEventType.Captured) _flashUntil = Time.time + 1.0f; };
        }

        private void LateUpdate()
        {
            var stage = Game.Stage;
            bool on = stage != null && Game.Screen != EscapeScreen.Menu;
            _body.SetActive(on);
            _heading.SetActive(on);
            if (!on)
            {
                _aimLine.enabled = _aimShape.enabled = _mirrorPreview.enabled = _stamina.enabled = false;
                return;
            }

            var p = stage.Player.ToVector3();
            bool flash = Time.time < _flashUntil && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f;
            var color = flash ? EscapePalette.Alarm : stage.Hidden ? EscapePalette.PlayerHidden : EscapePalette.Player;
            if (color != _bodyColor)
            {
                EscapeDraw.SetColor(_body, color);
                EscapeDraw.SetColor(_heading, color);
                _bodyColor = color;
            }
            float scale = stage.Hidden ? 0.6f : stage.Mode == MoveMode.Sneak ? 0.85f : 1f;
            _body.transform.position = p;
            _body.transform.localScale = new Vector3(scale, scale, 1f);
            _heading.SetActive(!stage.Hidden);
            _heading.transform.position = p + (Vector3)(Vec2.FromAngle(stage.PlayerHeading) * (Size * 0.45)).ToVector2();
            _heading.transform.rotation = UnityConvert.HeadingRotation(stage.PlayerHeading);
            _heading.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            // Stamina: a bar under the player that only shows while it is not full.
            bool tired = stage.Stamina < 0.999;
            _stamina.enabled = tired;
            if (tired)
            {
                float w = 0.8f * (float)stage.Stamina;
                EscapeDraw.SetColor(_stamina, stage.Stamina < 0.25 ? EscapePalette.Alarm : new Color(1f, 1f, 1f, 0.8f));
                EscapeDraw.Segment(_stamina, p + new Vector3(-0.4f, -0.55f), p + new Vector3(-0.4f + w, -0.55f));
            }

            DrawAim(stage);
        }

        private void DrawAim(Stage stage)
        {
            var input = Game.Input;
            bool playing = Game.Screen == EscapeScreen.Playing && input != null && !stage.Hidden;
            var tool = input != null ? input.SelectedTool : ToolKind.Throw;
            if (!playing || !stage.HasTool(tool) || (tool == ToolKind.Throw && stage.Carried == 0))
            {
                _aimLine.enabled = _aimShape.enabled = _mirrorPreview.enabled = false;
                return;
            }
            var aim = new Vec2(input.CursorWorld.x, input.CursorWorld.y);
            var target = stage.ToolTarget(tool, aim);
            bool ok = stage.ToolBlockedReason(tool, aim) == null;
            var c = ok ? EscapePalette.AimOk : EscapePalette.AimBlocked;
            EscapeDraw.SetColor(_aimLine, c);
            EscapeDraw.SetColor(_aimShape, c);
            _aimLine.enabled = true;
            _aimShape.enabled = true;
            EscapeDraw.Segment(_aimLine, stage.Player.ToVector3(), target.ToVector3());

            switch (tool)
            {
                case ToolKind.Jammer:
                    EscapeDraw.Circle(_aimShape, target.ToVector3(), (float)stage.Tuning.JammerRadius, 48);
                    _mirrorPreview.enabled = false;
                    break;
                case ToolKind.Mirror:
                    EscapeDraw.Circle(_aimShape, target.ToVector3(), 0.15f, 12);
                    _mirrorPreview.enabled = true;
                    var half = Vec2.FromAngle(input.MirrorAngle, stage.Tuning.MirrorLength * 0.5);
                    var mc = EscapePalette.Mirror;
                    mc.a = ok ? 0.8f : 0.3f;
                    EscapeDraw.SetColor(_mirrorPreview, mc);
                    EscapeDraw.Segment(_mirrorPreview, (target - half).ToVector3(), (target + half).ToVector3());
                    break;
                case ToolKind.Forge:
                    EscapeDraw.Circle(_aimShape, target.ToVector3(), 0.4f, 4);
                    _mirrorPreview.enabled = false;
                    break;
                default:
                    EscapeDraw.Circle(_aimShape, target.ToVector3(), 0.25f, 16);
                    _mirrorPreview.enabled = false;
                    break;
            }
        }
    }
}
