using System.Collections.Generic;
using LowObservable.Core.Maths;

namespace LowObservable.Core.Stages
{
    public sealed class Door
    {
        public int Index;
        public char Marker;
        public int I;
        public int J;
        public Vec2 Position;
        /// <summary>Keycard name that opens it on touch, or null if only a terminal opens it.</summary>
        public string Key;
        public bool Open;
    }

    public sealed class Terminal
    {
        public int Index;
        public char Marker;
        public Vec2 Position;
        public bool Forge;
        public readonly List<int> Opens = new List<int>();
        public double Progress;
        public double BeepTimer;
        public bool Done;
        public bool ForgeUsed;
    }

    public sealed class Keycard
    {
        public char Marker;
        public Vec2 Position;
        public string Name;
        public bool Taken;
    }

    public sealed class LightSwitch
    {
        public char Marker;
        public Vec2 Position;
        public int Circuit;
    }

    public sealed class ToolPickup
    {
        public char Marker;
        public Vec2 Position;
        public ToolKind Tool;
        public bool Taken;
    }

    public enum NoteKind
    {
        Sticky,
        Poster,
        Whiteboard,
        Graffiti,
        Ticket,
        Screen,
        Chalk,
    }

    /// <summary>Environmental storytelling: something written in the world. Read by walking up to it.</summary>
    public sealed class Note
    {
        public int Index;
        public char Marker;
        public Vec2 Position;
        public string Text;
        public NoteKind Kind;
        public bool Read;
    }

    /// <summary>A throwable object lying on the floor (cup, stapler, tray).</summary>
    public sealed class Throwable
    {
        public Vec2 Position;
        public bool Taken;
    }

    public sealed class Projectile
    {
        public Vec2 From;
        public Vec2 To;
        public Vec2 Position;
        public double Elapsed;
        public double Duration;
        public bool IsNoisemaker;
    }

    public sealed class Noisemaker
    {
        public Vec2 Position;
        public int BurstsLeft;
        public double Timer;
        /// <summary>Index of the roomba it rides on, or -1.</summary>
        public int Roomba = -1;
        public bool Active => BurstsLeft > 0;
    }

    public sealed class Jammer
    {
        public Vec2 Position;
        public double Radius;
        public double TimeLeft;
        public double InterferenceTimer;
        public bool Active => TimeLeft > 0;
    }

    /// <summary>A two-sided flat mirror panel. Vision rays reflect off it; sensors' own models assume they don't.</summary>
    public sealed class Mirror
    {
        public Vec2 Center;
        /// <summary>Direction of the mirror's surface (along the panel), radians.</summary>
        public double Angle;
        public double Length;

        public Vec2 A => Center - Vec2.FromAngle(Angle, Length * 0.5);
        public Vec2 B => Center + Vec2.FromAngle(Angle, Length * 0.5);

        /// <summary>Reflection of a point across the mirror's line: where a camera thinks a reflected object is.</summary>
        public Vec2 Reflect(Vec2 p)
        {
            var u = Vec2.FromAngle(Angle);
            var d = p - Center;
            var along = u * Vec2.Dot(d, u);
            var across = d - along;
            return Center + along - across;
        }
    }
}
