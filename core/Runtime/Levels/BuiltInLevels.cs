namespace LowObservable.Core.Levels
{
    /// <summary>
    /// Levels compiled into the core so tests, the headless simulator and a fresh Unity scene all
    /// have something to run. The format is documented in docs/level-format.md. In Unity, levels can
    /// also come from a FloorPlanAsset (a text asset), which uses the same parser.
    /// </summary>
    public static class BuiltInLevels
    {
        /// <summary>
        /// MVP floor "Annex B". Start bottom-left, uplink bottom-right, Seeker in the central hub.
        /// Routes, each teaching something:
        ///   - Dark, slick service corridor along the bottom: shortest, but the floor slips more than the filter assumes and
        ///     the camera is useless, so you must ping, and the hub next door hears pings through the wall.
        ///   - Through the hub: direct, lit, and right past the Seeker.
        ///   - North loop through the twin labs and the dark storage: long and hidden, but the two labs look
        ///     identical (landmarks c, d, e appear in both), so camera matches can put you in the wrong lab.
        /// </summary>
        public const string AnnexB = @"
; Annex B - MVP floor (46 x 24). See docs/level-format.md.
##############################################
#..........#####,,,,,,,,,,,,,,#####..........#
#.c........#####,,,,,,,,,,,,,,#####........c.#
#...............,,F,,,,,,,,,,,,,,,,..........#
#....e..........,,,,,,,,,,,,,,,,,,,.....e....#
#..........#####,,,,,,F,,,,,,,#####..........#
#........d.#####,,,,,,,,,,,,,,#####.d........#
#..........#####,,,,,,,,,,,G,,#####..........#
####...#########,,,,,,,,,,,,,,#########...####
####...#########,,,,,,,,,,,,,,#########...####
####...##############....##############...####
####...##############....##############...####
####...#######..................####.........#
####...#######.a.......K........####.k.....m.#
####............................####.........#
####..................b......................#
####...#######...............................#
#..........###................a.####.......U.#
#.h........###..................####.........#
#..........#########################.........#
#.........;;;;;;;;;;;;;;;J;;;;;;;;;;.........#
#..^......;;;;;;;;;;;;;;;;;;;;;;;;;;.......k.#
#........i.#########################.........#
##############################################
";

        /// <summary>One open room with a few landmarks. Used for the pre-MVP spike and unit tests.</summary>
        public const string OpenRoom = @"
; Open room - spike / tests
####################
#a........b.......c#
#..................#
#..................#
#.......,,,,.......#
#.>.....,,,,....K..#
#.......,,,,.......#
#..................#
#..................#
#d........e.....U.f#
####################
";

        public static World.FloorPlan Load(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "annexb":
                case "annex-b":
                case "mvp":
                    return World.FloorPlanParser.Parse("annex-b", AnnexB);
                case "openroom":
                case "open-room":
                case "spike":
                    return World.FloorPlanParser.Parse("open-room", OpenRoom);
                default:
                    throw new System.ArgumentException("Unknown built-in level '" + name + "'. Try 'annex-b' or 'open-room'.");
            }
        }
    }
}
