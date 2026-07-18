using Aetherium.Unity;
using Aphelion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Overworld.EditorTools
{
    /// <summary>
    /// One-click Overworld scene builder. Menu: Aetherium → Build Overworld Scene.
    /// Builds primitive-prefab stand-ins for the open-world terrain palette (plains, forest,
    /// desert, hills, mountains, water, road, walls, floors, and a translucent window wall),
    /// a ThemeAsset bound to those terrain names, and a scene wired with the client rig and an
    /// OverworldPlayerController — so pressing Play drops you into the sandbox (once you paste
    /// an overworld instance id into the AetheriumClient rig). Safe to re-run.
    ///
    /// <para>Reuses the AphelionCameraRig (it just follows the perception anchor) and relies on
    /// AphelionSceneBootstrap's InitializeOnLoad URP repair — no URP setup duplicated here.</para>
    /// </summary>
    public static class OverworldSceneBootstrap
    {
        private const string RootFolder = "Assets/Aetherium";
        private const string MaterialsFolder = "Assets/Aetherium/Materials";
        private const string PrefabsFolder = "Assets/Aetherium/Prefabs/Overworld";
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/Overworld.unity";
        private const string ThemePath = "Assets/Aetherium/OverworldTheme.asset";

        // Terrain name → (color, kind). Ground kinds are thin floor slabs; Block kinds are
        // full-height occluders; Glass is a translucent full-height pane (the window wall);
        // Liquid is a translucent floor slab (water you see across but can't wade).
        private enum Kind { Ground, Bump, Block, Glass, Liquid }

        private static readonly (string name, Color color, Kind kind)[] Terrain =
        {
            ("Plains",     new Color(0.42f, 0.58f, 0.30f), Kind.Ground),
            ("Forest",     new Color(0.17f, 0.39f, 0.20f), Kind.Ground),
            ("Desert",     new Color(0.83f, 0.75f, 0.50f), Kind.Ground),
            ("Hills",      new Color(0.52f, 0.50f, 0.28f), Kind.Bump),
            ("Road",       new Color(0.30f, 0.28f, 0.26f), Kind.Ground),
            ("Indoors",    new Color(0.52f, 0.43f, 0.32f), Kind.Ground),
            ("Mountain",   new Color(0.44f, 0.44f, 0.48f), Kind.Block),
            ("Wall",       new Color(0.55f, 0.55f, 0.60f), Kind.Block),
            ("Water",      new Color(0.16f, 0.36f, 0.70f), Kind.Liquid),
            ("WindowWall", new Color(0.60f, 0.82f, 0.92f), Kind.Glass),
        };

        [MenuItem("Aetherium/Build Overworld Scene")]
        public static void Build()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder("Assets/Aetherium/Prefabs");
            EnsureFolder(PrefabsFolder);
            EnsureFolder(ScenesFolder);

            // --- Terrain prefabs + theme bindings. ---
            var theme = AssetDatabase.LoadAssetAtPath<ThemeAsset>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<ThemeAsset>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            var bindings = new (string, GameObject)[Terrain.Length];
            GameObject plains = null;
            for (var i = 0; i < Terrain.Length; i++)
            {
                var t = Terrain[i];
                var prefab = TerrainPrefab(t.name, t.color, t.kind);
                bindings[i] = (t.name, prefab);
                if (t.name == "Plains") plains = prefab;
            }

            var player = CapsulePrefab("OverworldPlayer", new Color(0.25f, 0.85f, 0.95f));
            var item = SpherePrefab("OverworldItem", new Color(0.95f, 0.85f, 0.25f)); // the key reads as this
            var creatureDefault = CapsulePrefab("OverworldCreature", new Color(0.9f, 0.4f, 0.3f)); // unused (no monsters)

            var so = new SerializedObject(theme);
            SetBindings(so.FindProperty("terrain"), bindings);
            so.FindProperty("defaultTerrainPrefab").objectReferenceValue = plains; // unknown terrain → ground, never magenta
            so.FindProperty("defaultCreaturePrefab").objectReferenceValue = creatureDefault;
            so.FindProperty("defaultItemPrefab").objectReferenceValue = item;
            so.FindProperty("playerPrefab").objectReferenceValue = player;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);

            // --- Scene. ---
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.97f, 0.88f);
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.72f, 0.9f); // daytime sky
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<AphelionCameraRig>();
            cameraObject.transform.position = new Vector3(0f, 14f, -11f);
            cameraObject.transform.LookAt(Vector3.zero);

            var rig = new GameObject("Aetherium");
            var behaviour = rig.AddComponent<AetheriumClientBehaviour>();
            var behaviourSo = new SerializedObject(behaviour);
            behaviourSo.FindProperty("serverUrl").stringValue = "http://localhost:50310";
            // worldId stays empty: paste an overworld instance id here (or in the Inspector) —
            //   Invoke-RestMethod -Method Post http://localhost:50310/api/management/games/overworld/instances
            behaviourSo.ApplyModifiedPropertiesWithoutUndo();

            var mapView = rig.AddComponent<GridMapView>();
            var mapSo = new SerializedObject(mapView);
            mapSo.FindProperty("theme").objectReferenceValue = theme;
            mapSo.ApplyModifiedPropertiesWithoutUndo();

            var entityView = rig.AddComponent<EntityViewRegistry>();
            var entitySo = new SerializedObject(entityView);
            entitySo.FindProperty("theme").objectReferenceValue = theme;
            entitySo.ApplyModifiedPropertiesWithoutUndo();

            rig.AddComponent<global::Overworld.OverworldPlayerController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("[Overworld] Scene built. Start the server, create an 'overworld' instance, " +
                      "paste its world id into the Aetherium rig, then press Play.");
        }

        private static GameObject TerrainPrefab(string name, Color color, Kind kind)
        {
            switch (kind)
            {
                case Kind.Ground:
                    return SlabPrefab(name, color, height: 0.08f, transparent: false);
                case Kind.Bump:
                    return SlabPrefab(name, color, height: 0.22f, transparent: false);
                case Kind.Liquid:
                    return SlabPrefab(name, WithAlpha(color, 0.6f), height: 0.06f, transparent: true);
                case Kind.Glass:
                    return BlockPrefab(name, WithAlpha(color, 0.32f), height: 1.0f, transparent: true);
                case Kind.Block:
                default:
                    return BlockPrefab(name, color, height: name == "Mountain" ? 1.5f : 1.0f, transparent: false);
            }
        }

        // A thin floor slab whose TOP sits at the cell's y=0 (the walkable plane). Full 1.0 cell
        // footprint so ground tiles meet edge-to-edge with no seam — the earlier 0.99 left a 0.01
        // gap that read as a dark grid over the field. (Full-height BLOCKS stay slightly inset, see
        // BlockPrefab: two adjacent walls at a shared 1.0 face would z-fight.)
        private static GameObject SlabPrefab(string name, Color color, float height, bool transparent) =>
            SavePrefab(name, PrimitiveType.Cube, color, new Vector3(1f, height, 1f), -height / 2f, transparent);

        // A full-height occluder whose BASE sits at y=0.
        private static GameObject BlockPrefab(string name, Color color, float height, bool transparent) =>
            SavePrefab(name, PrimitiveType.Cube, color, new Vector3(0.98f, height, 0.98f), height / 2f, transparent);

        private static GameObject CapsulePrefab(string name, Color color) =>
            SavePrefab(name, PrimitiveType.Capsule, color, new Vector3(0.45f, 0.45f, 0.45f), 0.45f, false);

        private static GameObject SpherePrefab(string name, Color color) =>
            SavePrefab(name, PrimitiveType.Sphere, color, new Vector3(0.35f, 0.35f, 0.35f), 0.25f, false);

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private static void SetBindings(SerializedProperty list, (string id, GameObject prefab)[] bindings)
        {
            list.arraySize = bindings.Length;
            for (var i = 0; i < bindings.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = bindings[i].id;
                element.FindPropertyRelative("prefab").objectReferenceValue = bindings[i].prefab;
            }
        }

        private static Material MakeMaterial(string name, Color color, bool transparent)
        {
            var path = $"{MaterialsFolder}/OW_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            if (transparent)
            {
                // URP Lit → Transparent surface (same recipe the runtime ghost material uses).
                material.SetFloat("_Surface", 1f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject SavePrefab(
            string name, PrimitiveType primitive, Color color, Vector3 scale, float childY, bool transparent)
        {
            var root = new GameObject(name);
            var shape = GameObject.CreatePrimitive(primitive);
            shape.name = "shape";
            shape.transform.SetParent(root.transform, false);
            shape.transform.localPosition = new Vector3(0f, childY, 0f);
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = MakeMaterial(name, color, transparent);
            Object.DestroyImmediate(shape.GetComponent<Collider>()); // views are visual only

            var path = $"{PrefabsFolder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
