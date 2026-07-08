using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model.Abilities
{
    /// <summary>
    /// Model-layer mirror of the server's <c>ResourceRegenPolicy</c>. Lives in Aetherium.Model so a
    /// world's ability content can be declared as data reachable from both the server-side per-world
    /// config (<c>WorldConfig</c>) and the world-creation contract (<c>WorldTemplate</c>) — the same
    /// data/behavior split <c>DeathPolicy</c> and <c>ContentAtlas</c> use. The server compiler maps
    /// this to its runtime enum.
    /// </summary>
    [GenerateSerializer]
    public enum ResourceRegenPolicyKind
    {
        OutOfCombat,
        OnHit,
        Continuous,
    }

    /// <summary>
    /// Declarative definition of one resource pool a world's characters start with (mana, stamina,
    /// focus, battery, heat, oxygen — all data). Compiled into a fresh runtime <c>ResourcePool</c>
    /// per character at join time.
    /// </summary>
    [GenerateSerializer]
    public class ResourcePoolDefinition
    {
        [Id(0)] public string Tag { get; set; } = string.Empty;
        [Id(1)] public double Max { get; set; }
        [Id(2)] public double RegenPerTick { get; set; }
        [Id(3)] public ResourceRegenPolicyKind RegenPolicy { get; set; } = ResourceRegenPolicyKind.Continuous;

        /// <summary>A "heat"-style pool that fills with use and vents (regens) toward zero.</summary>
        [Id(4)] public bool IsInverse { get; set; }

        /// <summary>For an inverse pool: spending that would push Current above this is refused.</summary>
        [Id(5)] public double? OverheatThreshold { get; set; }

        /// <summary>Starting Current; null means full (normal pool) or empty (inverse pool).</summary>
        [Id(6)] public double? StartingValue { get; set; }
    }

    /// <summary>Which composable effect an <see cref="AbilityEffectDescriptor"/> describes. Closed set
    /// this slice (the three effect kinds that route through already-shipped systems); extended as new
    /// effect kinds ship.</summary>
    [GenerateSerializer]
    public enum AbilityEffectKind
    {
        DealDamage,
        ApplyStatus,
        ModifyResource,
    }

    /// <summary>Whether a <see cref="AbilityEffectKind.ModifyResource"/> effect adjusts the caster's
    /// own pool or the target's.</summary>
    [GenerateSerializer]
    public enum AbilityEffectTarget
    {
        Caster,
        Target,
    }

    /// <summary>
    /// Pure-data description of one ability effect. A single class with a <see cref="Kind"/> plus the
    /// parameters that kind consumes (modes that don't use a field ignore it) — deliberately mirrors
    /// <c>RespawnLocationPolicy</c>'s Mode+optional-fields shape, so the whole ability content model
    /// serializes without a polymorphic type hierarchy. The server's <c>AbilityCompiler</c> turns
    /// each descriptor into the matching runtime <c>IAbilityEffect</c>.
    /// </summary>
    [GenerateSerializer]
    public class AbilityEffectDescriptor
    {
        [Id(0)] public AbilityEffectKind Kind { get; set; }

        // DealDamage
        /// <summary>Damage type tag (campaign-defined: fire/kinetic/…). Defaults to "physical" if unset.</summary>
        [Id(1)] public string? DamageType { get; set; }
        [Id(2)] public double Amount { get; set; }

        // ApplyStatus
        /// <summary>Status id: "burning", "slowed", or "prone" (the shipped status effects).</summary>
        [Id(3)] public string? StatusId { get; set; }
        [Id(4)] public int DurationTicks { get; set; }
        /// <summary>Per-status magnitude: damage-per-tick for "burning", speed multiplier for "slowed"; ignored for "prone".</summary>
        [Id(5)] public double Magnitude { get; set; }

        // ModifyResource
        [Id(6)] public string? PoolTag { get; set; }
        [Id(7)] public double Delta { get; set; }
        [Id(8)] public AbilityEffectTarget ResourceTarget { get; set; }
    }

    /// <summary>
    /// Declarative definition of one ability (engine gap-analysis §4.3). Pure serializable data — no
    /// effect instances, no service references — so a world can carry its whole ability set through
    /// world creation and persistence. The server's <c>AbilityCompiler</c> compiles this into the
    /// runtime <c>Ability</c>, binding the map's damage pipeline into its effects. Timing fields
    /// (<see cref="ChargeTime"/>/<see cref="CastTime"/>/<see cref="RecoverTime"/>) are carried now but
    /// unconsumed until phased casting ships (see openspec/changes/wire-abilities-live Later slices);
    /// this slice executes every cast instantly.
    /// </summary>
    [GenerateSerializer]
    public class AbilityDefinition
    {
        [Id(0)] public string Id { get; set; } = string.Empty;
        [Id(1)] public string? ResourcePoolTag { get; set; }
        [Id(2)] public double ResourceCost { get; set; }
        [Id(3)] public double ChargeTime { get; set; }
        [Id(4)] public double CastTime { get; set; }
        [Id(5)] public double RecoverTime { get; set; }
        [Id(6)] public double Cooldown { get; set; }
        [Id(7)] public double Range { get; set; } = 1;

        /// <summary>Renderer-facing visual tag ("beam", "projectile", "aoe_ground", …). This slice
        /// resolves only self-target and single-entity-target casts; shape resolution is a later slice.</summary>
        [Id(8)] public string TargetShape { get; set; } = "single";

        [Id(9)] public List<AbilityEffectDescriptor> Effects { get; set; } = new();
        [Id(10)] public List<string> Tags { get; set; } = new();
    }

    /// <summary>
    /// A world's ability content: the abilities available on its maps and the resource pools every
    /// character in the world starts with. Threaded through world creation exactly like
    /// <c>DeathPolicy</c>; null anywhere means the world has no abilities and stamps no pools. The
    /// engine ships no default — the ability set is entirely campaign-supplied data.
    /// </summary>
    [GenerateSerializer]
    public class AbilityConfig
    {
        [Id(0)] public List<AbilityDefinition> Abilities { get; set; } = new();
        [Id(1)] public List<ResourcePoolDefinition> CharacterResourcePools { get; set; } = new();
    }
}
