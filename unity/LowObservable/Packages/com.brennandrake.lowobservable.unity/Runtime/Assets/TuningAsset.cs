using LowObservable.Core.Simulation;
using UnityEngine;

namespace LowObservable.Unity
{
    /// <summary>
    /// Designer-editable copy of <see cref="TuningParameters"/>. Every public field of the core class
    /// shows up in the inspector because the class is [Serializable]. Angles are stored in radians;
    /// docs/tuning.md lists the defaults in degrees. Create via Assets > Create > Low Observable > Tuning.
    /// </summary>
    [CreateAssetMenu(menuName = "Low Observable/Tuning", fileName = "Tuning")]
    public sealed class TuningAsset : ScriptableObject
    {
        public TuningParameters Parameters = TuningParameters.Default();

        /// <summary>Returns a copy so a running episode is not affected by inspector edits mid-run.</summary>
        public TuningParameters Snapshot() => Parameters.Clone();

        [ContextMenu("Reset To Defaults")]
        private void ResetToDefaults()
        {
            Parameters = TuningParameters.Default();
        }
    }
}
