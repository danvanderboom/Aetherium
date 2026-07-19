#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherium.Unity.Rounded
{
    /// <summary>
    /// Renders contiguous "region" terrain (water now; forest/mountain later) as a single
    /// smooth curved mesh per (terrain, altitude level) instead of one blocky prefab per cell.
    /// Marching-squares → Chaikin → a per-vertex signed-distance field that the region shader
    /// thresholds into a rounded, foam-ringed edge (see <see cref="RegionMeshBuilder"/>).
    ///
    /// <para>Deliberately a plain class, not a MonoBehaviour: <c>GridMapView</c> drives it from
    /// the perception memory, but it can equally be driven by an offline preview with a canned
    /// cell set — no server, no store. It only needs a parent <see cref="Transform"/> to hang
    /// the meshes under, a material resolver, and the cell list.</para>
    ///
    /// <para>Meshes are laid on the XZ ground plane (built that way by the mesh builder); each
    /// group sits at <c>y = level·cellSize + yBias</c>, the small bias lifting water just clear
    /// of the ground slabs so it never z-fights them. A group's mesh rebuilds only when its
    /// cell-set changes; groups that vanish from a sync are destroyed.</para>
    /// </summary>
    public sealed class RoundedRegionRenderer
    {
        private static readonly int BandAlphaId = Shader.PropertyToID("_BandAlpha");
        private static readonly int AmbientTintId = Shader.PropertyToID("_AmbientTint");
        private static readonly int BlendWidthId = Shader.PropertyToID("_BlendWidth");

        // Each priority step lifts the region this far in Y. Small enough to read as flat ground,
        // large enough that the transparent sort draws higher-priority terrains last (on top) so
        // their soft edges blend OVER the lower ones — see RoundedTerrain.shader.
        private const float PriorityLayerStep = 0.006f;

        private sealed class Group
        {
            public GameObject Go = null!;
            public MeshFilter Filter = null!;
            public MeshRenderer Renderer = null!;
            public Mesh Mesh = null!;
            public int CellHash;
            public bool Seen;
        }

        private readonly Dictionary<(string terrain, int level), Group> _groups =
            new Dictionary<(string, int), Group>();
        private MaterialPropertyBlock? _mpb;

        /// <summary>Number of live region-mesh groups (one per terrain+level currently shown).</summary>
        public int GroupCount => _groups.Count;

        /// <summary>The mesh for a (terrain, level) group, or null if none is live.</summary>
        public Mesh? GetMesh(string terrain, int level) =>
            _groups.TryGetValue((terrain, level), out var g) ? g.Mesh : null;

        /// <summary>
        /// Rebuilds the region meshes to exactly cover <paramref name="cells"/>. Cells are grouped
        /// by (terrain, z); each group gets one dirty-checked mesh. Groups absent from this call
        /// are destroyed. Safe to call every frame — unchanged groups do no work.
        /// </summary>
        public void Sync(
            Transform parent,
            IEnumerable<(string terrain, int x, int y, int z)> cells,
            Func<string, Material?> resolveMaterial,
            float cellSize = 1f,
            int smoothIterations = 2,
            int subdivisions = 3,
            Color ambientTint = default,
            float yBias = 0.02f,
            Func<string, int>? resolvePriority = null,
            float blendWidth = 0.6f)
        {
            if (ambientTint == default)
                ambientTint = Color.white;

            // Bucket cells by (terrain, level).
            var buckets = new Dictionary<(string, int), List<(int x, int y)>>();
            foreach (var (terrain, x, y, z) in cells)
            {
                if (string.IsNullOrEmpty(terrain))
                    continue;
                var key = (terrain, z);
                if (!buckets.TryGetValue(key, out var list))
                {
                    list = new List<(int, int)>();
                    buckets[key] = list;
                }
                list.Add((x, y));
            }

            foreach (var group in _groups.Values)
                group.Seen = false;

            foreach (var kv in buckets)
            {
                var (terrain, level) = kv.Key;
                var cellList = kv.Value;

                Group group = GetOrCreate(parent, terrain, level, resolveMaterial);
                group.Seen = true;

                int hash = CellSetHash(cellList);
                if (hash != group.CellHash)
                {
                    var data = RegionMeshBuilder.Build(cellList, smoothIterations, subdivisions, cellSize);
                    RegionMeshBuilder.Fill(group.Mesh, data);
                    group.CellHash = hash;
                }

                // Aetherium places cell (x,y) so its CENTER is at world (x·cs, −y·cs) — but the
                // marching-squares mesh treats cell (x,y) as the corner-based square [x,x+1]×[y,y+1]
                // (center at (x+0.5, y+0.5)). Shift the whole group by (−0.5, +0.5) cells so the
                // rounded surface lands exactly over the square tiles instead of half a cell off.
                // The per-priority y-lift stacks terrains so higher ones blend over lower ones.
                int priority = resolvePriority?.Invoke(terrain) ?? 0;
                group.Go.transform.localPosition = new Vector3(
                    -0.5f * cellSize,
                    level * cellSize + yBias + priority * PriorityLayerStep,
                    0.5f * cellSize);

                _mpb ??= new MaterialPropertyBlock();
                group.Renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(BandAlphaId, 1f);
                _mpb.SetColor(AmbientTintId, ambientTint);
                _mpb.SetFloat(BlendWidthId, blendWidth);
                group.Renderer.SetPropertyBlock(_mpb);
            }

            // Sweep groups that no longer have cells.
            if (_groups.Count != buckets.Count)
            {
                var stale = new List<(string, int)>();
                foreach (var kv in _groups)
                    if (!kv.Value.Seen)
                        stale.Add(kv.Key);
                foreach (var key in stale)
                {
                    SafeDestroy(_groups[key].Go);
                    _groups.Remove(key);
                }
            }
        }

        /// <summary>Destroys every region mesh (used on a hard re-anchor).</summary>
        public void Clear()
        {
            foreach (var group in _groups.Values)
                SafeDestroy(group.Go);
            _groups.Clear();
        }

        private Group GetOrCreate(Transform parent, string terrain, int level, Func<string, Material?> resolveMaterial)
        {
            if (_groups.TryGetValue((terrain, level), out var existing))
                return existing;

            var go = new GameObject($"region:{terrain}@{level}");
            go.transform.SetParent(parent, false);

            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = $"region:{terrain}@{level}" };
            filter.sharedMesh = mesh;

            var material = resolveMaterial(terrain);
            if (material == null)
                material = FallbackMaterial();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var group = new Group
            {
                Go = go,
                Filter = filter,
                Renderer = renderer,
                Mesh = mesh,
                CellHash = -1,
            };
            _groups[(terrain, level)] = group;
            return group;
        }

        private static Material? _fallback;

        /// <summary>Last-resort material if the theme resolves none: the rounded-water shader, then URP Unlit.</summary>
        public static Material? FallbackMaterial()
        {
            if (_fallback != null)
                return _fallback;

            Shader? shader = Shader.Find("Aetherium/RoundedWater")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
            if (shader == null)
                return null;

            _fallback = new Material(shader) { name = "RoundedRegionFallback" };
            return _fallback;
        }

        // Order-independent hash of a group's cell-set (drives the dirty check).
        private static int CellSetHash(List<(int x, int y)> cells)
        {
            int hash = 17 + cells.Count * 92821;
            foreach (var (x, y) in cells)
                hash ^= (x * 73856093) ^ (y * 19349663);
            return hash;
        }

        private static void SafeDestroy(GameObject go)
        {
            if (go == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
