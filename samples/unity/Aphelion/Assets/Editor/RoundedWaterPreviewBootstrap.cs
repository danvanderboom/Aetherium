using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aphelion.EditorTools
{
    /// <summary>
    /// One-click offline preview of the shared client's rounded-water rendering. Menu:
    /// Aetherium → Build Rounded Water Preview. Creates a tiny scene (camera + a
    /// <see cref="RoundedWaterPreview"/> object) that renders a lake through the package's
    /// RoundedRegionRenderer with no server. Press Play to see it. Safe to re-run.
    /// </summary>
    public static class RoundedWaterPreviewBootstrap
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/RoundedWaterPreview.unity";

        [MenuItem("Aetherium/Build Rounded Water Preview")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camObj = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.72f, 0.90f);
            camObj.transform.position = new Vector3(0f, 12f, -10f);
            camObj.transform.LookAt(Vector3.zero);

            var preview = new GameObject("RoundedWaterPreview");
            preview.AddComponent<RoundedWaterPreview>();

            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[Aetherium] Rounded Water Preview scene built at " + ScenePath +
                      ". Press Play to see rounded water — no server needed.");
        }
    }
}
