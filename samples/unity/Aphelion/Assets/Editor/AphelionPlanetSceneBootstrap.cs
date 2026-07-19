using System.Collections.Generic;
using Aetherium.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Aphelion.EditorTools
{
    /// <summary>
    /// One-click builder for the H3 planet scene. Menu: Aetherium → Build Aphelion Planet (H3) Scene.
    /// The parallel of <see cref="OverworldSceneBootstrap"/> but for the spherical Aphelion Prime
    /// bundle (<c>aphelion-h3</c>): a biome ThemeAsset covering the planet's full terrain vocabulary
    /// — water, plains, forest, desert, hills, mountains, plus the transport layers roads/rail/subway
    /// and the settlement tiles (walls, indoor floors, window walls) — and a scene wired to resolve
    /// and join a running aphelion-h3 world from the lobby.
    ///
    /// <para>Terrain renders as smooth, blended <see cref="RoundedRegionRenderer"/> region meshes
    /// (water/plains/forest/desert/hills/road/rail) so the sphere reads as feathered biomes rather
    /// than blocky slabs; mountains and settlement walls stay as 3D prefab blocks. The H3 topology
    /// is laid out as hex cells by GridCellLayout — this bootstrap only supplies the look.</para>
    ///
    /// <para>Reuses AphelionSceneBootstrap's InitializeOnLoad URP repair — no URP setup here.</para>
    /// </summary>
    public static class AphelionPlanetSceneBootstrap
    {
        private const string RootFolder = "Assets/Aetherium";
        private const string MaterialsFolder = "Assets/Aetherium/Materials";
        private const string PrefabsFolder = "Assets/Aetherium/Prefabs/Planet";
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/AphelionPlanet.unity";
        private const string ThemePath = "Assets/Aetherium/AphelionPlanetTheme.asset";

        // Terrain name → (color, kind). Ground = thin walkable slab; Bump = a low rise; Block = a
        // full-height occluder; Glass = a translucent pane; Liquid = a translucent floor slab. The
        // names match the aphelion-h3 tile-type vocabulary exactly (server perception TileTypes).
        private enum Kind { Ground, Bump, Block, Glass, Liquid }

        private static readonly (string name, Color color, Kind kind)[] Terrain =
        {
            ("Water",      new Color(0.13f, 0.34f, 0.66f), Kind.Liquid),
            ("Plains",     new Color(0.42f, 0.58f, 0.30f), Kind.Ground),
            ("Forest",     new Color(0.15f, 0.37f, 0.19f), Kind.Ground),
            ("Desert",     new Color(0.83f, 0.75f, 0.50f), Kind.Ground),
            ("Hills",      new Color(0.52f, 0.50f, 0.28f), Kind.Bump),
            ("Mountain",   new Color(0.46f, 0.46f, 0.50f), Kind.Block),
            ("Road",       new Color(0.29f, 0.27f, 0.25f), Kind.Ground),
            ("Rail",       new Color(0.42f, 0.42f, 0.48f), Kind.Ground),   // metallic sleepers
            ("Subway",     new Color(0.16f, 0.22f, 0.30f), Kind.Ground),   // dark tunnel deck, cyan-lit
            ("Indoors",    new Color(0.52f, 0.43f, 0.32f), Kind.Ground),   // settlement floors
            ("Wall",       new Color(0.55f, 0.55f, 0.60f), Kind.Block),
            ("WindowWall", new Color(0.60f, 0.82f, 0.92f), Kind.Glass),
        };

        // Draw order for the smooth region meshes (higher = on top; its soft edge feathers OVER
        // lower neighbours). Base grounds low, transport above them, water highest so rivers and
        // coastline read over everything. Mountains/walls/windows/subway keep the prefab path.
        private static readonly (string name, int priority)[] Regions =
        {
            ("Desert", 0),
            ("Plains", 1),
            ("Hills",  2),
            ("Forest", 3),
            ("Indoors", 4),
            ("Road",   5),
            ("Rail",   6),
            ("Water",  7),
        };

        [MenuItem("Aetherium/Build Aphelion Planet (H3) Scene")]
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

            var player = CapsulePrefab("PlanetPlayer", new Color(0.25f, 0.85f, 0.95f));
            var item = SpherePrefab("PlanetItem", new Color(0.95f, 0.85f, 0.25f));
            var creatureDefault = CapsulePrefab("PlanetCreature", new Color(0.9f, 0.45f, 0.25f)); // the scattered wildlife

            var so = new SerializedObject(theme);
            SetBindings(so.FindProperty("terrain"), bindings);
            so.FindProperty("defaultTerrainPrefab").objectReferenceValue = plains; // unknown terrain → ground, never magenta
            so.FindProperty("defaultCreaturePrefab").objectReferenceValue = creatureDefault;
            so.FindProperty("defaultItemPrefab").objectReferenceValue = item;
            so.FindProperty("playerPrefab").objectReferenceValue = player;

            // Smooth blended region terrain (see class doc). Water uses the animated RoundedWater
            // shader; every other region uses RoundedTerrain tinted to its biome color.
            var regionProp = so.FindProperty("regionTerrains");
            regionProp.arraySize = Regions.Length;
            for (var i = 0; i < Regions.Length; i++)
            {
                var name = Regions[i].name;
                var priority = Regions[i].priority;
                var material = name == "Water"
                    ? RoundedWaterMaterial()
                    : RoundedTerrainMaterial(name, ColorOf(name));

                // Deterministic draw order: transparent region meshes over the same ground can't be
                // depth-sorted reliably, so stamp the render queue by priority (higher draws later).
                if (material != null)
                {
                    material.renderQueue = (int)RenderQueue.Transparent + priority;
                    EditorUtility.SetDirty(material);
                }

                var element = regionProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = name;
                element.FindPropertyRelative("material").objectReferenceValue = material;
                element.FindPropertyRelative("priority").intValue = priority;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);

            // --- Scene. ---
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.97f, 0.9f);
            lightObject.transform.rotation = Quaternion.Euler(52f, -30f, 0f);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.53f, 0.71f, 0.92f); // planetary daytime sky
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<AphelionCameraRig>();
            // Higher, farther than the station rig: the planet's 360° vision reveals a wide horizon.
            cameraObject.transform.position = new Vector3(0f, 18f, -13f);
            cameraObject.transform.LookAt(Vector3.zero);

            var rig = new GameObject("Aetherium");
            var behaviour = rig.AddComponent<AetheriumClientBehaviour>();
            var behaviourSo = new SerializedObject(behaviour);
            behaviourSo.FindProperty("serverUrl").stringValue = "http://localhost:5000";
            // Resolve a live H3 planet from the lobby on connect (no world GUID to paste).
            // Create one first:  dotnet run --project Aetherctl -- game create aphelion-h3
            behaviourSo.FindProperty("joinGameDefinitionId").stringValue = "aphelion-h3";
            behaviourSo.ApplyModifiedPropertiesWithoutUndo();

            var mapView = rig.AddComponent<GridMapView>();
            var mapSo = new SerializedObject(mapView);
            mapSo.FindProperty("theme").objectReferenceValue = theme;
            mapSo.ApplyModifiedPropertiesWithoutUndo();

            var entityView = rig.AddComponent<EntityViewRegistry>();
            var entitySo = new SerializedObject(entityView);
            entitySo.FindProperty("theme").objectReferenceValue = theme;
            entitySo.ApplyModifiedPropertiesWithoutUndo();

            // Open-world controls (WASD + E interact) — the planet is a calm exploration/economy
            // sandbox, so the Overworld controller fits better than the station's attack-first one.
            rig.AddComponent<global::Overworld.OverworldPlayerController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("[AphelionPlanet] H3 planet scene built. Start the server, create an " +
                      "'aphelion-h3' instance (aetherctl game create aphelion-h3), then press Play.");
        }

        // Terrain renders as HEXAGONAL PRISMS (not cubes) so the H3 planet actually reads as hexes,
        // and — because a pointy-top hex of circumradius 1/√3 tessellates the axial layout exactly —
        // adjacent tiles meet edge-to-edge with no overlap, which is what kills the cube-overlap
        // z-fighting. Grounds sit their top at the walkable plane (y=0); blocks rise from it.
        private static GameObject TerrainPrefab(string name, Color color, Kind kind)
        {
            switch (kind)
            {
                case Kind.Ground:
                    return HexTerrainPrefab(name, color, topY: 0f, bottomY: -0.10f, transparent: false);
                case Kind.Bump:
                    return HexTerrainPrefab(name, color, topY: 0.16f, bottomY: -0.10f, transparent: false);
                case Kind.Liquid:
                    return HexTerrainPrefab(name, WithAlpha(color, 0.6f), topY: 0f, bottomY: -0.06f, transparent: true);
                case Kind.Glass:
                    return HexTerrainPrefab(name, WithAlpha(color, 0.32f), topY: 1.0f, bottomY: 0f, transparent: true);
                case Kind.Block:
                default:
                    return HexTerrainPrefab(name, color, topY: name == "Mountain" ? 1.6f : 1.0f, bottomY: 0f, transparent: false);
            }
        }

        // 1/√3: a pointy-top hex with this circumradius has flat-to-flat width 1.0, so it tessellates
        // the axial layout GridCellLayout uses for "h3" (centres one cell apart) with no gaps/overlap.
        private const float HexCircumradius = 0.57735026f;

        private static GameObject HexTerrainPrefab(string name, Color color, float topY, float bottomY, bool transparent)
        {
            var root = new GameObject(name);
            root.AddComponent<MeshFilter>().sharedMesh = HexMesh(name, topY, bottomY);
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MakeMaterial(name, color, transparent);
            renderer.shadowCastingMode = ShadowCastingMode.Off; // ground never self-shadows (see GridMapView)

            var path = $"{PrefabsFolder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // A pointy-top hex prism centred on the cell, top at topY, bottom at bottomY. Corners at
        // 30°+60°·k in the layout (x,y) plane, mapped to Unity (x, -y) to match GridMapView.CellToWorld
        // (which places cells at z = -layoutY). Saved as a shared mesh asset so the prefab persists.
        private static Mesh HexMesh(string name, float topY, float bottomY)
        {
            var path = $"{PrefabsFolder}/Hex_{name}.mesh";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.name = $"Hex_{name}";
            mesh.Clear();

            var top = new Vector3[6];
            var bot = new Vector3[6];
            for (var k = 0; k < 6; k++)
            {
                var a = Mathf.Deg2Rad * (30f + 60f * k);
                var cx = HexCircumradius * Mathf.Cos(a);
                var cy = HexCircumradius * Mathf.Sin(a);
                top[k] = new Vector3(cx, topY, -cy);
                bot[k] = new Vector3(cx, bottomY, -cy);
            }

            var verts = new List<Vector3>();
            var tris = new List<int>();

            var topCenter = verts.Count; verts.Add(new Vector3(0f, topY, 0f));
            var topStart = verts.Count; verts.AddRange(top);
            for (var k = 0; k < 6; k++) { tris.Add(topCenter); tris.Add(topStart + (k + 1) % 6); tris.Add(topStart + k); }

            var botCenter = verts.Count; verts.Add(new Vector3(0f, bottomY, 0f));
            var botStart = verts.Count; verts.AddRange(bot);
            for (var k = 0; k < 6; k++) { tris.Add(botCenter); tris.Add(botStart + k); tris.Add(botStart + (k + 1) % 6); }

            for (var k = 0; k < 6; k++)
            {
                var n = (k + 1) % 6;
                var i = verts.Count;
                verts.Add(top[k]); verts.Add(top[n]); verts.Add(bot[n]); verts.Add(bot[k]);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

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
            var path = $"{MaterialsFolder}/PL_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            // Double-sided: the procedural hex mesh is viewed from above and the sides at a low angle;
            // rendering both faces means a triangle-winding slip can't leave a tile invisible.
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.doubleSidedGI = true;
            if (transparent)
            {
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

        private static Color ColorOf(string name)
        {
            foreach (var t in Terrain)
                if (t.name == name)
                    return t.color;
            return Color.gray;
        }

        // Smooth region ground: the shared package's Aetherium/RoundedTerrain shader (unlit
        // transparent — opaque interior, soft outward-feathered edge), tinted to the biome color.
        private static Material RoundedTerrainMaterial(string name, Color color)
        {
            var path = $"{MaterialsFolder}/PL_Rounded{name}.mat";
            var shader = Shader.Find("Aetherium/RoundedTerrain");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var initial = shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit");
                material = new Material(initial);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }
            if (shader != null)
                material.SetColor("_Color", color);
            else
                Debug.LogWarning("[AphelionPlanet] Aetherium/RoundedTerrain shader not found yet — terrain uses a " +
                                 "plain fallback. Re-run 'Build Aphelion Planet (H3) Scene' once the package finishes importing.");
            EditorUtility.SetDirty(material);
            return material;
        }

        // Smooth region water: the shared package's Aetherium/RoundedWater shader (animated foam,
        // shallows→deep, curved coastline). Falls back to URP Unlit until the shader imports.
        private static Material RoundedWaterMaterial()
        {
            var path = $"{MaterialsFolder}/PL_RoundedWater.mat";
            var shader = Shader.Find("Aetherium/RoundedWater");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var initial = shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit");
                material = new Material(initial);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }
            if (shader == null)
                Debug.LogWarning("[AphelionPlanet] Aetherium/RoundedWater shader not found yet — water uses a " +
                                 "plain fallback. Re-run 'Build Aphelion Planet (H3) Scene' once the package finishes importing.");
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
