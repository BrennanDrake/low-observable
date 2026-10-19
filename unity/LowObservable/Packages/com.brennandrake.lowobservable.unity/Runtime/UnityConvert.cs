using LowObservable.Core.Maths;
using UnityEngine;

namespace LowObservable.Unity
{
    /// <summary>
    /// The only place core types meet Unity types. The game plays on the XY plane (z is depth for
    /// draw order), matching Unity's 2D convention, and the core's +Y-up world frame.
    /// </summary>
    public static class UnityConvert
    {
        public static Vector3 ToVector3(this Vec2 v, float z = 0f) => new Vector3((float)v.X, (float)v.Y, z);
        public static Vector2 ToVector2(this Vec2 v) => new Vector2((float)v.X, (float)v.Y);
        public static Vec2 ToVec2(this Vector2 v) => new Vec2(v.x, v.y);
        public static Vec2 ToVec2(this Vector3 v) => new Vec2(v.x, v.y);

        /// <summary>Rotation that points a sprite's +X (right) along a core heading.</summary>
        public static Quaternion HeadingRotation(double theta) => Quaternion.Euler(0f, 0f, (float)(theta * Angle.Rad2Deg));
    }

    /// <summary>Draw order for everything this package spawns (higher draws on top).</summary>
    public static class Layers
    {
        public const int Floor = 0;
        public const int Landmarks = 10;
        public const int SeekerCloud = 20;
        public const int Decoys = 25;
        public const int BeliefEllipse = 30;
        public const int Drone = 40;
        public const int Seeker = 45;
        public const int Truth = 60;
        public const int Reveal = 80;
    }
}
