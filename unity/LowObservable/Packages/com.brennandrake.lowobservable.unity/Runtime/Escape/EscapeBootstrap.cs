using UnityEngine;

namespace LowObservable.Unity.Escape
{
    /// <summary>
    /// Fastest way to play the escape campaign: add this one component to an empty GameObject in an empty
    /// scene and press Play (or use Low Observable > Create Escape Scene). It builds the game, every view and
    /// the camera from code, so the MVP needs no prefabs, sprites or scene wiring.
    /// </summary>
    public sealed class EscapeBootstrap : MonoBehaviour
    {
        [Tooltip("Optional folder of .lvl files (relative to the Unity project) to use instead of the built-in levels.")]
        public string LevelsFolder = "";
        public ulong Seed = 1;
        public bool UnlockAllLevels;
        [Tooltip("Skip the menu and start this level id, e.g. 04-open-plan.")]
        public string StartLevel = "";

        private void Awake()
        {
            // The game must exist (and load its levels) before views look for it.
            var gameGo = new GameObject("Escape Game");
            gameGo.SetActive(false);
            var input = gameGo.AddComponent<EscapeInput>();
            var game = gameGo.AddComponent<EscapeGame>();
            game.LevelsFolder = LevelsFolder;
            game.Seed = Seed;
            game.UnlockAll = UnlockAllLevels;
            game.StartLevel = StartLevel;
            game.Input = input;
            input.Game = game;
            gameGo.SetActive(true);

            var views = new GameObject("Escape Views");
            views.AddComponent<StageMapView>().Game = game;
            views.AddComponent<BeliefView>().Game = game;
            views.AddComponent<SecurityView>().Game = game;
            views.AddComponent<PlayerView>().Game = game;
            views.AddComponent<EscapeHud>().Game = game;
            views.AddComponent<EscapeMenus>().Game = game;

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }
            var rig = cam.GetComponent<EscapeCamera>();
            if (rig == null) rig = cam.gameObject.AddComponent<EscapeCamera>();
            rig.Game = game;
            input.AimCamera = cam;
        }
    }
}
