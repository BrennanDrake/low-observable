using LowObservable.Core.Maths;

namespace LowObservable.Core.Stages
{
    public enum MoveMode
    {
        Sneak,
        Walk,
        Run,
    }

    /// <summary>Things the player can use. Throwing is always available while carrying something.</summary>
    public enum ToolKind
    {
        Throw,
        Noisemaker,
        Jammer,
        Mirror,
        Forge,
    }

    public static class ToolInfo
    {
        public static readonly ToolKind[] All = { ToolKind.Throw, ToolKind.Noisemaker, ToolKind.Jammer, ToolKind.Mirror, ToolKind.Forge };

        public static bool TryParse(string s, out ToolKind kind)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "throw": kind = ToolKind.Throw; return true;
                case "noisemaker": kind = ToolKind.Noisemaker; return true;
                case "jammer": kind = ToolKind.Jammer; return true;
                case "mirror": kind = ToolKind.Mirror; return true;
                case "forge": kind = ToolKind.Forge; return true;
                default: kind = ToolKind.Throw; return false;
            }
        }

        public static string Name(ToolKind kind)
        {
            switch (kind)
            {
                case ToolKind.Throw: return "Throw";
                case ToolKind.Noisemaker: return "Noisemaker";
                case ToolKind.Jammer: return "Jammer";
                case ToolKind.Mirror: return "Mirror";
                default: return "Forged fix";
            }
        }

        public static string Id(ToolKind kind) => kind.ToString().ToLowerInvariant();

        public static string Description(ToolKind kind)
        {
            switch (kind)
            {
                case ToolKind.Throw: return "Throw something you're carrying. It makes one sound where it lands, and you can pick it up again.";
                case ToolKind.Noisemaker: return "A desk toy that makes footstep-loud bursts. Sensors can't tell it from you. It sticks to roombas.";
                case ToolKind.Jammer: return "Sensors inside its radius report nothing, not even 'nobody here'. WARDEN notices the interference itself.";
                case ToolKind.Mirror: return "Cameras that see you in it report you at your reflection. They also think they can see through it.";
                default: return "At a maintenance terminal, upload a fake sighting WARDEN will be very sure of.";
            }
        }
    }

    /// <summary>One physics tick of player intent. Built by Unity input or by the bot.</summary>
    public struct StageInput
    {
        /// <summary>Desired movement direction in world space; length is clamped to 1.</summary>
        public Vec2 Move;
        public MoveMode Mode;
        /// <summary>True on the tick the interact button goes down.</summary>
        public bool InteractPressed;
        /// <summary>True while the interact button is held (terminals).</summary>
        public bool InteractHeld;
        /// <summary>True on the tick the use-tool button goes down.</summary>
        public bool UsePressed;
        public ToolKind Tool;
        /// <summary>World point the tool is aimed at (mouse).</summary>
        public Vec2 Aim;
        /// <summary>Mirror surface direction in radians.</summary>
        public double MirrorAngle;
    }
}
