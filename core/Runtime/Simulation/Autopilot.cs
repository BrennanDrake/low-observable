using System.Collections.Generic;
using LowObservable.Core.Maths;
using LowObservable.Core.Robot;
using LowObservable.Core.World;

namespace LowObservable.Core.Simulation
{
    /// <summary>A play style for the autopilot. The headless simulator compares them to check balance.</summary>
    public sealed class AutopilotProfile
    {
        public string Name = "cautious";
        public MovementMode CruiseMode = MovementMode.Walk;
        /// <summary>Mode used when the Seeker's belief is concentrated near you.</summary>
        public MovementMode ThreatMode = MovementMode.Creep;
        /// <summary>Ping when your own position spread (meters) exceeds this. Infinity = never ping.</summary>
        public double PingSpread = 0.8;
        /// <summary>Only dock once your spread is below this, otherwise ping first.</summary>
        public double DockSpread = 0.35;
        public bool UseDecoys = true;
        /// <summary>Seeker probability mass within 3 m of you that counts as "it's onto me".</summary>
        public double ThreatMass = 0.35;

        public static AutopilotProfile Reckless() => new AutopilotProfile
        {
            Name = "reckless",
            CruiseMode = MovementMode.Sprint,
            ThreatMode = MovementMode.Sprint,
            PingSpread = 0.4,
            DockSpread = 0.5,
            UseDecoys = false,
            ThreatMass = 2.0,
        };

        public static AutopilotProfile Cautious() => new AutopilotProfile();

        public static AutopilotProfile Ghost() => new AutopilotProfile
        {
            Name = "ghost",
            CruiseMode = MovementMode.Creep,
            ThreatMode = MovementMode.Creep,
            PingSpread = 1.4,
            DockSpread = 0.4,
            UseDecoys = true,
            ThreatMass = 0.25,
        };

        /// <summary>Never pings: odometry plus the passive camera only, and feels for the uplink by trying to dock.</summary>
        public static AutopilotProfile Passive() => new AutopilotProfile
        {
            Name = "passive",
            CruiseMode = MovementMode.Walk,
            ThreatMode = MovementMode.Walk,
            PingSpread = double.PositiveInfinity,
            DockSpread = double.PositiveInfinity,
            UseDecoys = false,
            ThreatMass = 2.0,
        };

        public static AutopilotProfile ByName(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "reckless": return Reckless();
                case "ghost": return Ghost();
                case "passive":
                case "blind": return Passive();
                default: return Cautious();
            }
        }
    }

    /// <summary>
    /// A simple bot that plays an <see cref="Episode"/> using only what a player could know: the
    /// blueprint, its own belief, and the Seeker's visible belief cloud. It never reads ground truth.
    /// Used by the headless simulator for balance sweeps, by tests, and could drive an attract mode.
    /// </summary>
    public sealed class Autopilot
    {
        private readonly AutopilotProfile _profile;
        private readonly GridPathfinder _pathfinder;
        private readonly FloorPlan _plan;
        private List<Vec2> _path = new List<Vec2>();
        private int _index;
        private double _replanTimer;
        private double _decoyCooldown;
        private int _failedDocks;

        public Autopilot(FloorPlan plan, AutopilotProfile profile)
        {
            _plan = plan;
            _profile = profile;
            _pathfinder = new GridPathfinder(plan);
        }

        public AutopilotProfile Profile => _profile;

        public PlayerCommand Decide(Episode episode)
        {
            if (episode.AwaitingAssociation)
            {
                // A human would choose here; the bot takes the most likely candidate.
                episode.ResolvePending(episode.CurrentPending.Candidates[0].Landmark.Id);
            }

            double dt = episode.FixedDt;
            _replanTimer -= dt;
            _decoyCooldown -= dt;

            var belief = episode.Filter.Mean;
            double spread = episode.Filter.PositionSpread;
            double threat = episode.Seeker.Belief.MassWithin(belief.Position, 3.0);
            var mode = threat >= _profile.ThreatMass ? _profile.ThreatMode : _profile.CruiseMode;
            var command = PlayerCommand.Drive(0, 0, mode);

            foreach (var e in episode.TickEvents)
            {
                if (e.Type != EpisodeEventType.DockFailed) continue;
                _failedDocks++;
                _replanTimer = 0;
            }

            // After a failed dock the bot knows it is not where it thinks. Probe points around the uplink in a
            // widening ring, the way a player feels for a doorway in the dark.
            Vec2 goal = _plan.Uplink;
            if (_failedDocks > 0)
            {
                double ring = 0.5 + 0.25 * (_failedDocks / 8);
                goal = _plan.Uplink + Vec2.FromAngle(_failedDocks * Angle.TwoPi / 8.0, ring);
                if (_plan.IsWallAt(goal)) goal = _plan.Uplink;
            }

            if (_replanTimer <= 0 || _index >= _path.Count)
            {
                _replanTimer = 1.0;
                _path = _pathfinder.FindPath(belief.Position, goal);
                _index = 0;
            }

            double toUplink = Vec2.Distance(belief.Position, goal);
            if (toUplink < 0.35)
            {
                if (spread <= _profile.DockSpread) return command.With(PlayerAction.Dock);
                if (episode.PingCooldownRemaining <= 0) return command.With(PlayerAction.Ping);
                return command;
            }

            if (spread > _profile.PingSpread && episode.PingCooldownRemaining <= 0)
                command.Action = PlayerAction.Ping;
            else if (_profile.UseDecoys && episode.DecoysLeft > 0 && _decoyCooldown <= 0 && threat >= _profile.ThreatMass)
            {
                _decoyCooldown = 6.0;
                command.Action = PlayerAction.ThrowDecoy;
                command.DecoyTargetLocal = new Vec2(-1.0, 4.0); // behind and to the left
            }

            while (_index < _path.Count && Vec2.Distance(belief.Position, _path[_index]) < 0.3) _index++;
            if (_index >= _path.Count) return command;

            var target = _path[_index];
            double bearing = Angle.Difference((target - belief.Position).Angle, belief.Theta);
            command.Turn = System.Math.Max(-1, System.Math.Min(1, bearing * 2.0));
            command.Forward = System.Math.Abs(bearing) < 0.6 ? 1.0 : 0.0;
            return command;
        }

        /// <summary>Runs an episode to completion with this autopilot. Returns the outcome.</summary>
        public EpisodeOutcome RunToEnd(Episode episode, int maxTicks = int.MaxValue)
        {
            int ticks = 0;
            while (episode.Outcome == EpisodeOutcome.Running && ticks++ < maxTicks)
                episode.Step(Decide(episode));
            return episode.Outcome;
        }
    }
}
