using Aetherium.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aphelion.EditorTools
{
    /// <summary>
    /// One-click First Light scene builder (M0). Menu: Aetherium → Build First Light Scene.
    /// Creates primitive-prefab stand-ins (real meshes arrive with the beauty pass), a
    /// ThemeAsset bound to the aphelion bundle's terrain names, and a scene wired with the
    /// client rig — so the very first Play-mode run shows the station instead of an empty
    /// hierarchy. Safe to re-run: assets are overwritten in place.
    /// </summary>
    public static class AphelionSceneBootstrap
    {
        private const string RootFolder = "Assets/Aetherium";
        private const string MaterialsFolder = "Assets/Aetherium/Materials";
        private const string PrefabsFolder = "Assets/Aetherium/Prefabs";
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/FirstLight.unity";
        private const string ThemePath = "Assets/Aetherium/AphelionTheme.asset";

        [MenuItem("Aetherium/Build First Light Scene")]
        public static void Build()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(ScenesFolder);

            // --- Stand-in prefabs (primitive geometry, URP Lit materials). ---
            var wall = CubePrefab("Wall", new Color(0.33f, 0.38f, 0.44f), height: 1f);
            var door = CubePrefab("Door", new Color(0.75f, 0.55f, 0.25f), height: 0.9f);
            var floor = CubePrefab("Floor", new Color(0.16f, 0.18f, 0.21f), height: 0.08f, sinkTopToGround: true);
            var player = CapsulePrefab("Player", new Color(0.25f, 0.85f, 0.95f));
            var creature = CapsulePrefab("Creature", new Color(0.95f, 0.45f, 0.2f));
            var item = SpherePrefab("Item", new Color(0.95f, 0.8f, 0.3f));

            // --- Theme asset: bundle content ids → stand-ins. ---
            var theme = AssetDatabase.LoadAssetAtPath<ThemeAsset>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<ThemeAsset>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            var so = new SerializedObject(theme);
            SetBindings(so.FindProperty("terrain"), ("Wall", wall), ("Door", door));
            so.FindProperty("defaultTerrainPrefab").objectReferenceValue = floor;
            so.FindProperty("defaultCreaturePrefab").objectReferenceValue = creature;
            so.FindProperty("defaultItemPrefab").objectReferenceValue = item;
            so.FindProperty("playerPrefab").objectReferenceValue = player;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);

            // --- Scene. ---
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<AphelionCameraRig>();
            cameraObject.transform.position = new Vector3(0f, 12f, -9f);
            cameraObject.transform.LookAt(Vector3.zero);

            var rig = new GameObject("Aetherium");
            var behaviour = rig.AddComponent<AetheriumClientBehaviour>();
            var behaviourSo = new SerializedObject(behaviour);
            behaviourSo.FindProperty("serverUrl").stringValue = "http://localhost:50310";
            // worldId stays empty: connect joins the server's default session. Paste an
            // aphelion instance id here (or via the Inspector) to play the sample bundle.
            behaviourSo.ApplyModifiedPropertiesWithoutUndo();

            var mapView = rig.AddComponent<GridMapView>();
            var mapSo = new SerializedObject(mapView);
            mapSo.FindProperty("theme").objectReferenceValue = theme;
            mapSo.ApplyModifiedPropertiesWithoutUndo();

            var entityView = rig.AddComponent<EntityViewRegistry>();
            var entitySo = new SerializedObject(entityView);
            entitySo.FindProperty("theme").objectReferenceValue = theme;
            entitySo.ApplyModifiedPropertiesWithoutUndo();

            rig.AddComponent<AphelionPlayerController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("[Aphelion] First Light scene built. Start the Aetherium server, then press Play.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static void SetBindings(SerializedProperty list, params (string id, GameObject prefab)[] bindings)
        {
            list.arraySize = bindings.Length;
            for (var i = 0; i < bindings.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = bindings[i].id;
                element.FindPropertyRelative("prefab").objectReferenceValue = bindings[i].prefab;
            }
        }

        private static Material MakeMaterial(string name, Color color)
        {
            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CubePrefab(string name, Color color, float height, bool sinkTopToGround = false)
        {
            // Child offset puts the shape's base (or top, for floor slabs) at the cell's y=0.
            var y = sinkTopToGround ? -height / 2f : height / 2f;
            return SavePrefab(name, PrimitiveType.Cube, color, new Vector3(0.98f, height, 0.98f), y);
        }

        private static GameObject CapsulePrefab(string name, Color color) =>
            SavePrefab(name, PrimitiveType.Capsule, color, new Vector3(0.45f, 0.45f, 0.45f), 0.45f);

        private static GameObject SpherePrefab(string name, Color color) =>
            SavePrefab(name, PrimitiveType.Sphere, color, new Vector3(0.35f, 0.35f, 0.35f), 0.2f);

        private static GameObject SavePrefab(
            string name, PrimitiveType primitive, Color color, Vector3 scale, float childY)
        {
            var root = new GameObject(name);
            var shape = GameObject.CreatePrimitive(primitive);
            shape.name = "shape";
            shape.transform.SetParent(root.transform, false);
            shape.transform.localPosition = new Vector3(0f, childY, 0f);
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = MakeMaterial(name, color);

            var path = $"{PrefabsFolder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
