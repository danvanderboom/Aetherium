namespace Aetherium.Model.ContentAtlas
{
    /// <summary>
    /// A stable, renderer-agnostic vocabulary entry (engine gap-analysis §4.10). Perception
    /// payloads reference tags by <see cref="Id"/> only; each renderer (console, Unity, Unreal)
    /// binds ids to its own asset pack instead of the server assuming glyphs or colors.
    /// </summary>
    public abstract class ContentAtlasTag
    {
        public string Id { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        protected ContentAtlasTag() { }

        protected ContentAtlasTag(string id, string description)
        {
            Id = id;
            Description = description;
        }
    }

    /// <summary>A terrain/tile kind (e.g. "wall", "water"). Rendering vocabulary only — passability
    /// and opacity remain owned by engine-core's tile components.</summary>
    public class TerrainTag : ContentAtlasTag
    {
        public TerrainTag() { }
        public TerrainTag(string id, string description) : base(id, description) { }
    }

    /// <summary>An entity kind (e.g. "monster.zombie", "item.sword").</summary>
    public class EntityKindTag : ContentAtlasTag
    {
        public EntityKindTag() { }
        public EntityKindTag(string id, string description) : base(id, description) { }
    }

    /// <summary>A surface material with typed physical properties a renderer or audio system can use.</summary>
    public class MaterialTag : ContentAtlasTag
    {
        public double Hardness { get; set; }
        public double Friction { get; set; }
        public double Combustibility { get; set; }

        public MaterialTag() { }

        public MaterialTag(string id, string description, double hardness, double friction, double combustibility)
            : base(id, description)
        {
            Hardness = hardness;
            Friction = friction;
            Combustibility = combustibility;
        }
    }

    /// <summary>A light source kind with typed color/intensity/flicker a renderer can bind to a shader or palette.</summary>
    public class LightSourceTag : ContentAtlasTag
    {
        public string ColorHex { get; set; } = "#FFFFFF";
        public double Intensity { get; set; }
        public bool Flicker { get; set; }

        public LightSourceTag() { }

        public LightSourceTag(string id, string description, string colorHex, double intensity, bool flicker)
            : base(id, description)
        {
            ColorHex = colorHex;
            Intensity = intensity;
            Flicker = flicker;
        }
    }

    /// <summary>An actor animation/intent cue (e.g. "idle", "attacking", "dying").</summary>
    public class AnimationCueTag : ContentAtlasTag
    {
        public AnimationCueTag() { }
        public AnimationCueTag(string id, string description) : base(id, description) { }
    }

    /// <summary>A visual/gameplay effect cue (e.g. "melee_arc", "projectile").</summary>
    public class EffectTag : ContentAtlasTag
    {
        public EffectTag() { }
        public EffectTag(string id, string description) : base(id, description) { }
    }

    /// <summary>An audio cue (e.g. ambient bed, one-shot sfx).</summary>
    public class AudioTag : ContentAtlasTag
    {
        public AudioTag() { }
        public AudioTag(string id, string description) : base(id, description) { }
    }
}
