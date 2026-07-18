using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Model.Content;
using Aetherium.Model.ContentAtlas;

namespace Aetherium.Server.Content
{
    /// <summary>
    /// The runtime (compiled) form of a world's <see cref="ContentConfig"/>
    /// (openspec/changes/add-content-definitions): definitions indexed by id, a weighted spawn
    /// draw, and the per-world atlas. Built once per map bind (initialize/reactivate) by
    /// <see cref="ContentCompiler.Compile"/> — the same config→compiled split abilities,
    /// progression, and factions use.
    /// </summary>
    public sealed class ContentCatalog
    {
        /// <summary>The behavior-tree presets a <see cref="CreatureDefinition.Behavior"/> may name.
        /// One today; ECA-scripted behaviors are the graduation path.</summary>
        public static readonly IReadOnlyCollection<string> BehaviorPresets = new[] { "wander-melee" };

        public IReadOnlyDictionary<string, CreatureDefinition> CreaturesById { get; }
        public IReadOnlyDictionary<string, ItemDefinition> ItemsById { get; }

        /// <summary>Per-world content vocabulary: engine defaults plus one entity-kind tag per
        /// defined creature/item. Nothing live consumes it yet (same phase-1 status as
        /// DefaultContentAtlas) — it exists so renderer binding has a per-game source of truth.</summary>
        public Aetherium.Model.ContentAtlas.ContentAtlas Atlas { get; }

        private readonly List<SpawnTableEntry> _spawns;
        private readonly int _totalSpawnWeight;

        public bool HasSpawnTable => _totalSpawnWeight > 0;

        internal ContentCatalog(
            Dictionary<string, CreatureDefinition> creatures,
            Dictionary<string, ItemDefinition> items,
            List<SpawnTableEntry> spawns,
            Aetherium.Model.ContentAtlas.ContentAtlas atlas)
        {
            CreaturesById = creatures;
            ItemsById = items;
            Atlas = atlas;
            // Only rows that resolve to a defined creature with a positive weight participate in
            // the draw — the validator flags the rest at load time; this guard keeps a directly-
            // constructed config from throwing at spawn time.
            _spawns = spawns.Where(s => s.Weight > 0 && creatures.ContainsKey(s.CreatureId)).ToList();
            _totalSpawnWeight = _spawns.Sum(s => s.Weight);
        }

        /// <summary>One weighted draw from the spawn table. Deterministic for a given
        /// <paramref name="rng"/> sequence — callers seed from the world's generation seed so a
        /// given (seed, table) always produces the same creature mix.</summary>
        public CreatureDefinition DrawSpawn(Random rng)
        {
            if (!HasSpawnTable)
                throw new InvalidOperationException("Spawn table is empty.");

            int roll = rng.Next(_totalSpawnWeight);
            foreach (var entry in _spawns)
            {
                roll -= entry.Weight;
                if (roll < 0)
                    return CreaturesById[entry.CreatureId];
            }
            return CreaturesById[_spawns[^1].CreatureId]; // unreachable; satisfies the compiler
        }
    }

    /// <summary>
    /// Compiles <see cref="ContentConfig"/> into a <see cref="ContentCatalog"/> and binds
    /// definitions onto live entities. Definitions bind exclusively to components that already
    /// exist (Health, AttackPower, ActionSpeed, Tile, Carriable, Consumable, Weapon,
    /// CreatureTypeTag) — content is data over the same engine, never new semantics.
    /// </summary>
    public static class ContentCompiler
    {
        public static ContentCatalog Compile(ContentConfig config)
        {
            var creatures = new Dictionary<string, CreatureDefinition>(StringComparer.Ordinal);
            foreach (var creature in config.Creatures)
                creatures.TryAdd(creature.Id, creature);

            var items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (var item in config.Items)
                items.TryAdd(item.Id, item);

            var atlas = Aetherium.Server.ContentAtlas.DefaultContentAtlas.Build();
            foreach (var creature in creatures.Values)
                atlas.AddEntityKindTag(new EntityKindTag(creature.Id, creature.Description is { Length: > 0 } d ? d : creature.Name));
            foreach (var item in items.Values)
                atlas.AddEntityKindTag(new EntityKindTag(item.Id, item.Name));

            return new ContentCatalog(creatures, items, new List<SpawnTableEntry>(config.Spawns), atlas);
        }

        /// <summary>
        /// Binds a creature definition onto an existing entity: stats, per-creature tile
        /// (registered in <paramref name="world"/>'s tile types on first use), and identity tag.
        /// When <paramref name="preserveHealthLevel"/> is set (snapshot re-hydration), current
        /// damage survives the re-skin — only Max comes from the definition.
        /// </summary>
        public static void ApplyCreature(Entity entity, CreatureDefinition definition, World world, bool preserveHealthLevel = false)
        {
            int level = preserveHealthLevel && entity.Has<Health>()
                ? Math.Min(entity.Get<Health>().Level, definition.Health)
                : definition.Health;
            entity.Set(new Health(level, definition.Health));
            entity.Set(new AttackPower(definition.AttackPower));
            entity.Set(new ActionSpeed(speed: definition.Speed, maxBudget: 1.0));
            entity.Set(new Tile { Type = EnsureTileType(world, definition) });
            entity.Set(new Aetherium.Components.CreatureTypeTag(definition.Id));
            ApplyVision(entity, definition.Vision);
        }

        /// <summary>
        /// Stamps a creature's per-type vision (directionality/FOV/range) onto its
        /// <see cref="Aetherium.Components.HasHeading"/> — get-or-create so callers needn't
        /// pre-add it. A null config leaves the creature omnidirectional (legacy default). This
        /// is what makes each character type able to perceive differently; the agent-perception
        /// path reads these fields to filter what an AI creature can see.
        /// </summary>
        public static void ApplyVision(Entity entity, Aetherium.Model.Content.VisionConfig? vision)
        {
            if (vision is null)
                return;

            var heading = entity.Has<Aetherium.Components.HasHeading>()
                ? entity.Get<Aetherium.Components.HasHeading>()
                : new Aetherium.Components.HasHeading();

            heading.IsDirectional = vision.Directional;
            if (vision.Directional)
                heading.FieldOfViewDegrees = System.Math.Clamp(vision.FieldOfView, 1, 360);
            heading.ViewRange = vision.Range;

            entity.Set(heading);
        }

        /// <summary>
        /// Materializes an item definition: a plain <see cref="Item"/> whose components carry the
        /// definition — the same shape as the hand-written item classes, minus the class.
        /// </summary>
        public static Item MaterializeItem(ItemDefinition definition)
        {
            var item = new Item();
            var carriable = item.Get<Carriable>();
            carriable.Label = definition.Name is { Length: > 0 } n ? n : definition.Id;
            carriable.Icon = definition.Icon;
            carriable.Weight = definition.Weight;

            if (definition.Heal is not null)
            {
                item.Set(new Consumable
                {
                    EffectType = ConsumableEffectType.HealthRestore,
                    EffectValue = definition.Heal.Amount,
                    Uses = definition.Heal.Uses,
                });
            }

            if (definition.WeaponBonus is int bonus)
                item.Set(new Weapon(carriable.Label, bonus));

            return item;
        }

        /// <summary>Registers (once) and returns the per-creature tile type carrying the
        /// definition's glyph/color. Prefixed so creature ids can never collide with terrain
        /// tile-type names.</summary>
        public static TileType EnsureTileType(World world, CreatureDefinition definition)
        {
            var name = "Creature:" + definition.Id;
            if (!world.TileTypes.TryGetValue(name, out var tileType))
            {
                tileType = new TileType
                {
                    Name = name,
                    Settings = new Dictionary<string, string>
                    {
                        { "MapCharacter", definition.Glyph is { Length: > 0 } g ? g : "M" },
                        { "BackgroundColor", ConsoleColor.Black.ToString() },
                        { "ForegroundColor", definition.Color is { Length: > 0 } c ? c : ConsoleColor.DarkRed.ToString() },
                    },
                };
                world.TileTypes[name] = tileType;
            }
            return tileType;
        }
    }
}
