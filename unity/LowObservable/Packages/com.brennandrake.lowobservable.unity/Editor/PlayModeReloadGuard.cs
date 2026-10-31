using UnityEditor;
using UnityEngine;

namespace LowObservable.EditorTools
{
    /// <summary>
    /// The core <c>Episode</c> and <c>Stage</c> are plain C# objects that Unity cannot serialize, so a script
    /// reload during Play mode (saving a script, switching git branch) leaves every runner and view holding
    /// null and throwing NullReferenceException each frame. Stop Play mode instead; pressing Play again
    /// starts cleanly on the new code.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeReloadGuard
    {
        static PlayModeReloadGuard()
        {
            AssemblyReloadEvents.afterAssemblyReload += () =>
            {
                if (!EditorApplication.isPlaying) return;
                Debug.LogWarning("[LowObservable] Scripts reloaded during Play mode; stopping Play. Press Play to restart on the new code.");
                EditorApplication.isPlaying = false;
            };
        }
    }
}
