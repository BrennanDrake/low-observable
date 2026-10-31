# Estimation primer

The math behind each mechanic, at the level needed to change it confidently. Every section points
at the code that implements it. Notation follows *Probabilistic Robotics* (Thrun, Burgard, Fox).

## 1. The Bayes filter, which every belief in the game is

A belief is a probability distribution over the state, bel(x). It changes in two ways:

- **Predict** (you moved): bel⁻(xₜ) = ∫ p(xₜ | uₜ, xₜ₋₁) bel(xₜ₋₁) dxₜ₋₁. Uncertainty spreads.
- **Update** (you sensed): bel(xₜ) ∝ p(zₜ | xₜ) bel⁻(xₜ). Uncertainty shrinks where the measurement agrees.

In game terms: **moving smears the blue ellipse; sensing squeezes it.** The two filters in the game are
two ways of representing bel(x): a Gaussian (EKF) for the player and a cloud of samples (particle
filter) for the Seeker.

## 2. Motion model (`Robot/OdometryModel.cs`)

The drone reports odometry per tick: distance d and rotation r. The pose integrates with a
midpoint heading:

```
x' = x + d cos(θ + r/2)
y' = y + d sin(θ + r/2)
θ' = θ + r
```

Noise grows with how far and how much you turned (variance per meter, per radian), scaled by
movement mode, and much more on slick floor:

```
σ_d² = k · a_d · |d|              σ_r² = k · (a_θd · |d| + a_θr · |r|)
```

Using variance proportional to distance (not per tick) keeps the noise independent of the frame
rate. **Truth** draws real noise with the scale for the floor the drone is really on. **The filter**
assumes the scale for the floor it *thinks* it is on. On unknown ice the filter is overconfident,
which `EkfTests.OverconfidentFilterHasHighNees` demonstrates.

## 3. The EKF: the player's belief (`Estimation/EkfLocalizer.cs`)

State x = (x, y, θ), covariance P (3×3).

**Predict.** Linearise the motion model around the mean:

```
G = ∂f/∂x = [1 0 -d sin(θ+r/2); 0 1 d cos(θ+r/2); 0 0 1]
V = ∂f/∂u = [cos(θ+r/2) -d/2 sin(θ+r/2); sin(θ+r/2) d/2 cos(θ+r/2); 0 1]
P ← G P Gᵀ + V M Vᵀ,   M = diag(σ_d², σ_r²)
```

The off-diagonal terms are why the ellipse turns into a sideways sliver when you drive straight
with an uncertain heading (`EkfTests.HeadingErrorMakesLateralUncertaintyGrow`): heading error turns
into lateral position error.

**Update** with a range-bearing measurement z = (ρ, φ) of a landmark at m:

```
ẑ = ( |m - p| , atan2(m_y - y, m_x - x) - θ )
H = [-dx/ρ  -dy/ρ  0 ;  dy/ρ²  -dx/ρ²  -1]
ν = z - ẑ          (bearing wrapped to (-π, π]!)
S = H P Hᵀ + R
K = P Hᵀ S⁻¹
x ← x + K ν
P ← (I - K H) P (I - K H)ᵀ + K R Kᵀ      (Joseph form)
```

Two details that matter in practice:

- **Wrap the bearing innovation.** 179° vs -179° is a 2° error, not 358°. `Angle.Wrap` everywhere;
  `EkfTests.BearingInnovationIsWrapped` guards it.
- **Joseph form** keeps P symmetric positive definite even with a slightly wrong gain, which happens
  when a player feeds the filter a bad association. `Symmetrized()` mops up round-off.

**Sensor shapes.** The ping has small range noise (5 cm) and small bearing noise (1°): it squeezes
the ellipse hard in every direction. The camera has bearing noise of 2° but range noise that grows
with distance: one camera fix squeezes the ellipse *across* the line of sight much more than *along*
it. Two landmarks at different bearings beat one landmark measured twice.

## 4. Data association (`Estimation/DataAssociation.cs`)

A measurement doesn't say which landmark it came from. For each candidate landmark (same appearance,
when the camera reports one) compute the innovation and its **squared Mahalanobis distance**
d² = νᵀ S⁻¹ ν. If the measurement really came from that landmark, d² follows a chi-square
distribution with 2 degrees of freedom, so:

- **Gate:** keep candidates with d² ≤ 9.21 (99% of correct matches pass).
- **Pick:** smallest d².
- **Ambiguity:** if the likelihood ratio between best and second-best, exp((d₂² - d₁²)/2), is below
  3, the match is ambiguous and the player is asked.

The gate is in units of the belief's own uncertainty. A lost drone (big P, big S) lets far-away
landmarks through, which is exactly why being lost makes wrong matches likely, and why the twin labs
are dangerous.

## 5. The particle filter: the Seeker's belief (`Estimation/ParticleFilter2D.cs`)

The Seeker represents its belief about your position as N = 600 weighted samples. Unlike a Gaussian,
a cloud can have several separate blobs.

- **Predict:** each particle takes a random step with σ = v·√Δt·0.8 (it doesn't know how you moved).
  Steps into walls are rejected, so the cloud flows along corridors.
- **Update:** multiply each weight by the likelihood of what the Seeker sensed, then normalise:
  - heard a sound at ẑ with σ: w ← w · ((1-ε) exp(-|p-ẑ|²/2σ²) + ε). The ε floor stops one bad
    fix from wiping out every hypothesis.
  - saw you: same, with a small σ.
  - **looked and saw nothing:** w ← w · 0.08 for particles the camera covered. This is negative
    information, and it is what makes the Seeker search sensibly.
- **Resample** when the effective sample size 1/Σw² falls below N/2, using low-variance (systematic)
  resampling: one random offset, then evenly spaced picks through the cumulative weights.
- **Inject** 1% uniform particles each look, so a cloud that collapsed in the wrong place can recover
  (the fix for particle deprivation, and the reason the Seeker eventually stops staring at a decoy).

**Why decoys work:** a decoy burst is acoustically identical to a ping. Two equally loud sources give
a bimodal likelihood, the cloud splits, and the mean lands between the blobs, where nobody is
(`ParticleFilterTests.TwoIndistinguishableSoundsSplitTheCloud`). The Seeker steers by the most
probable cell (`Mode()`), not the mean, so it commits to one blob.

## 6. Consistency: judging a belief (`Estimation/Consistency.cs`)

A good belief is not just close to the truth; it's **honest** about how close. The normalised
estimation error squared,

```
NEES = eᵀ P⁻¹ e,   e = truth - estimate
```

follows chi-square with k degrees of freedom when the filter is consistent, so its average is about k
(3 for a pose, 2 for position). The reveal screen reports the fraction of time the truth was inside
the 95% position ellipse: about 95% for an honest filter, much less for an overconfident one.
`EkfTests.FilterIsConsistentOverMonteCarloRuns` checks that our EKF is consistent when its noise model
is right.

## 7. Mapping each mechanic to the concept

| Mechanic | Concept |
|---|---|
| Ellipse grows while driving | Prediction, process noise |
| Ping squeezes the ellipse | Measurement update, Kalman gain |
| Camera fixes are long and thin | Measurement covariance shapes the posterior |
| Sprinting and ice make it worse than it looks | Model mismatch, overconfidence |
| "Which landmark was that?" | Data association, gating, Mahalanobis distance |
| Twin labs | Perceptual aliasing |
| Red cloud spreads while you're quiet | Particle filter prediction |
| Red cloud collapses on a ping | Particle filter update |
| Seeker clears rooms it looked into | Negative information |
| Decoy splits the red cloud | Multimodal beliefs; why a single Gaussian would fail |
| Reveal screen score | Consistency, NEES |

## Further reading

- Thrun, Burgard, Fox, *Probabilistic Robotics*: chapters 2-4 (Bayes, Kalman, particle filters),
  5-7 (motion, sensors, localization).
- Bar-Shalom, Li, Kirubarajan, *Estimation with Applications to Tracking and Navigation*:
  consistency tests and gating.
- Cyrill Stachniss's SLAM course lectures (free online): good visual intuition for all of the above.
