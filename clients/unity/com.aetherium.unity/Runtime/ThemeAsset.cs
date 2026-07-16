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
