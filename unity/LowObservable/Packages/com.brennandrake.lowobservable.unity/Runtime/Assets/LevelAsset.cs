using LowObservable.Core.Levels;
using LowObservable.Core.World;
using UnityEngine;

namespace LowObservable.Unity
{
    /// <summary>
    /// A floor plan as an asset. Point it at a .txt file in the ASCII level format (docs/level-format.md),
    /// paste the text inline, or name a built-in level. Create via Assets > Create > Low Observable > Level.
    /// </summary>
    [CreateAssetMenu(menuName = "Low Observable/Level", fileName = "Level")]
    public sealed class LevelAsset : ScriptableObject
    {
        [Tooltip("ASCII level file. Takes priority over the inline text.")]
        public TextAsset LevelFile;

        [TextArea(10, 40)]
        [Tooltip("Inline ASCII level, used when no file is assigned.")]
        public string InlineText;

        [Tooltip("Used when neither a file nor inline text is set: 'annex-b' or 'open-room'.")]
        public string BuiltInName = "annex-b";

        [Min(0.25f)]
        public float CellSize = 1f;

        public FloorPlan Load()
        {
            if (LevelFile != null) return FloorPlanParser.Parse(LevelFile.name, LevelFile.text, CellSize);
            if (!string.IsNullOrWhiteSpace(InlineText)) return FloorPlanParser.Parse(name, InlineText, CellSize);
            return BuiltInLevels.Load(BuiltInName);
        }
    }
}
