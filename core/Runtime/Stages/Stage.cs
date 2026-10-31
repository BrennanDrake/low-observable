using System;
using System.Collections.Generic;
using LowObservable.Core.Maths;
using LowObservable.Core.Tracking;
using LowObservable.Core.World;

namespace LowObservable.Core.Stages
{
    public enum StageOutcome
    {
        Running,
        Escaped,
    }

    public sealed class StageConfig
    {
        public ulong Seed = 1;
        public StageTuning Tuning;
        /// <summary>Tools the player already owns from earlier levels.</summary>
        public IEnumerable<ToolKind> UnlockedTools;
        /// <summary>Play the level against a different tracker than its file says (tracker swap mode, tests).</summary>
        public TrackerKind? TrackerOverride;
    }

    /// <summary>
    /// One play-through of one level of the escape game. Engine-free and deterministic for a given seed and
    /// input sequence. Unity calls <see cref="Step"/> once per physics tick (50 Hz) and draws the public state.
    ///
    /// Truth vs belief: the player's position is truth and always shown. WARDEN's units act only on
    /// <see cref="Tracker"/>, which is fed only by sensor reports. The exploit detector and the replay may read
    /// truth; WARDEN never does.
    /// </summary>
    public sealed class Stage
    {
        public const double Dt = 0.02;

        private struct Noise
        {
            public Vec2 Position;
            public double Loudness;
            public ReportOrigin Origin;
        }

        private readonly Rng _rng;
        private readonly Rng _sensorRng;
        private readonly GridPathfinder _pathfinder;
        private readonly List<Noise> _noises = new List<Noise>();
        private readonly List<StageEvent> _events = new List<StageEvent>();
        private readonly double[] _clearedTime;
        private readonly bool[] _circuitToggled = new bool[10];
        private double _strideAccum;
        private double _dispatchTimer;
        private double _sampleTimer;
        private int _lastPlayerCell = -1;
        private Vec2 _checkpoint;

        public StageDefinition Definition { get; }
        public StageTuning Tuning { get; }
        public FloorPlan Plan { get; }
        public IThreatTracker Tracker { get; }
        public ExploitDetector Exploits { get; }
        public StageRecorder Recorder { get; }

        public double Time { get; private set; }
        public StageOutcome Outcome { get; private set; } = StageOutcome.Running;

        // ---------------------------------------------------------------- Player (truth)
        public Vec2 Player { get; private set; }
        public double PlayerHeading { get; private set; }
        public MoveMode Mode { get; private set; } = MoveMode.Walk;
        public bool Moving { get; private set; }
        /// <summary>0..1. Running drains it; when it's empty, Run behaves like Walk.</summary>
        public double Stamina { get; private set; } = 1.0;
        public bool Hidden { get; private set; }
        public int HideSpotIndex { get; private set; } = -1;
        public int Carried { get; private set; }
        public readonly HashSet<string> Keys = new HashSet<string>();
        public readonly HashSet<ToolKind> Tools = new HashSet<ToolKind>();
        public readonly List<ToolKind> ToolsFoundHere = new List<ToolKind>();
        private readonly Dictionary<ToolKind, int> _charges = new Dictionary<ToolKind, int>();

        public int Captures { get; private set; }
        public int Detections { get; private set; }
        public double LastDetectedTime { get; private set; } = -999;
        public double PlayerCellEnteredTime { get; private set; }
        public ReportOrigin LastFixOrigin { get; private set; } = ReportOrigin.Player;

        // ---------------------------------------------------------------- World
        public readonly List<SecurityCamera> Cameras = new List<SecurityCamera>();
        public readonly List<Microphone> Microphones = new List<Microphone>();
        public readonly List<Laser> Lasers = new List<Laser>();
        public readonly List<PatrolUnit> Units = new List<PatrolUnit>();
        public readonly List<Roomba> Roombas = new List<Roomba>();
        public readonly List<Door> Doors = new List<Door>();
        public readonly List<Terminal> Terminals = new List<Terminal>();
        public readonly List<Keycard> Keycards = new List<Keycard>();
        public readonly List<LightSwitch> Switches = new List<LightSwitch>();
        public readonly List<ToolPickup> Pickups = new List<ToolPickup>();
        public readonly List<Note> Notes = new List<Note>();
        public readonly List<Throwable> Throwables = new List<Throwable>();
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        public readonly List<Noisemaker> Noisemakers = new List<Noisemaker>();
        public readonly List<Jammer> Jammers = new List<Jammer>();
        public readonly List<Mirror> Mirrors = new List<Mirror>();
        public readonly List<Vec2> HideSpots;
        public readonly List<Vec2> Checkpoints;
        public readonly List<Vec2> Exits;

        /// <summary>What happened during the last <see cref="Step"/>.</summary>
        public IReadOnlyList<StageEvent> Events => _events;

        public Stage(StageDefinition definition, StageConfig config = null)
        {
            config = config ?? new StageConfig();
            Definition = definition;
            Tuning = config.Tuning ?? StageTuning.Default();
            Plan = definition.CreatePlan();
            _rng = Rng.Derive(config.Seed, "stage");
            _sensorRng = Rng.Derive(config.Seed, "sensors");
            _pathfinder = new GridPathfinder(Plan);
            _clearedTime = new double[Plan.Width * Plan.Height];
            for (int k = 0; k < _clearedTime.Length; k++) _clearedTime[k] = -1;

            Player = definition.PlayerStart;
            _checkpoint = definition.PlayerStart;
            HideSpots = new List<Vec2>(definition.HideSpots);
            Checkpoints = new List<Vec2>(definition.Checkpoints);
            Exits = new List<Vec2>(definition.Exits);
            foreach (var p in definition.Throwables) Throwables.Add(new Throwable { Position = p });

            if (config.UnlockedTools != null) foreach (var t in config.UnlockedTools) Tools.Add(t);
            Tools.Add(ToolKind.Throw);
            _charges[ToolKind.Noisemaker] = Tuning.NoisemakerCharges;
            _charges[ToolKind.Jammer] = Tuning.JammerCharges;
            _charges[ToolKind.Mirror] = Tuning.MirrorCharges;

            BuildEntities(definition);
            Tracker = CreateTracker(config.TrackerOverride ?? definition.Tracker, config.Seed);
            Exploits = new ExploitDetector(this);
            Recorder = new StageRecorder();
        }

        private IThreatTracker CreateTracker(TrackerKind kind, ulong seed)
        {
            var t = Tuning;
            switch (kind)
            {
                case TrackerKind.LastKnown:
                    return new LastKnownTracker();
                case TrackerKind.OccupancyGrid:
                    return new OccupancyGridTracker(Plan) { AssumedSpeed = t.TrackerAssumedSpeed };
                case TrackerKind.Kalman:
                    var size = Plan.WorldSize;
                    return new KalmanTracker(size * 0.5, 0.5 * System.Math.Max(size.X, size.Y))
                    {
                        AccelerationNoise = t.KalmanAccelerationNoise,
                        Gate = t.KalmanGate,
                    };
                default:
                    return new ParticleTracker(Plan, t.ParticleCount, Rng.Derive(seed, "tracker"))
                    {
                        AssumedSpeed = t.TrackerAssumedSpeed,
                        InjectionPerSecond = t.ParticleInjectionPerSecond,
                    };
            }
        }

        private void BuildEntities(StageDefinition def)
        {
            var t = Tuning;
            var doorIndex = new Dictionary<char, int>();
            foreach (var e in def.Entities)
            {
                switch (e.Type)
                {
                    case EntityType.Camera:
                    case EntityType.Swivel:
                        bool swivel = e.Type == EntityType.Swivel;
                        double from = (swivel ? e.GetDouble("from", 0) : e.GetDouble("dir", 0)) * Angle.Deg2Rad;
                        double to = (swivel ? e.GetDouble("to", 0) : e.GetDouble("dir", 0)) * Angle.Deg2Rad;
                        var cam = new SecurityCamera
                        {
                            Index = Cameras.Count,
                            Marker = e.Marker,
                            Position = e.Position,
                            Swivel = swivel,
                            From = from,
                            To = to,
                            Period = e.GetDouble("period", t.SwivelPeriod),
                            Phase = e.GetDouble("phase", 0),
                            FieldOfView = e.GetDouble("fov", t.CameraFieldOfView * Angle.Rad2Deg) * Angle.Deg2Rad,
                            Range = e.GetDouble("range", t.CameraRange),
                        };
                        cam.Update(0);
                        Cameras.Add(cam);
                        break;
                    case EntityType.Mic:
                        Microphones.Add(new Microphone { Index = Microphones.Count, Marker = e.Marker, Position = e.Position, Sensitivity = e.GetDouble("sensitivity", t.MicRadius) });
                        break;
                    case EntityType.Laser:
                        Lasers.Add(new Laser { Index = Lasers.Count, Marker = e.Marker, A = e.Position, B = def.Markers[e.Get("to")[0]] });
                        break;
                    case EntityType.Guard:
                        var unit = new PatrolUnit
                        {
                            Index = Units.Count,
                            Marker = e.Marker,
                            Pose = new Pose2(e.Position, 0),
                            SpeedScale = e.GetDouble("speed", 1.0),
                        };
                        foreach (char m in e.Get("route")) unit.Route.Add(def.Markers[m]);
                        unit.Goal = unit.Route[0];
                        Units.Add(unit);
                        break;
                    case EntityType.Roomba:
                        Roombas.Add(new Roomba
                        {
                            Index = Roombas.Count,
                            Marker = e.Marker,
                            Position = e.Position,
                            Heading = e.GetDouble("dir", _rng.Range(0, 360)) * Angle.Deg2Rad,
                            Hat = e.Get("hat", ""),
                        });
                        break;
                    case EntityType.Door:
                        doorIndex[e.Marker] = Doors.Count;
                        Doors.Add(new Door { Index = Doors.Count, Marker = e.Marker, I = e.CellI, J = e.CellJ, Position = e.Position, Key = e.Get("key") });
                        break;
                    case EntityType.Keycard:
                        Keycards.Add(new Keycard { Marker = e.Marker, Position = e.Position, Name = e.Get("name") });
                        break;
                    case EntityType.Switch:
                        Switches.Add(new LightSwitch { Marker = e.Marker, Position = e.Position, Circuit = int.Parse(e.Get("circuit")) });
                        break;
                    case EntityType.Pickup:
                        ToolInfo.TryParse(e.Get("tool"), out var tool);
                        Pickups.Add(new ToolPickup { Marker = e.Marker, Position = e.Position, Tool = tool, Taken = false });
                        break;
                    case EntityType.Note:
                        Enum.TryParse(e.Get("kind", "Sticky"), true, out NoteKind kind);
                        Notes.Add(new Note { Index = Notes.Count, Marker = e.Marker, Position = e.Position, Text = e.Get("text"), Kind = kind });
                        break;
                }
            }
            foreach (var e in def.Entities)
            {
                if (e.Type != EntityType.Terminal) continue;
                var term = new Terminal { Index = Terminals.Count, Marker = e.Marker, Position = e.Position, Forge = e.Get("kind", "door") == "forge" };
                foreach (char m in e.Get("opens", ""))
                {
                    if (!doorIndex.TryGetValue(m, out int di)) throw new FormatException($"{def.Id}: terminal '{e.Marker}' opens '{m}', which is not a door.");
                    term.Opens.Add(di);
                }
                Terminals.Add(term);
            }
        }

        // ================================================================ Queries for views and the bot

        public int Charges(ToolKind tool)
        {
            if (tool == ToolKind.Throw) return Carried;
            if (tool == ToolKind.Forge)
            {
                int n = 0;
                foreach (var term in Terminals) if (term.Forge && !term.ForgeUsed) n++;
                return n;
            }
            return _charges.TryGetValue(tool, out int c) ? c : 0;
        }

        public bool HasTool(ToolKind tool) => Tools.Contains(tool);

        /// <summary>What the player could see from where they stand (for drawing units the player is aware of).</summary>
        public bool PlayerCanSee(Vec2 p)
        {
            double d = Vec2.Distance(Player, p);
            if (d > 16) return false;
            if (Plan.IsDarkAt(p) && d > 3.0) return false;
            return Plan.LineOfSight(Player, p);
        }

        /// <summary>
        /// Would a point be inside a camera's view, or the view of a unit the player can currently see? Uses only
        /// what the player can see on screen (cones), so the bot may use it.
        /// </summary>
        public bool IsWatched(Vec2 p)
        {
            var t = Tuning;
            foreach (var cam in Cameras)
                if (!cam.Jammed && NaiveCoverage(cam.Position, cam.Heading, cam.FieldOfView, cam.Range)(p)) return true;
            foreach (var u in Units)
                if (PlayerCanSee(u.Pose.Position) && NaiveCoverage(u.Pose.Position, u.LookHeading, t.GuardFieldOfView, t.GuardVisionRange)(p)) return true;
            return false;
        }

        /// <summary>Exposure: how much of WARDEN's belief sits on the player right now.</summary>
        public double Exposure => Tracker.MassWithin(Player, Tuning.ExposureRadius);

        public double ClearedTimeAt(Vec2 p)
        {
            Plan.WorldToCell(p, out int i, out int j);
            return Plan.InBounds(i, j) ? _clearedTime[i * Plan.Height + j] : -1;
        }

        public bool AnyUnitHunting()
        {
            foreach (var u in Units) if (u.Mode != GuardMode.Patrol) return true;
            return false;
        }

        public double NearestHuntingUnitDistance(Vec2 p)
        {
            double best = double.PositiveInfinity;
            foreach (var u in Units)
                if (u.Mode == GuardMode.Investigate || u.Mode == GuardMode.Search || u.Mode == GuardMode.CheckLocker)
                    best = System.Math.Min(best, Vec2.Distance(u.Pose.Position, p));
            return best;
        }

        /// <summary>Short prompt for the nearest thing the interact button would act on, or null.</summary>
        public string InteractPrompt()
        {
            if (Hidden) return "Leave hiding spot";
            var target = FindInteractable();
            if (target.kind == InteractKind.None)
            {
                foreach (var term in Terminals)
                    if (!term.Forge && !term.Done && Vec2.Distance(term.Position, Player) <= Tuning.InteractRange)
                        return $"Hold to use terminal ({(int)(100 * term.Progress / Tuning.TerminalUseSeconds)}%)";
                foreach (var term in Terminals)
                    if (term.Forge && !term.ForgeUsed && Vec2.Distance(term.Position, Player) <= Tuning.InteractRange)
                        return HasTool(ToolKind.Forge) ? "Maintenance terminal: aim and use Forged fix" : "Maintenance terminal (locked)";
                return null;
            }
            switch (target.kind)
            {
                case InteractKind.Note: return "Read";
                case InteractKind.Pickup: return "Take " + ToolInfo.Name(Pickups[target.index].Tool);
                case InteractKind.Keycard: return "Take keycard";
                case InteractKind.Hide: return "Hide";
                case InteractKind.Throwable: return Carried >= Tuning.CarryLimit ? "Hands full" : "Pick up";
                case InteractKind.Switch: return "Flip light switch";
                case InteractKind.Mirror: return "Take mirror back";
                default: return null;
            }
        }

        // ================================================================ Simulation

        public void Step(StageInput input)
        {
            _events.Clear();
            _noises.Clear();
            if (Outcome != StageOutcome.Running) return;
            Time += Dt;

            var previous = Player;
            UpdatePlayer(input);
            UpdateProjectiles();
            UpdateNoisemakers();
            UpdateJammers();
            UpdateJamming();
            UpdateRoombas();
            UpdateCameras();
            UpdateLasers(previous);
            ProcessNoises();
            Tracker.Predict(Dt);

            _dispatchTimer -= Dt;
            if (_dispatchTimer <= 0)
            {
                _dispatchTimer = Tuning.DispatchPeriod;
                Dispatch();
            }
            foreach (var u in Units) UpdateUnit(u);
            if (Outcome != StageOutcome.Running) return;

            foreach (var exit in Exits)
            {
                if (!Hidden && Vec2.Distance(exit, Player) <= 0.7)
                {
                    Outcome = StageOutcome.Escaped;
                    Emit(StageEventType.Escaped, Player, Definition.Title);
                    break;
                }
            }

            Exploits.Tick(Dt);
            _sampleTimer -= Dt;
            if (_sampleTimer <= 0)
            {
                _sampleTimer = Tuning.SampleInterval;
                Recorder.Sample(this);
            }
        }

        // ---------------------------------------------------------------- Player

        private enum InteractKind { None, Note, Pickup, Keycard, Hide, Throwable, Switch, Mirror }

        private (InteractKind kind, int index) FindInteractable()
        {
            double best = Tuning.InteractRange;
            var result = (InteractKind.None, -1);
            void Consider(InteractKind kind, int index, Vec2 pos)
            {
                double d = Vec2.Distance(pos, Player);
                if (d <= best) { best = d; result = (kind, index); }
            }
            for (int k = 0; k < Pickups.Count; k++) if (!Pickups[k].Taken) Consider(InteractKind.Pickup, k, Pickups[k].Position);
            for (int k = 0; k < Keycards.Count; k++) if (!Keycards[k].Taken) Consider(InteractKind.Keycard, k, Keycards[k].Position);
            for (int k = 0; k < Notes.Count; k++) Consider(InteractKind.Note, k, Notes[k].Position);
            for (int k = 0; k < HideSpots.Count; k++) Consider(InteractKind.Hide, k, HideSpots[k]);
            for (int k = 0; k < Throwables.Count; k++) if (!Throwables[k].Taken) Consider(InteractKind.Throwable, k, Throwables[k].Position);
            for (int k = 0; k < Switches.Count; k++) Consider(InteractKind.Switch, k, Switches[k].Position);
            for (int k = 0; k < Mirrors.Count; k++) Consider(InteractKind.Mirror, k, Mirrors[k].Center);
            return result;
        }

        private void UpdatePlayer(StageInput input)
        {
            var t = Tuning;
            Mode = input.Mode;
            bool wantsRun = Mode == MoveMode.Run && input.Move.LengthSquared > 1e-6 && !Hidden;
            if (wantsRun && Stamina > 0) Stamina = System.Math.Max(0, Stamina - Dt / t.StaminaSeconds);
            else if (!wantsRun) Stamina = System.Math.Min(1, Stamina + Dt * t.StaminaRecovery / t.StaminaSeconds);
            if (Mode == MoveMode.Run && Stamina <= 0) Mode = MoveMode.Walk;
            Moving = false;

            if (Hidden)
            {
                if (input.InteractPressed)
                {
                    Hidden = false;
                    HideSpotIndex = -1;
                    MakeNoise(Player, t.HideLoudness, ReportOrigin.Player);
                    Emit(StageEventType.Unhid, Player);
                }
                return;
            }

            // Movement with wall sliding.
            var move = input.Move;
            double len = move.Length;
            if (len > 1e-6)
            {
                if (len > 1) move = move / len;
                double speed = Mode == MoveMode.Sneak ? t.SneakSpeed : Mode == MoveMode.Run ? t.RunSpeed : t.WalkSpeed;
                var delta = move * speed * Dt;
                var next = Player + delta;
                if (!Plan.IsFree(next, t.PlayerRadius))
                {
                    var nx = new Vec2(Player.X + delta.X, Player.Y);
                    var ny = new Vec2(Player.X, Player.Y + delta.Y);
                    next = Plan.IsFree(nx, t.PlayerRadius) ? nx : Plan.IsFree(ny, t.PlayerRadius) ? ny : Player;
                }
                double moved = Vec2.Distance(next, Player);
                if (moved > 1e-6)
                {
                    Moving = true;
                    PlayerHeading = (next - Player).Angle;
                    Player = next;
                    _strideAccum += moved;
                    if (_strideAccum >= t.StrideLength)
                    {
                        _strideAccum = 0;
                        double loud = Mode == MoveMode.Sneak ? t.SneakLoudness : Mode == MoveMode.Run ? t.RunLoudness : t.WalkLoudness;
                        MakeNoise(Player, loud, ReportOrigin.Player);
                        Emit(StageEventType.Footstep, Player, null, -1, loud);
                    }
                }
            }

            Plan.WorldToCell(Player, out int ci, out int cj);
            int cell = ci * Plan.Height + cj;
            if (cell != _lastPlayerCell)
            {
                _lastPlayerCell = cell;
                PlayerCellEnteredTime = Time;
            }

            foreach (var c in Checkpoints)
            {
                if (Vec2.Distance(c, Player) <= 1.0 && Vec2.Distance(c, _checkpoint) > 0.1)
                {
                    _checkpoint = c;
                    Emit(StageEventType.CheckpointReached, c);
                }
            }

            // Doors open on touch if you hold their keycard.
            foreach (var d in Doors)
            {
                if (d.Open || d.Key == null || !Keys.Contains(d.Key)) continue;
                if (Vec2.Distance(d.Position, Player) <= 1.3) OpenDoor(d);
            }

            if (input.InteractPressed) Interact();
            UpdateTerminals(input.InteractHeld || input.InteractPressed);
            if (input.UsePressed) UseTool(input);
        }

        private void Interact()
        {
            var t = Tuning;
            var (kind, index) = FindInteractable();
            switch (kind)
            {
                case InteractKind.Note:
                    var note = Notes[index];
                    note.Read = true;
                    Emit(StageEventType.NoteRead, note.Position, note.Text, note.Index, 0, note.Kind.ToString());
                    break;
                case InteractKind.Pickup:
                    var pickup = Pickups[index];
                    pickup.Taken = true;
                    Tools.Add(pickup.Tool);
                    ToolsFoundHere.Add(pickup.Tool);
                    Emit(StageEventType.ToolUnlocked, pickup.Position, ToolInfo.Name(pickup.Tool), -1, 0, ToolInfo.Id(pickup.Tool));
                    break;
                case InteractKind.Keycard:
                    var card = Keycards[index];
                    card.Taken = true;
                    Keys.Add(card.Name);
                    Emit(StageEventType.KeycardTaken, card.Position, card.Name);
                    break;
                case InteractKind.Hide:
                    Hidden = true;
                    HideSpotIndex = index;
                    Player = HideSpots[index];
                    MakeNoise(Player, t.HideLoudness, ReportOrigin.Player);
                    Emit(StageEventType.Hid, Player);
                    break;
                case InteractKind.Throwable:
                    if (Carried >= t.CarryLimit) { Emit(StageEventType.Denied, Player, "Hands full"); break; }
                    Throwables[index].Taken = true;
                    Carried++;
                    Emit(StageEventType.PickedUp, Throwables[index].Position);
                    break;
                case InteractKind.Switch:
                    ToggleCircuit(Switches[index]);
                    break;
                case InteractKind.Mirror:
                    var m = Mirrors[index];
                    Mirrors.RemoveAt(index);
                    _charges[ToolKind.Mirror]++;
                    Emit(StageEventType.MirrorTaken, m.Center);
                    break;
            }
        }

        private void UpdateTerminals(bool held)
        {
            var t = Tuning;
            foreach (var term in Terminals)
            {
                if (term.Forge || term.Done) continue;
                if (!held || Vec2.Distance(term.Position, Player) > t.InteractRange) continue;
                term.Progress += Dt;
                term.BeepTimer -= Dt;
                if (term.BeepTimer <= 0)
                {
                    term.BeepTimer = t.TerminalBeepPeriod;
                    MakeNoise(term.Position, t.TerminalBeepLoudness, ReportOrigin.Player);
                    Emit(StageEventType.TerminalProgress, term.Position, null, term.Index, t.TerminalBeepLoudness);
                }
                if (term.Progress >= t.TerminalUseSeconds)
                {
                    term.Done = true;
                    Emit(StageEventType.TerminalDone, term.Position, null, term.Index);
                    foreach (int di in term.Opens) OpenDoor(Doors[di]);
                }
            }
        }

        private void OpenDoor(Door d)
        {
            if (d.Open) return;
            d.Open = true;
            Plan.SetCell(d.I, d.J, Plan.GetCell(d.I, d.J) & ~CellFlags.Wall);
            if (Tracker is OccupancyGridTracker grid) grid.RefreshFreeCells();
            MakeNoise(d.Position, Tuning.DoorLoudness, ReportOrigin.Player);
            Emit(StageEventType.DoorOpened, d.Position, null, d.Index, Tuning.DoorLoudness);
        }

        private void ToggleCircuit(LightSwitch sw)
        {
            _circuitToggled[sw.Circuit] = !_circuitToggled[sw.Circuit];
            for (int i = 0; i < Plan.Width; i++)
                for (int j = 0; j < Plan.Height; j++)
                    if (Definition.Circuits[i, j] == sw.Circuit) Plan.SetCell(i, j, Plan.GetCell(i, j) ^ CellFlags.Dark);
            MakeNoise(sw.Position, Tuning.SwitchLoudness, ReportOrigin.Player);
            Emit(StageEventType.LightsToggled, sw.Position, null, sw.Circuit, Tuning.SwitchLoudness);
        }

        public bool CircuitToggled(int circuit) => circuit >= 0 && circuit < _circuitToggled.Length && _circuitToggled[circuit];

        /// <summary>Why a tool can't be used right now, or null if it can.</summary>
        public string ToolBlockedReason(ToolKind tool, Vec2 aim)
        {
            var t = Tuning;
            if (!HasTool(tool)) return "Not found yet";
            switch (tool)
            {
                case ToolKind.Throw: return Carried > 0 ? null : "Nothing to throw";
                case ToolKind.Noisemaker: return Charges(tool) > 0 ? null : "No noisemakers left";
                case ToolKind.Jammer: return Charges(tool) > 0 ? (PlaceablePoint(aim, out _) ? null : "Can't place there") : "No jammers left";
                case ToolKind.Mirror: return Charges(tool) > 0 ? (PlaceablePoint(aim, out _) ? null : "Can't place there") : "No mirrors left";
                default:
                    var term = NearbyForgeTerminal();
                    if (term == null) return "Stand at a maintenance terminal";
                    if (Vec2.Distance(aim, term.Position) > t.ForgeRange) return "Out of terminal range";
                    if (!Plan.IsFree(aim, 0.1)) return "Pick open floor";
                    return null;
            }
        }

        private Terminal NearbyForgeTerminal()
        {
            foreach (var term in Terminals)
                if (term.Forge && !term.ForgeUsed && Vec2.Distance(term.Position, Player) <= Tuning.InteractRange) return term;
            return null;
        }

        private bool PlaceablePoint(Vec2 aim, out Vec2 point)
        {
            var dir = aim - Player;
            double d = dir.Length;
            point = d > Tuning.MirrorPlaceRange ? Player + dir / d * Tuning.MirrorPlaceRange : aim;
            return Plan.IsFree(point, 0.2) && Plan.LineOfSight(Player, point);
        }

        private void UseTool(StageInput input)
        {
            var t = Tuning;
            string reason = ToolBlockedReason(input.Tool, input.Aim);
            if (reason != null)
            {
                Emit(StageEventType.Denied, Player, reason);
                return;
            }
            switch (input.Tool)
            {
                case ToolKind.Throw:
                    Carried--;
                    Launch(input.Aim, false);
                    break;
                case ToolKind.Noisemaker:
                    _charges[ToolKind.Noisemaker]--;
                    Launch(input.Aim, true);
                    break;
                case ToolKind.Jammer:
                    PlaceablePoint(input.Aim, out var jp);
                    _charges[ToolKind.Jammer]--;
                    Jammers.Add(new Jammer { Position = jp, Radius = t.JammerRadius, TimeLeft = t.JammerDuration, InterferenceTimer = t.JammerInterferencePeriod });
                    Emit(StageEventType.JammerPlaced, jp, null, -1, t.JammerRadius);
                    break;
                case ToolKind.Mirror:
                    PlaceablePoint(input.Aim, out var mp);
                    _charges[ToolKind.Mirror]--;
                    Mirrors.Add(new Mirror { Center = mp, Angle = input.MirrorAngle, Length = t.MirrorLength });
                    Emit(StageEventType.MirrorPlaced, mp);
                    break;
                case ToolKind.Forge:
                    var term = NearbyForgeTerminal();
                    term.ForgeUsed = true;
                    var result = Report(input.Aim, t.ForgeSigma, t.ForgeOutlierWeight, ReportOrigin.Forge);
                    Emit(StageEventType.Forged, input.Aim, result == FixResult.Accepted ? "Forged sighting accepted" : "Forgery rejected as an outlier");
                    break;
            }
        }

        /// <summary>
        /// Where a tool aimed at <paramref name="aim"/> would land or be placed: throws stop short of walls and
        /// at <see cref="StageTuning.ThrowRange"/>, placed tools at <see cref="StageTuning.MirrorPlaceRange"/>, and
        /// a forged fix goes exactly where it is aimed. For aim previews; using the tool applies the same rule.
        /// </summary>
        public Vec2 ToolTarget(ToolKind tool, Vec2 aim)
        {
            switch (tool)
            {
                case ToolKind.Throw:
                case ToolKind.Noisemaker:
                    return ThrowLanding(aim);
                case ToolKind.Jammer:
                case ToolKind.Mirror:
                    PlaceablePoint(aim, out var point);
                    return point;
                default:
                    return aim;
            }
        }

        private Vec2 ThrowLanding(Vec2 aim)
        {
            var dir = aim - Player;
            double d = dir.Length;
            if (d < 1e-6) { dir = Vec2.FromAngle(PlayerHeading); d = 0.5; }
            var u = dir / d;
            d = System.Math.Min(d, Tuning.ThrowRange);
            // Fly until just before the first wall.
            var landing = Player;
            for (double s = 0.1; s <= d + 1e-9; s += 0.1)
            {
                var p = Player + u * s;
                if (!Plan.IsFree(p, 0.15)) break;
                landing = p;
            }
            return landing;
        }

        private void Launch(Vec2 aim, bool noisemaker)
        {
            var t = Tuning;
            var landing = ThrowLanding(aim);
            double flight = System.Math.Max(0.1, Vec2.Distance(Player, landing) / t.ThrowSpeed);
            Projectiles.Add(new Projectile { From = Player, To = landing, Position = Player, Duration = flight, IsNoisemaker = noisemaker });
        }

        private void UpdateProjectiles()
        {
            for (int k = Projectiles.Count - 1; k >= 0; k--)
            {
                var p = Projectiles[k];
                p.Elapsed += Dt;
                double s = System.Math.Min(1.0, p.Elapsed / p.Duration);
                p.Position = Vec2.Lerp(p.From, p.To, s);
                if (s < 1.0) continue;
                Projectiles.RemoveAt(k);
                if (p.IsNoisemaker)
                {
                    var nm = new Noisemaker { Position = p.To, BurstsLeft = Tuning.NoisemakerBursts, Timer = 0 };
                    foreach (var r in Roombas)
                    {
                        if (Vec2.Distance(r.Position, p.To) <= Tuning.NoisemakerStickRadius)
                        {
                            nm.Roomba = r.Index;
                            Emit(StageEventType.NoisemakerAttached, r.Position, "Noisemaker stuck to a roomba", r.Index);
                            break;
                        }
                    }
                    Noisemakers.Add(nm);
                }
                else
                {
                    MakeNoise(p.To, Tuning.ThrowLoudness, ReportOrigin.Decoy);
                    Emit(StageEventType.ThrowLanded, p.To, null, -1, Tuning.ThrowLoudness);
                    Throwables.Add(new Throwable { Position = p.To });
                }
            }
        }

        private void UpdateNoisemakers()
        {
            for (int k = Noisemakers.Count - 1; k >= 0; k--)
            {
                var nm = Noisemakers[k];
                if (nm.Roomba >= 0) nm.Position = Roombas[nm.Roomba].Position;
                nm.Timer -= Dt;
                if (nm.Timer > 0) continue;
                nm.Timer = Tuning.NoisemakerBurstPeriod;
                nm.BurstsLeft--;
                MakeNoise(nm.Position, Tuning.NoisemakerLoudness, ReportOrigin.Decoy);
                Emit(StageEventType.Noise, nm.Position, "noisemaker", -1, Tuning.NoisemakerLoudness);
                if (!nm.Active) Noisemakers.RemoveAt(k);
            }
        }

        private void UpdateJammers()
        {
            var t = Tuning;
            for (int k = Jammers.Count - 1; k >= 0; k--)
            {
                var j = Jammers[k];
                j.TimeLeft -= Dt;
                if (!j.Active) { Jammers.RemoveAt(k); continue; }
                j.InterferenceTimer -= Dt;
                if (j.InterferenceTimer <= 0)
                {
                    j.InterferenceTimer = t.JammerInterferencePeriod;
                    double s = t.JammerInterferenceSigma;
                    var measured = new Vec2(j.Position.X + _sensorRng.NextGaussian() * s, j.Position.Y + _sensorRng.NextGaussian() * s);
                    Report(measured, s, t.SensorOutlierWeight, ReportOrigin.Interference);
                }
            }
        }

        private bool IsJammed(Vec2 sensor)
        {
            foreach (var j in Jammers) if (j.Active && Vec2.Distance(j.Position, sensor) <= j.Radius) return true;
            return false;
        }

        private void UpdateJamming()
        {
            foreach (var c in Cameras) c.Jammed = IsJammed(c.Position);
            foreach (var m in Microphones) m.Jammed = IsJammed(m.Position);
            foreach (var l in Lasers) l.Jammed = IsJammed(l.A);
            foreach (var u in Units) u.Jammed = IsJammed(u.Pose.Position);
            foreach (var r in Roombas) r.Jammed = IsJammed(r.Position);
        }

        // ---------------------------------------------------------------- Sensors

        private void MakeNoise(Vec2 position, double loudness, ReportOrigin origin)
        {
            _noises.Add(new Noise { Position = position, Loudness = loudness, Origin = origin });
        }

        private bool Hears(Vec2 listener, double sensitivity, Noise noise, out Vec2 measured, out double sigma)
        {
            var t = Tuning;
            double dist = Vec2.Distance(listener, noise.Position);
            bool los = Plan.LineOfSight(listener, noise.Position);
            double audible = noise.Loudness * sensitivity * (los ? 1.0 : t.WallMuffling);
            measured = noise.Position;
            sigma = t.HearingSigmaBase + t.HearingSigmaPerMeter * dist;
            if (dist > audible) return false;
            measured = new Vec2(noise.Position.X + _sensorRng.NextGaussian() * sigma, noise.Position.Y + _sensorRng.NextGaussian() * sigma);
            return true;
        }

        private void ProcessNoises()
        {
            var t = Tuning;
            foreach (var noise in _noises)
            {
                foreach (var mic in Microphones)
                {
                    mic.SinceHeard += 0;
                    if (mic.Jammed || !Hears(mic.Position, mic.Sensitivity, noise, out var z, out var s)) continue;
                    mic.SinceHeard = 0;
                    Report(z, s, t.SensorOutlierWeight, noise.Origin);
                    Emit(StageEventType.Heard, mic.Position, "mic", mic.Index, 0, noise.Origin.ToString());
                }
                foreach (var u in Units)
                {
                    if (u.Jammed || !Hears(u.Pose.Position, t.UnitHearing, noise, out var z, out var s)) continue;
                    Report(z, s, t.SensorOutlierWeight, noise.Origin);
                    Emit(StageEventType.Heard, u.Pose.Position, "unit", u.Index, 0, noise.Origin.ToString());
                }
            }
            foreach (var mic in Microphones) mic.SinceHeard += Dt;
        }

        /// <summary>Sends a position fix to WARDEN's tracker and lets the exploit journal see it.</summary>
        private FixResult Report(Vec2 measured, double sigma, double outlier, ReportOrigin origin)
        {
            var result = Tracker.ReportFix(measured, sigma, outlier);
            if (result == FixResult.Accepted) LastFixOrigin = origin;
            else Emit(StageEventType.ReportRejected, measured, "Report rejected as an outlier", -1, 0, origin.ToString());
            Recorder.CountReport(result);
            Exploits.OnReport(measured, origin, result);
            return result;
        }

        private void ReportMiss(Vec2 origin, double range, Func<Vec2, bool> covers)
        {
            bool tracking = Tracker.Kind == TrackerKind.LastKnown && Tracker.HasEstimate;
            var point = Tracker.BestGuess;
            var footprint = new SensorFootprint(origin, range, covers);
            Tracker.ReportMiss(footprint, Tuning.MissLikelihood);
            MarkCleared(footprint);
            if (tracking && !Tracker.HasEstimate)
            {
                Exploits.OnLastKnownLost(point);
                Emit(StageEventType.GuardBark, point, "TRAIL LOST.");
            }
        }

        private void MarkCleared(SensorFootprint fp)
        {
            Plan.WorldToCell(fp.Origin, out int ci, out int cj);
            int r = (int)System.Math.Ceiling(fp.Range) + 1;
            for (int i = System.Math.Max(0, ci - r); i <= System.Math.Min(Plan.Width - 1, ci + r); i++)
                for (int j = System.Math.Max(0, cj - r); j <= System.Math.Min(Plan.Height - 1, cj + r); j++)
                    if (!Plan.IsWall(i, j) && fp.Covers(Plan.CellCenter(i, j))) _clearedTime[i * Plan.Height + j] = Time;
        }

        private void Detected()
        {
            Detections++;
            LastDetectedTime = Time;
        }

        private bool InCone(Vec2 origin, double heading, double fov, double range, Vec2 p)
        {
            var rel = p - origin;
            double d = rel.Length;
            if (d > range) return false;
            if (d < 0.35) return true;
            return System.Math.Abs(Angle.Difference(rel.Angle, heading)) <= 0.5 * fov;
        }

        private bool LightOk(Vec2 target, double distance) => !Plan.IsDarkAt(target) || distance <= Tuning.DarkVisionRange;

        private bool BlockedByMirror(Vec2 a, Vec2 b, Mirror except = null)
        {
            foreach (var m in Mirrors)
            {
                if (m == except) continue;
                if (SegmentsIntersect(a, b, m.A, m.B, out _, out _)) return true;
            }
            return false;
        }

        /// <summary>
        /// What a vision sensor's own model says it can see: cone, range, walls, light. It ignores mirrors,
        /// which is exactly the flaw mirrors exploit. Used for negative information.
        /// </summary>
        private Func<Vec2, bool> NaiveCoverage(Vec2 origin, double heading, double fov, double range)
        {
            return p => InCone(origin, heading, fov, range, p) && LightOk(p, Vec2.Distance(origin, p)) && Plan.LineOfSight(origin, p);
        }

        /// <summary>
        /// Physical vision: can the sensor see the player, directly or through one mirror bounce? Reports the
        /// position the sensor believes (the reflection, for a mirror sighting).
        /// </summary>
        private bool Sees(Vec2 origin, double heading, double fov, double range, out Vec2 apparent, out bool viaMirror, out bool blockedByMirror)
        {
            apparent = Player;
            viaMirror = false;
            blockedByMirror = false;
            if (Hidden) return false;
            var target = Player;
            if (InCone(origin, heading, fov, range, target) && LightOk(target, Vec2.Distance(origin, target)) && Plan.LineOfSight(origin, target))
            {
                if (!BlockedByMirror(origin, target)) return true;
                blockedByMirror = true;
            }
            foreach (var m in Mirrors)
            {
                var virt = m.Reflect(target);
                double pathLength = Vec2.Distance(origin, virt);
                if (!InCone(origin, heading, fov, range, virt)) continue;
                if (!SegmentsIntersect(origin, virt, m.A, m.B, out var hit, out _)) continue;
                if (!Plan.LineOfSight(origin, hit) || !Plan.LineOfSight(hit, target)) continue;
                if (BlockedByMirror(origin, hit, m) || BlockedByMirror(hit, target, m)) continue;
                if (!LightOk(target, pathLength)) continue;
                apparent = virt;
                viaMirror = true;
                return true;
            }
            return false;
        }

        private static bool SegmentsIntersect(Vec2 p, Vec2 p2, Vec2 q, Vec2 q2, out Vec2 hit, out double tP)
        {
            hit = default;
            tP = 0;
            var r = p2 - p;
            var s = q2 - q;
            double denom = r.X * s.Y - r.Y * s.X;
            if (System.Math.Abs(denom) < 1e-12) return false;
            var qp = q - p;
            double t = (qp.X * s.Y - qp.Y * s.X) / denom;
            double u = (qp.X * r.Y - qp.Y * r.X) / denom;
            if (t <= 1e-6 || t >= 1 - 1e-6 || u < 0 || u > 1) return false;
            tP = t;
            hit = p + r * t;
            return true;
        }

        private void UpdateCameras()
        {
            var t = Tuning;
            foreach (var cam in Cameras)
            {
                cam.Update(Time);
                cam.SinceAlarm += Dt;
                cam.CheckTimer -= Dt;
                if (cam.CheckTimer > 0) continue;
                cam.CheckTimer = t.CameraPeriod;

                bool sees = Sees(cam.Position, cam.Heading, cam.FieldOfView, cam.Range, out var apparent, out bool viaMirror, out bool blocked);
                var naive = NaiveCoverage(cam.Position, cam.Heading, cam.FieldOfView, cam.Range);
                bool wouldSeeDirect = !Hidden && naive(Player) && !blocked;
                Exploits.OnCameraCheck(cam.Jammed, wouldSeeDirect, blocked, naive(Player));
                if (cam.Jammed) continue;
                if (sees)
                {
                    cam.SinceAlarm = 0;
                    Detected();
                    var z = Jitter(apparent, t.VisionSigma);
                    Report(z, t.VisionSigma * 1.5, t.SensorOutlierWeight * 0.1, viaMirror ? ReportOrigin.Mirror : ReportOrigin.Player);
                    Emit(StageEventType.Sighted, cam.Position, viaMirror ? "mirror" : "camera", cam.Index, 0, viaMirror ? "Mirror" : "Player");
                }
                else
                {
                    ReportMiss(cam.Position, cam.Range, naive);
                }
            }
        }

        private Vec2 Jitter(Vec2 p, double sigma) => new Vec2(p.X + _sensorRng.NextGaussian() * sigma, p.Y + _sensorRng.NextGaussian() * sigma);

        private void UpdateLasers(Vec2 previous)
        {
            foreach (var l in Lasers)
            {
                l.SinceTripped += Dt;
                if (Hidden || Vec2.Distance(previous, Player) < 1e-9) continue;
                if (!SegmentsIntersect(previous, Player + (Player - previous) * 0.01, l.A, l.B, out var hit, out _)) continue;
                l.SinceTripped = 0;
                Emit(StageEventType.LaserTripped, hit, null, l.Index);
                if (l.Jammed) continue;
                Detected();
                Report(Jitter(hit, Tuning.LaserSigma), Tuning.LaserSigma, Tuning.SensorOutlierWeight * 0.1, ReportOrigin.Player);
            }
        }

        private void UpdateRoombas()
        {
            var t = Tuning;
            foreach (var r in Roombas)
            {
                r.SinceBump += Dt;
                r.Cooldown -= Dt;
                r.TurnTimer -= Dt;
                if (r.TurnTimer <= 0)
                {
                    r.TurnTimer = _rng.Range(2.5, 6.0);
                    r.Heading += _rng.Range(-0.8, 0.8);
                }
                var next = r.Position + Vec2.FromAngle(r.Heading, t.RoombaSpeed * Dt);
                if (Plan.IsFree(next, 0.3)) r.Position = next;
                else r.Heading += _rng.Range(1.8, 4.4); // bonk, spin, try somewhere else

                if (!Hidden && r.Cooldown <= 0 && Vec2.Distance(r.Position, Player) <= t.RoombaBumpRange)
                {
                    r.Cooldown = t.RoombaCooldown;
                    r.SinceBump = 0;
                    r.Heading = (r.Position - Player).Angle;
                    Emit(StageEventType.Bump, r.Position, "OBSTRUCTION REPORTED. SORRY.", r.Index);
                    if (r.Jammed) continue;
                    Detected();
                    Report(Jitter(Player, t.RoombaSigma), t.RoombaSigma, t.SensorOutlierWeight, ReportOrigin.Player);
                }
            }
        }

        // ---------------------------------------------------------------- WARDEN and its units

        private void Dispatch()
        {
            var t = Tuning;
            if (!Tracker.HasEstimate || Units.Count == 0) return;
            var best = Tracker.BestGuess;
            double confidence = Tracker.MassWithin(best, t.DispatchRadius);
            if (confidence < t.DispatchConfidence) return;
            bool everyone = confidence >= t.AllUnitsConfidence;

            var order = new List<PatrolUnit>(Units);
            order.Sort((a, b) => Vec2.Distance(a.Pose.Position, best).CompareTo(Vec2.Distance(b.Pose.Position, best)));
            foreach (var u in order)
            {
                if (u.Mode == GuardMode.Chase || u.Mode == GuardMode.CheckLocker) { if (!everyone) break; continue; }
                bool alreadyThere = (u.Mode == GuardMode.Investigate && Vec2.Distance(u.Goal, best) < 1.5) ||
                                    (u.Mode == GuardMode.Search && Vec2.Distance(u.SearchCenter, best) < 2.5);
                if (!alreadyThere)
                {
                    if (u.Mode == GuardMode.Patrol || u.Mode == GuardMode.Search) Bark(u, "INVESTIGATING.");
                    u.Mode = GuardMode.Investigate;
                    u.Goal = best;
                    u.AssignedOrigin = LastFixOrigin;
                }
                else if (u.Mode == GuardMode.Investigate)
                {
                    u.Goal = best;
                }
                if (!everyone) break;
            }
        }

        private void Bark(PatrolUnit u, string text) => Emit(StageEventType.GuardBark, u.Pose.Position, text, u.Index);

        private void UpdateUnit(PatrolUnit u)
        {
            var t = Tuning;

            // Vision.
            u.VisionTimer -= Dt;
            if (u.VisionTimer <= 0)
            {
                u.VisionTimer = t.GuardVisionPeriod;
                bool sees = Sees(u.Pose.Position, u.LookHeading, t.GuardFieldOfView, t.GuardVisionRange, out var apparent, out bool viaMirror, out _);
                u.SawPlayerLastCheck = sees;
                if (sees)
                {
                    if (u.Mode != GuardMode.Chase) Bark(u, "VISUAL CONTACT.");
                    u.Mode = GuardMode.Chase;
                    u.ChaseTarget = apparent;
                    u.ChaseTimer = t.ChaseMemory;
                    Detected();
                    if (!u.Jammed)
                    {
                        Report(Jitter(apparent, t.VisionSigma), t.VisionSigma * 1.5, t.SensorOutlierWeight * 0.1, viaMirror ? ReportOrigin.Mirror : ReportOrigin.Player);
                        Emit(StageEventType.Sighted, u.Pose.Position, "unit", u.Index, 0, viaMirror ? "Mirror" : "Player");
                    }
                }
                else if (!u.Jammed)
                {
                    ReportMiss(u.Pose.Position, t.GuardVisionRange, NaiveCoverage(u.Pose.Position, u.LookHeading, t.GuardFieldOfView, t.GuardVisionRange));
                }
            }

            double speed;
            switch (u.Mode)
            {
                case GuardMode.Chase:
                    u.ChaseTimer -= Dt;
                    u.Goal = u.ChaseTarget;
                    speed = t.ChaseSpeed;
                    if (u.ChaseTimer <= 0)
                    {
                        // Lost sight: fall back to WARDEN's belief, or search where it last saw you.
                        if (Tracker.HasEstimate) { u.Mode = GuardMode.Investigate; u.Goal = Tracker.BestGuess; }
                        else StartSearch(u, u.ChaseTarget);
                    }
                    break;
                case GuardMode.Investigate:
                    speed = t.InvestigateSpeed;
                    if (Vec2.Distance(u.Pose.Position, u.Goal) < 0.7 || (u.PathIndex >= u.Path.Count && u.ReplanTimer > 0 && u.Path.Count > 0))
                    {
                        Exploits.OnUnitArrived(u, u.Goal);
                        StartSearch(u, u.Goal);
                    }
                    break;
                case GuardMode.Search:
                    speed = 0;
                    u.SearchTimer -= Dt;
                    TryStartLockerCheck(u);
                    if (u.Mode == GuardMode.Search && u.SearchTimer <= 0)
                    {
                        u.Mode = GuardMode.Patrol;
                        Bark(u, "RESUMING PATROL.");
                        u.Goal = NearestWaypoint(u);
                    }
                    break;
                case GuardMode.CheckLocker:
                    speed = t.InvestigateSpeed;
                    u.Goal = u.LockerTarget;
                    if (Vec2.Distance(u.Pose.Position, u.LockerTarget) < 1.0)
                    {
                        speed = 0;
                        u.LockerTimer += Dt;
                        if (u.LockerTimer >= t.LockerCheckSeconds)
                        {
                            if (Hidden && HideSpotIndex == u.LockerIndex)
                            {
                                Capture(u);
                                return;
                            }
                            Bark(u, "LOCKER CLEAR.");
                            ReportMiss(u.LockerTarget, 0.8, p => Vec2.Distance(p, u.LockerTarget) <= 0.8);
                            u.CheckedLockers.Add(u.LockerIndex);
                            u.Mode = GuardMode.Search;
                        }
                    }
                    break;
                default:
                    speed = t.PatrolSpeed;
                    if (Vec2.Distance(u.Pose.Position, u.Route[u.RouteIndex]) < 0.6)
                        u.RouteIndex = (u.RouteIndex + 1) % u.Route.Count;
                    u.Goal = u.Route[u.RouteIndex];
                    break;
            }

            Move(u, speed * u.SpeedScale);

            if (!Hidden && Vec2.Distance(u.Pose.Position, Player) <= t.CaptureRadius) Capture(u);
        }

        private void StartSearch(PatrolUnit u, Vec2 center)
        {
            u.Mode = GuardMode.Search;
            u.SearchCenter = center;
            u.SearchTimer = Tuning.SearchDuration;
            u.CheckedLockers.Clear();
        }

        private void TryStartLockerCheck(PatrolUnit u)
        {
            var t = Tuning;
            for (int k = 0; k < HideSpots.Count; k++)
            {
                if (u.CheckedLockers.Contains(k)) continue;
                if (Vec2.Distance(HideSpots[k], u.SearchCenter) > 3.0) continue;
                if (Tracker.MassWithin(HideSpots[k], 1.5) < t.LockerCheckMass) continue;
                u.Mode = GuardMode.CheckLocker;
                u.LockerTarget = HideSpots[k];
                u.LockerIndex = k;
                u.LockerTimer = 0;
                Bark(u, "CHECKING HIDING SPOT.");
                return;
            }
        }

        private Vec2 NearestWaypoint(PatrolUnit u)
        {
            int best = 0;
            double bd = double.PositiveInfinity;
            for (int k = 0; k < u.Route.Count; k++)
            {
                double d = Vec2.Distance(u.Route[k], u.Pose.Position);
                if (d < bd) { bd = d; best = k; }
            }
            u.RouteIndex = best;
            return u.Route[best];
        }

        private void Move(PatrolUnit u, double speed)
        {
            var t = Tuning;
            u.ReplanTimer -= Dt;
            if (u.ReplanTimer <= 0 || Vec2.Distance(u.PathGoal, u.Goal) > 0.5)
            {
                u.ReplanTimer = 0.5;
                u.PathGoal = u.Goal;
                u.Path = _pathfinder.FindPath(u.Pose.Position, u.Goal);
                u.PathIndex = 0;
            }

            double heading = u.Pose.Theta;
            var pos = u.Pose.Position;
            double budget = speed * Dt;
            while (budget > 1e-9 && u.PathIndex < u.Path.Count)
            {
                var target = u.Path[u.PathIndex];
                var to = target - pos;
                double d = to.Length;
                if (d < 1e-6) { u.PathIndex++; continue; }
                double turn = Angle.Difference(to.Angle, heading);
                double maxTurn = t.GuardTurnRate * Dt;
                heading += System.Math.Max(-maxTurn, System.Math.Min(maxTurn, turn));
                if (System.Math.Abs(turn) > 1.2) break;
                double step = System.Math.Min(budget, d);
                pos += to / d * step;
                budget -= step;
                if (step >= d - 1e-9) u.PathIndex++;
            }
            u.Pose = new Pose2(pos, Angle.Wrap(heading));

            if (u.Mode == GuardMode.Search || u.Mode == GuardMode.CheckLocker && speed == 0)
            {
                u.ScanPhase += Dt * 1.6;
                u.LookHeading = Angle.Wrap(u.Pose.Theta + 1.3 * System.Math.Sin(u.ScanPhase));
            }
            else if (u.Mode == GuardMode.Chase)
            {
                var toTarget = u.ChaseTarget - u.Pose.Position;
                u.LookHeading = toTarget.LengthSquared > 0.01 ? toTarget.Angle : u.Pose.Theta;
            }
            else
            {
                u.ScanPhase += Dt * 0.9;
                u.LookHeading = Angle.Wrap(u.Pose.Theta + 0.35 * System.Math.Sin(u.ScanPhase));
            }
        }

        private void Capture(PatrolUnit u)
        {
            Captures++;
            Emit(StageEventType.Captured, Player, "DETAINED.", u.Index);
            // Back to the last checkpoint. WARDEN loses track while you're escorted; its units go back on patrol.
            Hidden = false;
            HideSpotIndex = -1;
            Player = _checkpoint;
            Tracker.Reset();
            Projectiles.Clear();
            Noisemakers.Clear();
            foreach (var unit in Units)
            {
                unit.Mode = GuardMode.Patrol;
                unit.Pose = new Pose2(unit.Route[0], 0);
                unit.RouteIndex = 0;
                unit.Path.Clear();
                unit.PathIndex = 0;
                unit.Goal = unit.Route[0];
            }
            Recorder.CountCapture();
        }

        internal void Emit(StageEventType type, Vec2 pos, string text = null, int unit = -1, double radius = 0, string tag = null)
        {
            _events.Add(new StageEvent { Type = type, Time = Time, Position = pos, Text = text, Unit = unit, Radius = radius, Tag = tag });
            Recorder.Log(type, Time, pos, text);
        }
    }
}
