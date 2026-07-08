using Aetherium.Model.ContentAtlas;

namespace Aetherium.Server.ContentAtlas
{
    /// <summary>
    /// The engine's initial (v1.0.0) content atlas, seeded from the tile types and entity kinds
    /// that actually exist today (<see cref="Aetherium.WorldBuilders.TorusWorldBuilder"/> and the
    /// known entity classes) rather than placeholder data. Phase 1 of the content-atlas change
    /// (see openspec/changes/add-content-atlas) — nothing in the live perception pipeline
    /// references these ids yet.
    /// </summary>
    public static class DefaultContentAtlas
    {
        public static Model.ContentAtlas.ContentAtlas Build()
        {
            var atlas = new Model.ContentAtlas.ContentAtlas("1.0.0");

            // Terrain — mirrors TorusWorldBuilder's TerrainTypeNames exactly (kept in sync by
            // the coverage test in Aetherium.Test/ContentAtlas).
            atlas.AddTerrainTag(new TerrainTag("none", "Unmapped/void tile"));
            atlas.AddTerrainTag(new TerrainTag("indoors", "Interior floor"));
            atlas.AddTerrainTag(new TerrainTag("wall", "Solid wall, blocks movement and view"));
            atlas.AddTerrainTag(new TerrainTag("mountain", "Impassable high terrain, blocks view"));
            atlas.AddTerrainTag(new TerrainTag("road", "Traveled path"));
            atlas.AddTerrainTag(new TerrainTag("plains", "Open grassland"));
            atlas.AddTerrainTag(new TerrainTag("forest", "Wooded terrain"));
            atlas.AddTerrainTag(new TerrainTag("water", "Water body"));
            atlas.AddTerrainTag(new TerrainTag("cave", "Underground cavern floor"));
            atlas.AddTerrainTag(new TerrainTag("upstairs", "Stairway up a level"));
            atlas.AddTerrainTag(new TerrainTag("downstairs", "Stairway down a level"));

            // Entity kinds — the known concrete entity classes as of this change.
            atlas.AddEntityKindTag(new EntityKindTag("character", "Player-controlled character"));
            atlas.AddEntityKindTag(new EntityKindTag("monster", "Generic hostile monster"));
            atlas.AddEntityKindTag(new EntityKindTag("dead_monster", "Defeated monster marker"));
            atlas.AddEntityKindTag(new EntityKindTag("zombie", "Undead melee monster"));
            atlas.AddEntityKindTag(new EntityKindTag("snake", "Wandering hazard monster"));
            atlas.AddEntityKindTag(new EntityKindTag("sword_item", "Melee weapon item"));
            atlas.AddEntityKindTag(new EntityKindTag("key_item", "Key item"));
            atlas.AddEntityKindTag(new EntityKindTag("food_item", "Consumable food item"));

            // Animation/intent cues (engine gap-analysis §4.10 "intent" vocabulary).
            atlas.AddAnimationCueTag(new AnimationCueTag("idle", "Actor is idle"));
            atlas.AddAnimationCueTag(new AnimationCueTag("moving", "Actor is moving"));
            atlas.AddAnimationCueTag(new AnimationCueTag("attacking", "Actor is performing an attack"));
            atlas.AddAnimationCueTag(new AnimationCueTag("hit", "Actor was just hit"));
            atlas.AddAnimationCueTag(new AnimationCueTag("dying", "Actor is transitioning to defeated"));

            // A first effect/light seed so downstream combat/ability work (§4.2/§4.3) has
            // somewhere to register visual tags rather than inventing raw strings again.
            atlas.AddEffectTag(new EffectTag("melee_arc", "Close-range melee swing"));
            atlas.AddLightSourceTag(new LightSourceTag("torch", "Handheld flame light", "#FF8C1A", intensity: 0.7, flicker: true));

            return atlas;
        }
    }
}
