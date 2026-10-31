using System.Collections.Generic;
using LowObservable.Core.Maths;

namespace LowObservable.Core.Stages
{
    /// <summary>A fixed or swivelling security camera. Reports sightings and sweeps negative information.</summary>
    public sealed class SecurityCamera
    {
        public int Index;
        public char Marker;
        public Vec2 Position;
        public bool Swivel;
        /// <summary>Fixed cameras: their heading. Swivels: one end of the sweep.</summary>
        public double From;
        public double To;
        public double Period;
        public double Phase;
        public double FieldOfView;
        public double Range;
        public double Heading;
        public double CheckTimer;
        /// <summary>Seconds since this camera last saw the player (for the red blink).</summary>
        public double SinceAlarm = 999;
        public bool Jammed;

        public void Update(double time)
        {
            if (!Swivel)
            {
                Heading = From;
                return;
            }
            // Smooth back-and-forth: s goes 0 → 1 → 0 over one period, easing at the ends like a servo.
            double s = 0.5 - 0.5 * System.Math.Cos(2 * System.Math.PI * (time / Period + Phase));
            Heading = Angle.Wrap(From + Angle.Difference(To, From) * s);
        }
    }

    public sealed class Microphone
    {
        public int Index;
        public char Marker;
        public Vec2 Position;
        /// <summary>Multiplier on a sound's audible radius. 1 = as good as a patrol unit's ears.</summary>
        public double Sensitivity;
        public double SinceHeard = 999;
        public bool Jammed;
    }

    public sealed class Laser
    {
        public int Index;
        public char Marker;
        public Vec2 A;
        public Vec2 B;
        public double SinceTripped = 999;
        public bool Jammed;
    }

    public enum GuardMode
    {
        Patrol,
        Investigate,
        Search,
        Chase,
        CheckLocker,
    }

    /// <summary>A patrol robot. Walks a route, investigates where WARDEN sends it, chases what it sees, and catches on contact.</summary>
    public sealed class PatrolUnit
    {
        public int Index;
        public char Marker;
        public Pose2 Pose;
        public double LookHeading;
        public GuardMode Mode = GuardMode.Patrol;
        public readonly List<Vec2> Route = new List<Vec2>();
        public int RouteIndex;
        public List<Vec2> Path = new List<Vec2>();
        public int PathIndex;
        public Vec2 Goal;
        public Vec2 PathGoal;
        public double ReplanTimer;
        public double VisionTimer;
        public double SearchTimer;
        public Vec2 SearchCenter;
        public double ChaseTimer;
        public Vec2 ChaseTarget;
        public Vec2 LockerTarget;
        public double LockerTimer;
        public readonly HashSet<int> CheckedLockers = new HashSet<int>();
        public int LockerIndex = -1;
        public double ScanPhase;
        public double SpeedScale = 1.0;
        public bool Jammed;
        /// <summary>What WARDEN's last dispatch was based on (for the exploit journal).</summary>
        public ReportOrigin AssignedOrigin;
        public bool SawPlayerLastCheck;
    }

    /// <summary>A janitorial roomba. Harmless, but reports anything it bumps into.</summary>
    public sealed class Roomba
    {
        public int Index;
        public char Marker;
        public Vec2 Position;
        public double Heading;
        public double TurnTimer;
        public double Cooldown;
        public double SinceBump = 999;
        public bool Jammed;
        /// <summary>Optional costume from the stage file (e.g. "party").</summary>
        public string Hat = "";
    }
}
