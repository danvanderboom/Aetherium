using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherium.Unity
{
    /// <summary>
    /// The presentation contract (docs/design/unity-sample/unity-client-library.md): a
    /// ScriptableObject mapping the game bundle's stable content ids — creature ids from
    /// <c>Creature:&lt;id&gt;</c> tiles, terrain tile-type names, item ids — to prefabs. This is
    /// what keeps the library game-agnostic: Aphelion and any third-party game differ only
    /// in their ThemeAsset (and their bundle).
    ///
    /// Every lookup has a fallback chain: exact id → category default → loud placeholder
    /// (bright magenta, never invisible). The legacy client's "everything renders invisible"
    /// failure mode is designed out.
    /// </summary>
    [CreateAssetMenu(fileName = "AetheriumTheme", menuName = "Aetherium/Theme Asset")]
    public sealed class ThemeAsset : ScriptableObject
    {
        [Serializable]
        public struct Binding
        {
            [Tooltip("The bundle content id (e.g. 'custodian', 'medgel') or terrain tile-type name (e.g. 'Wall').")]
            public string id;
            public GameObject prefab;
        }

        [Header("Creatures (content id → prefab)")]
        [SerializeField] private List<Binding> creatures = new List<Binding>();
        [SerializeField] private GameObject defaultCreaturePrefab;

        [Header("Terrain (tile-type name → prefab)")]
        [SerializeField] private List<Binding> terrain = new List<Binding>();
        [SerializeField] private GameObject defaultTerrainPrefab;

        [Serializable]
        public struct RegionBinding
        {
            [Tooltip("Terrain tile-type name that renders as one smooth curved region mesh " +
                     "(e.g. 'Water') instead of a blocky prefab per cell.")]
            public string id;

            [Tooltip("Material for the region surface — typically the Aetherium/RoundedWater or " +
                     "Aetherium/RoundedTerrain shader. Leave empty to fall back to a runtime material.")]
            public Material material;

            [Tooltip("Draw order among region terrains: higher priority renders on top, so its soft " +
                     "edge blends OVER lower-priority neighbours (e.g. water/forest over base ground).")]
            public int priority;
        }

        [Header("Rounded region terrain (tile-type name → surface material)")]
        [Tooltip("Terrains listed here are drawn by RoundedRegionRenderer as a single smooth, " +
                 "foam-ringed mesh (marching-squares → Chaikin → SDF) rather than as one prefab " +
                 "per cell. GridMapView skips prefab-spawning for these. Empty = classic behavior.")]
        [SerializeField] private List<RegionBinding> regionTerrains = new List<RegionBinding>();
        private Dictionary<string, Material> _regionLookup;
        private Dictionary<string, int> _regionPriority;
        private HashSet<string> _regionNames;

        [Header("Items (item id → prefab)")]
        [SerializeField] private List<Binding> items = new List<Binding>();
        [SerializeField] private GameObject defaultItemPrefab;

        [Header("Players")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Last resort")]
        [Tooltip("Used when even the category default is missing. Leave empty to get a runtime-generated bright magenta capsule — loud beats invisible.")]
        [SerializeField] private GameObject loudPlaceholderPrefab;

        private Dictionary<string, GameObject> _creatureLookup;
        private Dictionary<string, GameObject> _terrainLookup;
        private Dictionary<string, GameObject> _itemLookup;

        public GameObject ResolveCreature(string creatureTypeId) =>
            Resolve(ref _creatureLookup, creatures, creatureTypeId, defaultCreaturePrefab);

        public GameObject ResolveTerrain(string tileTypeName) =>
            Resolve(ref _terrainLookup, terrain, tileTypeName, defaultTerrainPrefab);

        public GameObject ResolveItem(string itemId) =>
            Resolve(ref _itemLookup, items, itemId, defaultItemPrefab);

        public GameObject ResolvePlayer() =>
            playerPrefab != null ? playerPrefab : LoudPlaceholder();

        /// <summary>True when this terrain should render as a smooth curved region mesh
        /// (via RoundedRegionRenderer) rather than one prefab per cell. Case-insensitive.</summary>
        public bool IsRegionTerrain(string tileTypeName)
        {
            EnsureRegionLookup();
            return !string.IsNullOrEmpty(tileTypeName) && _regionNames.Contains(tileTypeName);
        }

        /// <summary>The surface material for a region terrain, or null to use a runtime fallback.</summary>
        public Material ResolveRegionMaterial(string tileTypeName)
        {
            EnsureRegionLookup();
            return !string.IsNullOrEmpty(tileTypeName) && _regionLookup.TryGetValue(tileTypeName, out var m)
                ? m : null;
        }

        /// <summary>Draw priority for a region terrain (higher = on top, blends over lower ones);
        /// 0 for anything not registered as a region.</summary>
        public int ResolveRegionPriority(string tileTypeName)
        {
            EnsureRegionLookup();
            return !string.IsNullOrEmpty(tileTypeName) && _regionPriority.TryGetValue(tileTypeName, out var p)
                ? p : 0;
        }

        /// <summary>Registers a terrain as a rounded region at runtime (for code-built themes and
        /// tests). Editor themes usually set this in the inspector / a scene bootstrap instead.</summary>
        public void RegisterRegionTerrain(string id, Material material = null, int priority = 0)
        {
            if (string.IsNullOrEmpty(id))
                return;
            regionTerrains.Add(new RegionBinding { id = id, material = material, priority = priority });
            _regionNames = null; // force a rebuild of the lookup on next query
            _regionLookup = null;
            _regionPriority = null;
        }

        private void EnsureRegionLookup()
        {
            if (_regionNames != null)
                return;
            _regionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _regionLookup = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            _regionPriority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in regionTerrains)
            {
                if (string.IsNullOrEmpty(binding.id))
                    continue;
                _regionNames.Add(binding.id);
                _regionLookup[binding.id] = binding.material; // may be null → renderer falls back
                _regionPriority[binding.id] = binding.priority;
            }
        }

        private GameObject Resolve(
            ref Dictionary<string, GameObject> lookup,
            List<Binding> bindings,
            string id,
            GameObject categoryDefault)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
                foreach (var binding in bindings)
                    if (!string.IsNullOrEmpty(binding.id) && binding.prefab != null)
                        lookup[binding.id] = binding.prefab;
            }

            if (!string.IsNullOrEmpty(id) && lookup.TryGetValue(id, out var prefab))
                return prefab;
            if (categoryDefault != null)
                return categoryDefault;
            return LoudPlaceholder();
        }

        private GameObject _generatedPlaceholder;

        private GameObject LoudPlaceholder()
        {
            if (loudPlaceholderPrefab != null)
                return loudPlaceholderPrefab;
            if (_generatedPlaceholder == null)
            {
                // A bright magenta capsule no artist would ship: unmissable in any scene.
                _generatedPlaceholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _generatedPlaceholder.name = "AetheriumMissingBinding";
                var renderer = _generatedPlaceholder.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial.color = Color.magenta;
                _generatedPlaceholder.SetActive(false); // template only; views instantiate copies
            }
            return _generatedPlaceholder;
        }
    }
}
