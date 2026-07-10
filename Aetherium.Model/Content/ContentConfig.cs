using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model.Content
{
    /// <summary>
    /// Per-world content vocabulary (openspec/changes/add-content-definitions): what creatures
    /// and items exist in this game and in what mix they populate a map. Pure serializable data —
    /// the fifth member of the per-world config family (death/abilities/progression/factions).
    /// Population passes keep deciding <em>where and how many</em>; this config decides
    /// <em>what</em>. A null config anywhere preserves legacy behavior exactly.
    /// </summary>
    [GenerateSerializer]
    public class ContentConfig
    {
        [Id(0)] public List<CreatureDefinition> Creatures { get; set; } = new();

        [Id(1)] public List<ItemDefinition> Items { get; set; } = new();

        /// <summary>Weighted mix that fills the monster slots the population passes create.</summary>
        [Id(2)] public List<SpawnTableEntry> Spawns { get; set; } = new();
    }

    /// <summary>
    /// A creature kind, bound at spawn time onto the components that already exist
    /// (<c>Health</c>, <c>AttackPower</c>, <c>ActionSpeed</c>, <c>Tile</c>, <c>CreatureTypeTag</c>).
    /// </summary>
    [GenerateSerializer]
    public class CreatureDefinition
    {
        /// <summary>Stable id — becomes the entity's <c>CreatureTypeTag</c> and therefore the
        /// faction doctrine's <c>kill:&lt;id&gt;</c> vocabulary.</summary>
        [Id(0)] public string Id { get; set; } = string.Empty;

        [Id(1)] public string Name { get; set; } = string.Empty;

        [Id(2)] public string Description { get; set; } = string.Empty;

        /// <summary>Single-character map glyph.</summary>
        [Id(3)] public string Glyph { get; set; } = "M";

        /// <summary>ConsoleColor name for the shipped console renderer (validated at load).
        /// Renderer-agnostic identity is the atlas entity-kind tag, not this field.</summary>
        [Id(4)] public string Color { get; set; } = "DarkRed";

        [Id(5)] public int Health { get; set; } = 30;

        [Id(6)] public int AttackPower { get; set; } = 6;

        /// <summary>ActionSpeed speed multiplier — 1.0 acts every eligible tick (legacy baseline),
        /// &lt;1 acts less often, &gt;1 more often. MaxBudget stays 1.0.</summary>
        [Id(7)] public double Speed { get; set; } = 1.0;

        /// <summary>Behavior-tree preset id; must name a known preset ("wander-melee").</summary>
        [Id(8)] public string Behavior { get; set; } = "wander-melee";

        /// <summary>Item this creature drops on death; null drops nothing. (A victim with no
        /// definition at all keeps the legacy SwordItem drop.)</summary>
        [Id(9)] public string? LootItemId { get; set; }
    }

    /// <summary>
    /// An item kind, bound onto the existing item component set: <c>Carriable</c> always,
    /// <c>Consumable</c> when <see cref="Heal"/> is present, <c>Weapon</c> when
    /// <see cref="WeaponBonus"/> is present.
    /// </summary>
    [GenerateSerializer]
    public class ItemDefinition
    {
        [Id(0)] public string Id { get; set; } = string.Empty;

        /// <summary>Carriable label shown in inventories.</summary>
        [Id(1)] public string Name { get; set; } = string.Empty;

        /// <summary>Carriable icon (single character).</summary>
        [Id(2)] public string Icon { get; set; } = "?";

        [Id(3)] public int Weight { get; set; } = 1;

        /// <summary>Optional restorative effect → <c>Consumable(HealthRestore)</c>.</summary>
        [Id(4)] public HealEffectDefinition? Heal { get; set; }

        /// <summary>Optional attack bonus while carried → <c>Weapon(Name, bonus)</c>.</summary>
        [Id(5)] public int? WeaponBonus { get; set; }
    }

    [GenerateSerializer]
    public class HealEffectDefinition
    {
        [Id(0)] public int Amount { get; set; } = 20;

        [Id(1)] public int Uses { get; set; } = 1;
    }

    /// <summary>One weighted row of the spawn mix.</summary>
    [GenerateSerializer]
    public class SpawnTableEntry
    {
        [Id(0)] public string CreatureId { get; set; } = string.Empty;

        [Id(1)] public int Weight { get; set; } = 1;
    }
}
