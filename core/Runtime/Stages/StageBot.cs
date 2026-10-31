using System.Collections.Generic;
using LowObservable.Core.Maths;
using LowObservable.Core.World;

namespace LowObservable.Core.Stages
{
    public enum BotPolicy
    {
        /// <summary>Walks the route with no stealth at all. Used for reachability checks with sensors disabled.</summary>
        Walker,
        /// <summary>Sneaks, and waits while its next step is inside a visible camera or unit cone.</summary>
        Sneaker,
        /// <summary>Runs everywhere. Should get caught a lot; if it doesn't, the level has no pressure.</summary>
        Runner,
    }

    /// <summary>
    /// A simple player for headless checks: visits the stage's <c>botroute</c> markers in order (keycards,
    /// tools, terminals), then the nearest exit. It only uses what a player can see (map, cameras, visible
    /// units), never WARDEN internals beyond what the visor shows, and never unit positions out of sight.
    /// It's a smoke test and a rough difficulty probe, not a good player.
    /// </summary>
    public sealed class StageBot
    {
        private readonly Stage _stage;
        private readonly BotPolicy _policy;
        private readonly GridPathfinder _pathfinder;
        private readonly List<char> _route = new List<char>();
        private int _routeIndex;
        private List<Vec2> _path = new List<Vec2>();
        private int _pathIndex;
        private double _replan;
        private double _waited;
        private bool _interactWasDown;
        private int _lastCaptures;

        public StageBot(Stage stage, BotPolicy policy)
        {
            _stage = stage;
            _policy = policy;
            _pathfinder = new GridPathfinder(stage.Plan);
            foreach (char c in stage.Definition.BotRoute ?? "") _route.Add(c);
            if (_route.Count == 0 || _route[_route.Count - 1] != 'X') _route.Add('X');
        }

        public char CurrentGoalMarker => _routeIndex < _route.Count ? _route[_routeIndex] : 'X';

        private Vec2 GoalPosition()
        {
            char m = CurrentGoalMarker;
            if (m == 'X')
            {
                Vec2 best = _stage.Exits[0];
                foreach (var e in _stage.Exits)
                    if (Vec2.Distance(e, _stage.Player) < Vec2.Distance(best, _stage.Player)) best = e;
                return best;
            }
            return _stage.Definition.Markers[m];
        }

        private bool GoalDone()
        {
            char m = CurrentGoalMarker;
            foreach (var k in _stage.Keycards) if (k.Marker == m) return k.Taken;
            foreach (var p in _stage.Pickups) if (p.Marker == m) return p.Taken;
            foreach (var t in _stage.Terminals) if (t.Marker == m) return t.Forge || t.Done;
            foreach (var n in _stage.Notes) if (n.Marker == m) return n.Read;
            foreach (var d in _stage.Doors) if (d.Marker == m) return d.Open;
            return Vec2.Distance(GoalPosition(), _stage.Player) < 0.8;
        }

        public StageInput NextInput()
        {
            var input = new StageInput { Mode = _policy == BotPolicy.Runner ? MoveMode.Run : _policy == BotPolicy.Sneaker ? MoveMode.Sneak : MoveMode.Walk };
            if (_stage.Captures != _lastCaptures)
            {
                _lastCaptures = _stage.Captures;
                _path.Clear();
            }
            if (_stage.Hidden)
            {
                input.InteractPressed = true;
                return input;
            }
            while (_routeIndex < _route.Count - 1 && GoalDone()) { _routeIndex++; _path.Clear(); }

            var goal = GoalPosition();
            double dGoal = Vec2.Distance(goal, _stage.Player);

            // At an interactable goal: press (or hold, for terminals).
            if (CurrentGoalMarker != 'X' && dGoal <= _stage.Tuning.InteractRange * 0.9)
            {
                bool terminal = false;
                foreach (var t in _stage.Terminals) if (t.Marker == CurrentGoalMarker && !t.Forge) terminal = true;
                bool isDoor = false;
                foreach (var d in _stage.Doors) if (d.Marker == CurrentGoalMarker) isDoor = true;
                if (terminal) input.InteractHeld = true;
                else if (!isDoor)
                {
                    input.InteractPressed = !_interactWasDown;
                    _interactWasDown = input.InteractPressed;
                }
                if (!isDoor) return input;
            }
            _interactWasDown = false;

            _replan -= Stage.Dt;
            if (_path.Count == 0 || _replan <= 0)
            {
                _replan = 1.0;
                _path = _pathfinder.FindPath(_stage.Player, goal);
                _pathIndex = 0;
            }
            while (_pathIndex < _path.Count && Vec2.Distance(_path[_pathIndex], _stage.Player) < 0.35) _pathIndex++;
            if (_pathIndex >= _path.Count) return input;

            var next = _path[_pathIndex];
            if (_policy == BotPolicy.Sneaker)
            {
                var ahead = _stage.Player + (next - _stage.Player).Normalized * 1.0;
                if (_stage.IsWatched(ahead) && !_stage.IsWatched(_stage.Player) && _waited < 6.0)
                {
                    _waited += Stage.Dt;
                    return input;
                }
                if (!_stage.IsWatched(ahead)) _waited = 0;
            }
            input.Move = (next - _stage.Player).Normalized;
            return input;
        }

        /// <summary>Plays until escape or a time limit. Returns true if it escaped.</summary>
        public bool RunToEnd(double maxSeconds = 600)
        {
            while (_stage.Outcome == StageOutcome.Running && _stage.Time < maxSeconds) _stage.Step(NextInput());
            return _stage.Outcome == StageOutcome.Escaped;
        }
    }
}
