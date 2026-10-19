using LowObservable.Unity.Input;
using LowObservable.Unity.Views;
using UnityEngine;

namespace LowObservable.Unity
{
    /// <summary>
    /// Quickest way to play: add this one component to an empty GameObject in an empty scene and press
    /// Play. It builds the runner, every view and the camera from code, so the MVP needs no prefabs,
    /// sprites or scene wiring. Swap pieces for hand-made ones as art arrives; each view is independent.
    /// </summary>
    public sealed class LowObservableBootstrap : MonoBehaviour
    {
        public LevelAsset Level;
        public TuningAsset Tuning;
        public string BuiltInLevel = "annex-b";
        public ulong Seed = 1;
        [Tooltip("Let the autopilot play instead of the keyboard (attract mode / balance watching).")]
        public bool Autopilot;
        public string AutopilotProfile = "cautious";

        private void Awake()
        {
            // The runner must exist (and configure itself) before views look for it.
            var runnerGo = new GameObject("Episode Runner");
            runnerGo.SetActive(false);
            var runner = runnerGo.AddComponent<EpisodeRunner>();
            runner.Level = Level;
            runner.Tuning = Tuning;
            runner.BuiltInLevel = BuiltInLevel;
            runner.Seed = Seed;
            if (Autopilot)
            {
                var pilot = runnerGo.AddComponent<AutopilotInput>();
                pilot.Profile = AutopilotProfile;
                runner.InputSource = pilot;
            }
            else
            {
                runner.InputSource = runnerGo.AddComponent<KeyboardMouseInput>();
            }
            runnerGo.SetActive(true);

            var views = new GameObject("Views");
            views.AddComponent<FloorView>().Runner = runner;
            views.AddComponent<SeekerView>().Runner = runner;
            views.AddComponent<PlayerBeliefView>().Runner = runner;
            views.AddComponent<TruthOverlayView>().Runner = runner;
            views.AddComponent<RevealView>().Runner = runner;
            views.AddComponent<HudView>().Runner = runner;

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }
            var rig = cam.GetComponent<CameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
            rig.Runner = runner;
        }
    }
}
