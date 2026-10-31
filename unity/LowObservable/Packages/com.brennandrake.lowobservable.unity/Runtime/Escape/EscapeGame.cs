using System;
using System.Collections.Generic;
using System.IO;
using LowObservable.Core.Campaign;
using LowObservable.Core.Stages;
using LowObservable.Core.Tracking;
using LowObservable.Core.World;
using UnityEngine;

namespace LowObservable.Unity.Escape
{
    public enum EscapeScreen
    {
        /// <summary>Level select, exploit journal and WARDEN codex.</summary>
        Menu,
        /// <summary>The spec sheet for this level's tracker, shown before play.</summary>
        Briefing,
        Playing,
        Paused,
        /// <summary>Escaped: marks, time, and any new journal entries.</summary>
        Results,
    }

    /// <summary>
    /// Owns the campaign: which level is loaded, the save, and the screen state. Steps the core
    /// <see cref="Stage"/> at its fixed 50 Hz in FixedUpdate and forwards its events to the views. All game
    /// rules live in the core; this class only wires input in and state out.
    /// </summary>
    public sealed class EscapeGame : MonoBehaviour
    {
        public const string SaveKey = "lowobservable.escape.progress.v1";

        [Tooltip("Optional folder of .lvl files to load instead of the built-in levels (for editing levels without regenerating BuiltInStages.cs). Relative paths are resolved from the project folder.")]
        public string LevelsFolder = "";
        [Tooltip("Base seed. Each attempt gets its own derived seed so retries are not identical.")]
        public ulong Seed = 1;
        [Tooltip("Unlock every level (for testing).")]
        public bool UnlockAll;
        [Tooltip("Skip the menu and start this level id on Play (for testing a level).")]
        public string StartLevel = "";
        public EscapeInput Input;

        public CampaignDefinition Campaign { get; private set; }
        public CampaignProgress Progress { get; private set; }
        public Stage Stage { get; private set; }
        public StageDefinition Definition { get; private set; }
        public EscapeScreen Screen { get; private set; } = EscapeScreen.Menu;
        /// <summary>The tracker the current attempt runs against (differs from the level's in sandbox replays).</summary>
        public TrackerKind ActiveTracker { get; private set; }
        public bool IsSandboxRun { get; private set; }
        public StageResult LastResult { get; private set; }
        public List<ExploitId> LastNewExploits { get; private set; } = new List<ExploitId>();
        /// <summary>The note being read, or null. Reading does not pause WARDEN.</summary>
        public Note OpenNote { get; private set; }

        /// <summary>Raised for every core event, after the step that produced it.</summary>
        public event Action<StageEvent> EventRaised;
        /// <summary>Raised whenever a new <see cref="Stage"/> is built (level start or restart).</summary>
        public event Action<Stage> StageStarted;

        private readonly Dictionary<string, StageDefinition> _definitions = new Dictionary<string, StageDefinition>();
        private int _attempt;

        private void Awake()
        {
            LoadDefinitions();
            Progress = LoadProgress();
            if (Input == null) Input = GetComponent<EscapeInput>();
            if (Input == null) Input = gameObject.AddComponent<EscapeInput>();
            Input.Game = this;
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(StartLevel) && _definitions.ContainsKey(StartLevel)) Begin(StartLevel);
            else
            {
                // Something to draw behind the menu: the next level, idle.
                var next = Progress.NextLevel(Campaign);
                if (next != null) Load(next.Id, null);
                Screen = EscapeScreen.Menu;
            }
        }

        // ---------------------------------------------------------------- levels and saves

        private void LoadDefinitions()
        {
            var list = new List<StageDefinition>();
            string folder = ResolveFolder(LevelsFolder);
            if (folder != null)
            {
                var files = Directory.GetFiles(folder, "*.lvl");
                Array.Sort(files, StringComparer.Ordinal);
                foreach (var f in files)
                {
                    try { list.Add(StageParser.Parse(Path.GetFileNameWithoutExtension(f), File.ReadAllText(f))); }
                    catch (Exception e) { Debug.LogError($"[LowObservable] {f}: {e.Message}"); }
                }
            }
            if (list.Count == 0) list = BuiltInStages.LoadAll();
            _definitions.Clear();
            foreach (var d in list) _definitions[d.Id] = d;
            Campaign = CampaignDefinition.FromStages(list);
        }

        private static string ResolveFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return null;
            string path = Path.IsPathRooted(folder) ? folder : Path.GetFullPath(Path.Combine(Application.dataPath, "..", folder));
            return Directory.Exists(path) ? path : null;
        }

        private static CampaignProgress LoadProgress()
        {
            string text = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrEmpty(text)) return new CampaignProgress();
            try { return CampaignProgress.Deserialize(text); }
            catch (Exception e)
            {
                Debug.LogWarning($"[LowObservable] Save unreadable, starting fresh: {e.Message}");
                return new CampaignProgress();
            }
        }

        public void SaveProgress()
        {
            PlayerPrefs.SetString(SaveKey, Progress.Serialize());
            PlayerPrefs.Save();
        }

        public void ResetProgress()
        {
            Progress = new CampaignProgress();
            SaveProgress();
        }

        public bool IsUnlocked(string id) => UnlockAll || Progress.IsUnlocked(Campaign, id);

        public StageDefinition DefinitionFor(string id) => _definitions.TryGetValue(id, out var d) ? d : null;

        // ---------------------------------------------------------------- screen flow

        /// <summary>Loads a level and shows its briefing. <paramref name="trackerOverride"/> replays it against another tracker.</summary>
        public void Begin(string id, TrackerKind? trackerOverride = null)
        {
            Load(id, trackerOverride);
            Screen = EscapeScreen.Briefing;
        }

        public void Play()
        {
            if (Stage == null) return;
            Screen = EscapeScreen.Playing;
        }

        public void Restart()
        {
            if (Definition == null) return;
            Load(Definition.Id, IsSandboxRun ? ActiveTracker : (TrackerKind?)null);
            Screen = EscapeScreen.Playing;
        }

        public void TogglePause()
        {
            if (Screen == EscapeScreen.Playing) Screen = EscapeScreen.Paused;
            else if (Screen == EscapeScreen.Paused) Screen = EscapeScreen.Playing;
        }

        /// <summary>Leaves the level. Whatever was found on the way (notes, exploits, tools) is kept.</summary>
        public void QuitToMenu()
        {
            // Only an attempt that was actually played counts (backing out of a briefing must not mark the tracker as met).
            if (Stage != null && (Screen == EscapeScreen.Playing || Screen == EscapeScreen.Paused) && !IsSandboxRun)
            {
                Progress.Apply(StageResult.From(Stage), ActiveTracker);
                SaveProgress();
            }
            OpenNote = null;
            Screen = EscapeScreen.Menu;
        }

        public void NextLevel()
        {
            int i = Definition == null ? -1 : Campaign.IndexOf(Definition.Id);
            if (i >= 0 && i + 1 < Campaign.Levels.Count) Begin(Campaign.Levels[i + 1].Id);
            else Screen = EscapeScreen.Menu;
        }

        public void CloseNote() => OpenNote = null;

        private void Load(string id, TrackerKind? trackerOverride)
        {
            Definition = _definitions[id];
            _attempt++;
            var config = new StageConfig
            {
                Seed = Seed * 7919UL + (ulong)_attempt,
                UnlockedTools = Progress.Tools,
                TrackerOverride = trackerOverride,
            };
            Stage = new Stage(Definition, config);
            ActiveTracker = Stage.Tracker.Kind;
            IsSandboxRun = trackerOverride.HasValue && trackerOverride.Value != Definition.Tracker;
            OpenNote = null;
            LastResult = null;
            LastNewExploits = new List<ExploitId>();
            StageStarted?.Invoke(Stage);
        }

        // ---------------------------------------------------------------- simulation

        private void FixedUpdate()
        {
            if (Stage == null || Screen != EscapeScreen.Playing) return;
            // Unity's fixed step defaults to 0.02 s, matching Stage.Dt. If a project changes it, step the core
            // as many times as fit so game speed stays right.
            int steps = Mathf.Clamp(Mathf.RoundToInt(Time.fixedDeltaTime / (float)Stage.Dt), 1, 5);
            for (int k = 0; k < steps && Screen == EscapeScreen.Playing; k++)
            {
                Stage.Step(Input.Read(Stage));
                foreach (var e in Stage.Events)
                {
                    if (e.Type == StageEventType.NoteRead && e.Unit >= 0 && e.Unit < Stage.Notes.Count) OpenNote = Stage.Notes[e.Unit];
                    EventRaised?.Invoke(e);
                }
                if (Stage.Outcome == StageOutcome.Escaped) Finish();
            }
        }

        private void Update()
        {
            if (Input == null) return;
            if (Input.ConsumeBack())
            {
                if (OpenNote != null) CloseNote();
                else if (Screen == EscapeScreen.Playing || Screen == EscapeScreen.Paused) TogglePause();
                else if (Screen == EscapeScreen.Briefing || Screen == EscapeScreen.Results) Screen = EscapeScreen.Menu;
            }
            if (Input.ConsumeRestart() && (Screen == EscapeScreen.Playing || Screen == EscapeScreen.Paused || Screen == EscapeScreen.Results)) Restart();
            if (Input.ConsumeConfirm())
            {
                if (Screen == EscapeScreen.Briefing) Play();
                else if (Screen == EscapeScreen.Results) NextLevel();
                else if (OpenNote != null) CloseNote();
            }
            // Walking away from a note closes it.
            if (OpenNote != null && Stage != null && Core.Maths.Vec2.Distance(OpenNote.Position, Stage.Player) > Stage.Tuning.InteractRange * 2.5)
                CloseNote();
        }

        private void Finish()
        {
            LastResult = StageResult.From(Stage);
            if (IsSandboxRun)
            {
                // Sandbox replays teach, they don't grade: exploits still go in the journal.
                LastNewExploits = new List<ExploitId>();
                foreach (var e in LastResult.Exploits) if (Progress.Exploits.Add(e)) LastNewExploits.Add(e);
            }
            else
            {
                LastNewExploits = Progress.Apply(LastResult, ActiveTracker);
            }
            SaveProgress();
            OpenNote = null;
            Screen = EscapeScreen.Results;
        }

        /// <summary>The floor plan the views should draw (doors and lights change it during play).</summary>
        public FloorPlan Plan => Stage?.Plan;
    }
}
