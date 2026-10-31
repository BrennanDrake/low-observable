namespace LowObservable.Core.Tracking
{
    /// <summary>
    /// The in-world "tracking spec" sheet a WARDEN terminal shows at the start of each zone. Plain words
    /// first; the real terms and equations live in <see cref="Detail"/>, which the UI keeps collapsed
    /// until the player has found some of the tracker's exploits.
    /// </summary>
    public sealed class TrackerSpec
    {
        public TrackerKind Kind;
        public string ProductName;
        public string Tagline;
        public string[] HowItWorks;
        public string AlgorithmName;
        public string Detail;
    }

    public static class TrackerSpecs
    {
        public static TrackerSpec For(TrackerKind kind)
        {
            switch (kind)
            {
                case TrackerKind.LastKnown:
                    return new TrackerSpec
                    {
                        Kind = kind,
                        ProductName = "SIGHTLINE v1",
                        Tagline = "Remembers where you were last seen. Sends a unit there.",
                        HowItWorks = new[]
                        {
                            "Every report (camera, microphone, unit) moves the marker to that spot.",
                            "Every report is trusted completely, whatever the sensor.",
                            "If a sensor looks at the marker and you're not there, the trail is lost.",
                        },
                        AlgorithmName = "Last-known position (point estimate)",
                        Detail = "x̂ = z_latest. No uncertainty, no motion model. The estimate never moves on its own.",
                    };
                case TrackerKind.OccupancyGrid:
                    return new TrackerSpec
                    {
                        Kind = kind,
                        ProductName = "HEATMAP v2",
                        Tagline = "Keeps a probability for every square metre of floor.",
                        HowItWorks = new[]
                        {
                            "While you're unobserved, the heat spreads outward, blocked by walls.",
                            "A report makes the squares near it hot and the rest cooler.",
                            "Squares a camera or unit can see, and doesn't find you in, go cold.",
                        },
                        AlgorithmName = "Occupancy grid (histogram Bayes filter)",
                        Detail = "Predict: p ← diffuse(p). Update: p(c) ∝ p(c)·p(z|c). Miss: p(c) ← p(c)·ε for every cell c the sensor covers, then normalize.",
                    };
                case TrackerKind.Kalman:
                    return new TrackerSpec
                    {
                        Kind = kind,
                        ProductName = "KESTREL v3",
                        Tagline = "Predicts your path. One smooth estimate, always.",
                        HowItWorks = new[]
                        {
                            "Keeps one best guess with an uncertainty ellipse, plus your speed and direction.",
                            "Assumes you keep moving the way you were going.",
                            "Ignores reports that fall too far outside its ellipse as glitches.",
                        },
                        AlgorithmName = "Kalman filter (constant-velocity model, Mahalanobis gating)",
                        Detail = "x = [p, v]. Predict: x ← Fx, P ← FPFᵀ + Q. Update: K = PHᵀ(HPHᵀ + R)⁻¹, x ← x + K(z − Hx), P ← (I − KH)P. Reject z when (z − Hx)ᵀS⁻¹(z − Hx) > γ.",
                    };
                default:
                    return new TrackerSpec
                    {
                        Kind = kind,
                        ProductName = "SWARM v4",
                        Tagline = "Hundreds of guesses at once. The likely ones multiply.",
                        HowItWorks = new[]
                        {
                            "Each dot is one guess of where you are; dots drift while you're unobserved.",
                            "A report makes dots near it survive and copy themselves; distant dots die out.",
                            "Dots a sensor can see, and doesn't find you at, are removed. A few random dots are added back over time.",
                        },
                        AlgorithmName = "Particle filter (sequential importance resampling)",
                        Detail = "Predict: xᵢ ← xᵢ + noise. Weight: wᵢ ∝ wᵢ·p(z|xᵢ). Resample when N_eff = 1/Σwᵢ² < N/2. Inject a small fraction of uniform particles each second.",
                    };
            }
        }
    }
}
