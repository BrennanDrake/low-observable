using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LowObservable.Core.Estimation;

namespace LowObservable.Core.Simulation
{
    /// <summary>One row of the run log: truth, the player's belief, and the Seeker's belief at a moment.</summary>
    public struct RunSample
    {
        public double Time;
        public double TrueX, TrueY, TrueTheta;
        public double EstX, EstY, EstTheta;
        public double CovXX, CovXY, CovYY, CovTT;
        public double SeekerX, SeekerY, SeekerLook;
        public int SeekerMode;
        public double SeekerBeliefX, SeekerBeliefY, SeekerBeliefSpread, SeekerEss;
        public int Mode;
    }

    public sealed class RunSummary
    {
        public string Level;
        public ulong Seed;
        public EpisodeOutcome Outcome;
        public double Duration;
        public double AverageError;
        public double MaxError;
        public double AverageNees;
        public double FractionInsideEllipse;
        public int CorrectAssociations;
        public int WrongAssociations;
        public int Pings;
        public int DecoysThrown;
        public int TimesSeen;
        public int TimesHeard;
    }

    /// <summary>
    /// Records an episode for the reveal screen and for offline analysis (tools/plot_run.py).
    /// Writes plain CSV so it needs no JSON library in Unity or .NET and opens in anything.
    /// </summary>
    public sealed class RunRecorder
    {
        private readonly List<RunSample> _samples = new List<RunSample>();
        private readonly List<EpisodeEvent> _events = new List<EpisodeEvent>();

        public string Level { get; }
        public ulong Seed { get; }
        public IReadOnlyList<RunSample> Samples => _samples;
        public IReadOnlyList<EpisodeEvent> Events => _events;
        public RunSummary Summary { get; private set; }

        public RunRecorder(string level, ulong seed)
        {
            Level = level;
            Seed = seed;
        }

        public void AddSample(RunSample s) => _samples.Add(s);
        public void AddEvent(EpisodeEvent e) => _events.Add(e);

        public void Finish(EpisodeOutcome outcome, double duration, ConsistencyTracker consistency, int correct, int wrong)
        {
            var s = new RunSummary
            {
                Level = Level,
                Seed = Seed,
                Outcome = outcome,
                Duration = duration,
                AverageError = consistency.AverageError,
                MaxError = consistency.MaxError,
                AverageNees = consistency.AverageNees,
                FractionInsideEllipse = consistency.FractionInside,
                CorrectAssociations = correct,
                WrongAssociations = wrong,
            };
            foreach (var e in _events)
            {
                if (e.Type == EpisodeEventType.PingFired) s.Pings++;
                else if (e.Type == EpisodeEventType.DecoyThrown) s.DecoysThrown++;
                else if (e.Type == EpisodeEventType.SeekerSaw) s.TimesSeen++;
                else if (e.Type == EpisodeEventType.SeekerHeard) s.TimesHeard++;
            }
            Summary = s;
        }

        public const string SampleHeader =
            "time,true_x,true_y,true_theta,est_x,est_y,est_theta,cov_xx,cov_xy,cov_yy,cov_tt," +
            "seeker_x,seeker_y,seeker_look,seeker_mode,seeker_belief_x,seeker_belief_y,seeker_belief_spread,seeker_ess,mode";

        public void WriteSamplesCsv(TextWriter w)
        {
            w.WriteLine(SampleHeader);
            foreach (var s in _samples)
            {
                w.WriteLine(Join(s.Time, s.TrueX, s.TrueY, s.TrueTheta, s.EstX, s.EstY, s.EstTheta,
                    s.CovXX, s.CovXY, s.CovYY, s.CovTT, s.SeekerX, s.SeekerY, s.SeekerLook, s.SeekerMode,
                    s.SeekerBeliefX, s.SeekerBeliefY, s.SeekerBeliefSpread, s.SeekerEss, s.Mode));
            }
        }

        public void WriteEventsCsv(TextWriter w)
        {
            w.WriteLine("time,type,x,y,value,landmark_id,association_correct");
            foreach (var e in _events)
            {
                w.WriteLine(string.Join(",",
                    F(e.Time), e.Type.ToString(), F(e.Position.X), F(e.Position.Y), F(e.Value),
                    e.LandmarkId.ToString(CultureInfo.InvariantCulture), e.AssociationCorrect ? "1" : "0"));
            }
        }

        public static string SummaryHeader =>
            "level,seed,outcome,duration,avg_error,max_error,avg_nees,fraction_inside,correct_assoc,wrong_assoc,pings,decoys,times_seen,times_heard";

        public static string SummaryRow(RunSummary s)
        {
            return string.Join(",", s.Level, s.Seed.ToString(CultureInfo.InvariantCulture), s.Outcome.ToString(),
                F(s.Duration), F(s.AverageError), F(s.MaxError), F(s.AverageNees), F(s.FractionInsideEllipse),
                s.CorrectAssociations, s.WrongAssociations, s.Pings, s.DecoysThrown, s.TimesSeen, s.TimesHeard);
        }

        private static string Join(params double[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++) parts[i] = F(values[i]);
            return string.Join(",", parts);
        }

        private static string F(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
    }
}
