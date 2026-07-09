using System.Collections.Generic;

namespace Aetherium.Model.ContentAtlas
{
    public enum ContentAtlasCategory
    {
        Terrain,
        EntityKind,
        Material,
        LightSource,
        AnimationCue,
        Effect,
        Audio,
    }

    /// <summary>
    /// The render-agnostic content vocabulary (engine gap-analysis §4.10): a versioned set of
    /// typed tags every renderer binds ids from instead of the server assuming glyphs/colors.
    /// </summary>
    public class ContentAtlas
    {
        public string Version { get; }

        public IReadOnlyDictionary<string, TerrainTag> TerrainTags => _terrain;
        public IReadOnlyDictionary<string, EntityKindTag> EntityKindTags => _entityKinds;
        public IReadOnlyDictionary<string, MaterialTag> MaterialTags => _materials;
        public IReadOnlyDictionary<string, LightSourceTag> LightSourceTags => _lightSources;
        public IReadOnlyDictionary<string, AnimationCueTag> AnimationCueTags => _animationCues;
        public IReadOnlyDictionary<string, EffectTag> EffectTags => _effects;
        public IReadOnlyDictionary<string, AudioTag> AudioTags => _audio;

        private readonly Dictionary<string, TerrainTag> _terrain = new();
        private readonly Dictionary<string, EntityKindTag> _entityKinds = new();
        private readonly Dictionary<string, MaterialTag> _materials = new();
        private readonly Dictionary<string, LightSourceTag> _lightSources = new();
        private readonly Dictionary<string, AnimationCueTag> _animationCues = new();
        private readonly Dictionary<string, EffectTag> _effects = new();
        private readonly Dictionary<string, AudioTag> _audio = new();

        public ContentAtlas(string version)
        {
            Version = version;
        }

        public bool AddTerrainTag(TerrainTag tag) => TryAdd(_terrain, tag);
        public bool AddEntityKindTag(EntityKindTag tag) => TryAdd(_entityKinds, tag);
        public bool AddMaterialTag(MaterialTag tag) => TryAdd(_materials, tag);
        public bool AddLightSourceTag(LightSourceTag tag) => TryAdd(_lightSources, tag);
        public bool AddAnimationCueTag(AnimationCueTag tag) => TryAdd(_animationCues, tag);
        public bool AddEffectTag(EffectTag tag) => TryAdd(_effects, tag);
        public bool AddAudioTag(AudioTag tag) => TryAdd(_audio, tag);

        private static bool TryAdd<T>(Dictionary<string, T> dict, T tag) where T : ContentAtlasTag
            => dict.TryAdd(tag.Id, tag);

        /// <summary>Checks whether <paramref name="id"/> is registered in <paramref name="category"/>.</summary>
        public bool Contains(ContentAtlasCategory category, string id) => category switch
        {
            ContentAtlasCategory.Terrain => _terrain.ContainsKey(id),
            ContentAtlasCategory.EntityKind => _entityKinds.ContainsKey(id),
            ContentAtlasCategory.Material => _materials.ContainsKey(id),
            ContentAtlasCategory.LightSource => _lightSources.ContainsKey(id),
            ContentAtlasCategory.AnimationCue => _animationCues.ContainsKey(id),
            ContentAtlasCategory.Effect => _effects.ContainsKey(id),
            ContentAtlasCategory.Audio => _audio.ContainsKey(id),
            _ => false,
        };

        /// <summary>
        /// True when <paramref name="clientVersion"/> shares this atlas's major version — additive
        /// (minor/patch) atlas changes stay compatible with a client that declared an older
        /// minor/patch of the same major; a major bump signals a renamed/removed tag.
        /// </summary>
        public bool SupportsClientVersion(string clientVersion)
            => SemVer.Parse(clientVersion).Major == SemVer.Parse(Version).Major;
    }
}
