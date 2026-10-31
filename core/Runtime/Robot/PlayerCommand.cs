using LowObservable.Core.Maths;

namespace LowObservable.Core.Robot
{
    public enum MovementMode
    {
        /// <summary>Silent, precise, slow.</summary>
        Creep,
        /// <summary>Balanced.</summary>
        Walk,
        /// <summary>Fast, loud, and the wheels slip.</summary>
        Sprint,
    }

    public enum PlayerAction
    {
        None,
        /// <summary>Active range-and-bearing sweep: a precise fix, but every hunter in earshot hears it.</summary>
        Ping,
        /// <summary>Stop and listen for the Seeker: a noisy bearing to it, at the cost of standing still.</summary>
        Listen,
        /// <summary>Throw a decoy that broadcasts a fake ping signature.</summary>
        ThrowDecoy,
        /// <summary>Try to dock at the uplink. Succeeds only if you are truly there.</summary>
        Dock,
    }

    /// <summary>
    /// One tick of player intent. Built by whatever drives the player: Unity input, the autopilot, or a test.
    /// Movement is unicycle-style (forward and turn), which is how a differential-drive robot moves.
    /// </summary>
    public struct PlayerCommand
    {
        /// <summary>-1..1, fraction of the current mode's speed (negative reverses).</summary>
        public double Forward;
        /// <summary>-1..1, fraction of the turn rate (positive turns counter-clockwise / left).</summary>
        public double Turn;
        public MovementMode Mode;
        public PlayerAction Action;

        /// <summary>For ThrowDecoy: target in the drone's own frame (x forward, y left), meters.</summary>
        public Vec2 DecoyTargetLocal;

        public static PlayerCommand Idle => new PlayerCommand { Mode = MovementMode.Walk };

        public static PlayerCommand Drive(double forward, double turn, MovementMode mode = MovementMode.Walk)
        {
            return new PlayerCommand { Forward = forward, Turn = turn, Mode = mode };
        }

        public PlayerCommand With(PlayerAction action)
        {
            var c = this;
            c.Action = action;
            return c;
        }
    }
}
