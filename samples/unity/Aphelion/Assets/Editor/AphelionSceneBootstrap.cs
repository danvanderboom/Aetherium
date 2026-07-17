using Aetherium.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
            EnsureUrpActive();
            EnsureModelsBakedForUrp();

            // --- Terrain + item stand-ins (primitive geometry, URP Lit materials). ---
            var wall = CubePrefab("Wall", new Color(0.33f, 0.38f, 0.44f), height: 1f);
            var door = CubePrefab("Door", new Color(0.75f, 0.55f, 0.25f), height: 0.9f);
            var floor = CubePrefab("Floor", new Color(0.16f, 0.18f, 0.21f), height: 0.08f, sinkTopToGround: true);
            var item = SpherePrefab("Item", new Color(0.95f, 0.8f, 0.3f));

            // --- Player + creatures: each bundle content id gets its own Quaternius model
            //     (normalized to a consistent cell footprint); a distinct-colored capsule
            //     stands in when a model has not imported. This is what makes each NPC read
            //     as a different creature rather than one shared silhouette. ---
            var player = ActorPrefab("Player", "reclaimer-astronaut.glb", new Color(0.25f, 0.85f, 0.95f));
            var creatureDefault = CapsulePrefab("Creature", new Color(0.95f, 0.45f, 0.2f));
            var scrapMite = ActorPrefab("scrap-mite", "scrapmite-robot-flying.glb", new Color(0.85f, 0.8f, 0.2f), footprint: 0.55f);
            var custodian = ActorPrefab("custodian", "custodian-animated-robot.glb", new Color(0.3f, 0.55f, 0.95f));
            var sentinel = ActorPrefab("sentinel", "sentinel-robot-large-gun.glb", new Color(0.9f, 0.25f, 0.25f), footprint: 0.9f);
            var ventLurker = ActorPrefab("vent-lurker", "ventlurker-alien.glb", new Color(0.4f, 0.85f, 0.35f));
            var overseer = ActorPrefab("overseer-node", "overseer-mech.glb", new Color(0.7f, 0.35f, 0.9f), footprint: 1.1f);

            // --- Theme asset: bundle content ids → stand-ins. ---
            var theme = AssetDatabase.LoadAssetAtPath<ThemeAsset>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<ThemeAsset>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            var so = new SerializedObject(theme);
            SetBindings(so.FindProperty("terrain"), ("Wall", wall), ("Door", door));
            SetBindings(so.FindProperty("creatures"),
                ("scrap-mite", scrapMite),
                ("custodian", custodian),
                ("sentinel", sentinel),
                ("vent-lurker", ventLurker),
                ("overseer-node", overseer));
            so.FindProperty("defaultTerrainPrefab").objectReferenceValue = floor;
            so.FindProperty("defaultCreaturePrefab").objectReferenceValue = creatureDefault;
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

        /// <summary>
        /// Runs after every domain reload so the URP repair can't be missed by menu-click
        /// timing: if the project has no pipeline asset, it gets one as soon as scripts
        /// finish compiling. Idempotent and a no-op once URP is assigned.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void EnsureUrpOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                EnsureFolder(RootFolder);
                EnsureUrpActive();
            };
        }

        /// <summary>
        /// A regenerated project ships with no render pipeline asset assigned, which means
        /// Built-in RP — and every URP-shader material renders the classic error magenta.
        /// Create + assign a URP asset so the project actually runs the pipeline it imports.
        /// </summary>
        private static void EnsureUrpActive()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)
                return;

            var rendererPath = $"{RootFolder}/UniversalRenderer.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, rendererPath);
            }

            var pipelinePath = $"{RootFolder}/UniversalRP.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            AssetDatabase.SaveAssets();
            Debug.Log("[Aphelion] Assigned URP pipeline asset (project was running Built-in RP).");
        }

        /// <summary>
        /// glTFast bakes model materials against the render pipeline that was active when the
        /// GLB imported. The models first imported under Built-in RP (before the URP asset
        /// existed), so their materials are magenta until reimported under URP. Do that once,
        /// tracked by an EditorPrefs flag so later menu runs stay fast.
        /// </summary>
        private static void EnsureModelsBakedForUrp()
        {
            const string flag = "Aphelion.ModelsBakedForUrp";
            if (EditorPrefs.GetBool(flag, false) || !AssetDatabase.IsValidFolder(ModelsFolder))
                return;

            AssetDatabase.ImportAsset(ModelsFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            EditorPrefs.SetBool(flag, true);
            Debug.Log("[Aphelion] Reimported Quaternius models under URP (one-time material bake).");
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

        private const string ModelsFolder = "Assets/ThirdParty/Quaternius";

        /// <summary>
        /// A player/creature prefab: the named Quaternius model normalized to sit on the cell
        /// floor at a consistent footprint, or — when the model has not imported (glTFast
        /// pending, file absent) — a distinct-colored capsule so the actor still reads as its
        /// own creature. The color also documents the intended silhouette per id.
        /// </summary>
        private static GameObject ActorPrefab(string name, string glbFile, Color fallbackColor, float footprint = 0.75f)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsFolder}/{glbFile}");
            if (model == null)
                return CapsulePrefab(name, fallbackColor);
            return NormalizedModelPrefab(name, model, footprint, fallbackColor);
        }

        /// <summary>
        /// Wraps an imported model in a prefab whose pivot is the cell floor-center and whose
        /// horizontal size is <paramref name="footprint"/> cells, so every creature — whatever
        /// the source model's authored scale/origin — drops into the grid consistently.
        /// </summary>
        private static GameObject NormalizedModelPrefab(string name, GameObject model, float footprint, Color fallbackColor)
        {
            var root = new GameObject(name);
            var instance = Object.Instantiate(model);
            instance.transform.SetParent(root.transform, worldPositionStays: false);

            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(root);
                return CapsulePrefab(name, fallbackColor); // model imported but has no mesh yet
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            // Re-origin: horizontal center to (0,0), base to y=0 (root is at world origin,
            // identity scale, so these world bounds equal the offsets we apply locally).
            instance.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

            var widest = Mathf.Max(bounds.size.x, bounds.size.z);
            root.transform.localScale = Vector3.one * (widest > 1e-4f ? footprint / widest : 1f);

            var path = $"{PrefabsFolder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

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
