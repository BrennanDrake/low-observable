namespace LowObservable.Core.Maths
{
    /// <summary>A planar pose: position in meters plus heading in radians (counter-clockwise from +X).</summary>
    public readonly struct Pose2
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Theta;

        public Pose2(double x, double y, double theta)
        {
            X = x;
            Y = y;
            Theta = Angle.Wrap(theta);
        }

        public Pose2(Vec2 position, double theta) : this(position.X, position.Y, theta) { }

        public Vec2 Position => new Vec2(X, Y);
        public Vec2 Forward => Vec2.FromAngle(Theta);

        /// <summary>Transforms a point from this pose's local frame into the world frame.</summary>
        public Vec2 LocalToWorld(Vec2 local) => Position + local.Rotated(Theta);

        /// <summary>Transforms a world point into this pose's local frame.</summary>
        public Vec2 WorldToLocal(Vec2 world) => (world - Position).Rotated(-Theta);

        public Pose2 WithPosition(Vec2 p) => new Pose2(p.X, p.Y, Theta);

        public override string ToString() => $"({X:0.###}, {Y:0.###}, {Theta * Angle.Rad2Deg:0.#}°)";
    }
}
