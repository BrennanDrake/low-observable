using System;

namespace LowObservable.Core.Maths
{
    /// <summary>
    /// Immutable 2D vector in world meters. The core uses its own vector type (not UnityEngine.Vector2
    /// or System.Numerics) so it has no engine dependency and uses doubles for numerical stability.
    /// Convert at the Unity boundary with <c>UnityConvert</c> in the integration package.
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly double X;
        public readonly double Y;

        public Vec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0, 0);

        public double Length => System.Math.Sqrt(X * X + Y * Y);
        public double LengthSquared => X * X + Y * Y;

        public Vec2 Normalized
        {
            get
            {
                double len = Length;
                return len < 1e-12 ? Zero : new Vec2(X / len, Y / len);
            }
        }

        /// <summary>World-frame angle of this vector in radians, measured from +X.</summary>
        public double Angle => System.Math.Atan2(Y, X);

        public static Vec2 FromAngle(double radians, double length = 1.0)
        {
            return new Vec2(System.Math.Cos(radians) * length, System.Math.Sin(radians) * length);
        }

        public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        public Vec2 Rotated(double radians)
        {
            double c = System.Math.Cos(radians);
            double s = System.Math.Sin(radians);
            return new Vec2(c * X - s * Y, s * X + c * Y);
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, double s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(double s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, double s) => new Vec2(a.X / s, a.Y / s);

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
        public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
        public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);

        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }
}
