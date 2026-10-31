using System.IO;
using LowObservable.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LowObservable.EditorTools
{
    /// <summary>Editor menu: one click to a playable scene, plus asset helpers.</summary>
    public static class LowObservableMenu
    {
        public const string ScenePath = "Assets/LowObservable/Scenes/Main.unity";

        [MenuItem("Low Observable/Create Sample Scene", priority = 0)]
        public static void CreateSampleScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateSceneAt(ScenePath);
            Debug.Log($"[LowObservable] Created {ScenePath}. Press Play.");
        }

        /// <summary>Creates (or overwrites) the bootstrap scene and registers it as the first build scene.</summary>
        public static void CreateSceneAt(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            camGo.transform.position = new Vector3(0, 0, -10);

            var root = new GameObject("Low Observable");
            root.AddComponent<LowObservableBootstrap>();

            EditorSceneManager.SaveScene(scene, path);
            var scenes = EditorBuildSettings.scenes;
            bool present = false;
            foreach (var s in scenes) present |= s.path == path;
            if (!present)
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
                list.AddRange(scenes);
                EditorBuildSettings.scenes = list.ToArray();
            }
        }

        [MenuItem("Low Observable/Create Tuning Asset")]
        public static void CreateTuningAsset()
        {
            var asset = ScriptableObject.CreateInstance<TuningAsset>();
            CreateAsset(asset, "Assets/LowObservable/Tuning.asset");
        }

        [MenuItem("Low Observable/Create Level Asset (Annex B)")]
        public static void CreateLevelAsset()
        {
            var asset = ScriptableObject.CreateInstance<LevelAsset>();
            asset.InlineText = Core.Levels.BuiltInLevels.AnnexB.Trim();
            CreateAsset(asset, "Assets/LowObservable/AnnexB.asset");
        }

        [MenuItem("Low Observable/Open Docs Folder")]
        public static void OpenDocs()
        {
            EditorUtility.RevealInFinder(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../docs")));
        }

        private static void CreateAsset(Object asset, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
