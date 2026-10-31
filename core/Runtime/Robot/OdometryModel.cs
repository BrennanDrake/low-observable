using LowObservable.Core.Maths;

namespace LowObservable.Core.Robot
{
    /// <summary>
    /// One tick of odometry: how far the wheels say the drone moved and turned.
    /// This is the "control input" u of the filter's prediction step.
    /// </summary>
    public readonly struct OdometryDelta
    {
        public readonly double Distance;
        public readonly double Rotation;

        public OdometryDelta(double distance, double rotation)
        {
            Distance = distance;
            Rotation = rotation;
        }

        public bool IsZero => Distance == 0.0 && Rotation == 0.0;
    }

    /// <summary>
    /// Motion model shared by the true world and the player's filter. Integrates an odometry
    /// delta with a midpoint heading (rotate half, translate, rotate half), and defines the noise
    /// covariance M of that delta. Using the same equations for truth and belief keeps the filter
    /// consistent; deliberate mismatches (slick floors) are what make it inconsistent.
    /// </summary>
    public static class OdometryModel
    {
        public static Pose2 Apply(Pose2 pose, OdometryDelta u)
        {
            double mid = pose.Theta + 0.5 * u.Rotation;
            return new Pose2(
                pose.X + u.Distance * System.Math.Cos(mid),
                pose.Y + u.Distance * System.Math.Sin(mid),
                pose.Theta + u.Rotation);
        }

        /// <summary>Variance of the distance and rotation components for a delta, given a noise scale.</summary>
        public static void NoiseVariances(OdometryDelta u, double scale,
            double distVarPerMeter, double headingVarPerMeter, double headingVarPerRadian,
            out double distanceVariance, out double rotationVariance)
        {
            double d = System.Math.Abs(u.Distance);
            double r = System.Math.Abs(u.Rotation);
            distanceVariance = scale * distVarPerMeter * d + 1e-10;
            rotationVariance = scale * (headingVarPerMeter * d + headingVarPerRadian * r) + 1e-10;
        }

        /// <summary>Jacobian of <see cref="Apply"/> with respect to the pose (3x3).</summary>
        public static Matrix JacobianState(Pose2 pose, OdometryDelta u)
        {
            double mid = pose.Theta + 0.5 * u.Rotation;
            return new Matrix(3, 3,
                1, 0, -u.Distance * System.Math.Sin(mid),
                0, 1, u.Distance * System.Math.Cos(mid),
                0, 0, 1);
        }

        /// <summary>Jacobian of <see cref="Apply"/> with respect to the control (3x2: distance, rotation).</summary>
        public static Matrix JacobianControl(Pose2 pose, OdometryDelta u)
        {
            double mid = pose.Theta + 0.5 * u.Rotation;
            return new Matrix(3, 2,
                System.Math.Cos(mid), -0.5 * u.Distance * System.Math.Sin(mid),
                System.Math.Sin(mid), 0.5 * u.Distance * System.Math.Cos(mid),
                0, 1);
        }
    }
}
