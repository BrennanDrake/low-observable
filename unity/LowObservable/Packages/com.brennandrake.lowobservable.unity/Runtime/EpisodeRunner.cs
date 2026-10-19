using System;
using LowObservable.Core.Levels;
using LowObservable.Core.Simulation;
using LowObservable.Core.World;
using LowObservable.Unity.Input;
using UnityEngine;

namespace LowObservable.Unity
{
    /// <summary>
    /// Owns the core <see cref="Episode"/> and advances it on FixedUpdate. This is the single seam
    /// between Unity and the simulation: views read from <see cref="Episode"/> and subscribe to the
    /// events below; nothing else calls <c>Episode.Step</c>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class EpisodeRunner : MonoBehaviour
    {
        [Header("Content")]
        [Tooltip("Floor plan. If empty, the built-in level named below is used.")]
        public LevelAsset Level;
        public string BuiltInLevel = "annex-b";
        [Tooltip("Tuning values. If empty, core defaults are used.")]
        public TuningAsset Tuning;

        [Header("Run")]
        public ulong Seed = 1;
        [Tooltip("Advance the seed on every restart so each attempt plays differently.")]
        public bool NewSeedOnRestart = true;
        public AssociationPolicy Association = AssociationPolicy.PauseForPlayer;
        public bool SeekerStartsAlerted;

        [Header("Input")]
        [Tooltip("What drives the drone. If empty, a KeyboardMouseInput is added.")]
        public PlayerInputSource InputSource;

        /// <summary>The running simulation. Replaced on restart.</summary>
        public Episode Episode { get; private set; }
        public FloorPlan Plan { get; private set; }

        /// <summary>When true, views may draw ground truth (debug only). Toggled with F1 by default.</summary>
        public bool ShowTruth { get; set; }

        public event Action<Episode> EpisodeStarted;
        public event Action<EpisodeEvent> EventRaised;
        public event Action<Episode> EpisodeEnded;

        private bool _endedRaised;

        private void Awake()
        {
            if (InputSource == null) InputSource = GetComponent<PlayerInputSource>();
            if (InputSource == null) InputSource = gameObject.AddComponent<KeyboardMouseInput>();
            Plan = Level != null ? Level.Load() : BuiltInLevels.Load(BuiltInLevel);
            StartEpisode();
        }

        /// <summary>Starts a fresh attempt on the same floor.</summary>
        public void Restart()
        {
            if (NewSeedOnRestart) Seed++;
            StartEpisode();
        }

        private void StartEpisode()
        {
            var config = new EpisodeConfig
            {
                Seed = Seed,
                Tuning = Tuning != null ? Tuning.Snapshot() : TuningParameters.Default(),
                Association = Association,
                SeekerStartsAlerted = SeekerStartsAlerted,
                Record = true,
            };
            Episode = new Episode(Plan, config);
            Time.fixedDeltaTime = (float)Episode.FixedDt;
            _endedRaised = false;
            EpisodeStarted?.Invoke(Episode);
        }

        private void Update()
        {
            if (InputSource.RestartRequested()) Restart();
            if (InputSource.TruthToggleRequested()) ShowTruth = !ShowTruth;

            if (Episode.AwaitingAssociation)
            {
                int? choice = InputSource.ReadAssociationChoice();
                if (choice.HasValue)
                {
                    var pending = Episode.CurrentPending;
                    int idx = choice.Value;
                    int landmarkId = idx >= 0 && idx < pending.Candidates.Count ? pending.Candidates[idx].Landmark.Id : -1;
                    Episode.ResolvePending(landmarkId);
                    RaiseTickEvents();
                }
            }
        }

        private void FixedUpdate()
        {
            if (Episode.Outcome != EpisodeOutcome.Running || Episode.AwaitingAssociation) return;

            Episode.Step(InputSource.ReadCommand(Episode));
            RaiseTickEvents();

            if (Episode.Outcome != EpisodeOutcome.Running && !_endedRaised)
            {
                _endedRaised = true;
                EpisodeEnded?.Invoke(Episode);
            }
        }

        private void RaiseTickEvents()
        {
            if (EventRaised == null) return;
            foreach (var e in Episode.TickEvents) EventRaised(e);
        }
    }
}
