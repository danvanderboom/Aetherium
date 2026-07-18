using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model.Eca
{
    /// <summary>
    /// A world's reactive logic (add-eca-scripting): event–condition–action rules loaded from a bundle's
    /// <c>rules.yaml</c>. Pure serializable data — the sixth member of the per-world config family
    /// (death/abilities/progression/factions/content). Null anywhere means the world has no rules and the
    /// kill path behaves exactly as before. This is the T0 runtime spine of the ECA vision
    /// (docs/eca-scripting.md); the visual editor, plugin SDK, selectors, and wider event catalog are later.
    /// </summary>
    [GenerateSerializer]
    public class EcaConfig
    {
        [Id(0)] public List<EcaRule> Rules { get; set; } = new();
    }

    /// <summary>
    /// One rule: a trigger (<see cref="When"/>), an optional AND-ed condition list (<see cref="If"/>,
    /// empty ⇒ always fire on the trigger), and an ordered action list (<see cref="Do"/>). Ids for
    /// triggers/conditions/actions are strings resolved against <c>EcaVocabulary</c> — a closed set this
    /// slice, but string-keyed so the vocabulary (not a sealed enum) stays the single source of truth and
    /// the plugin-extensibility path in the vision doesn't require a Model change.
    /// </summary>
    [GenerateSerializer]
    public class EcaRule
    {
        [Id(0)] public string Id { get; set; } = string.Empty;

        /// <summary>The trigger tile id (e.g. "creature_died").</summary>
        [Id(1)] public string When { get; set; } = string.Empty;

        [Id(2)] public List<EcaConditionDescriptor> If { get; set; } = new();

        [Id(3)] public List<EcaActionDescriptor> Do { get; set; } = new();
    }

    /// <summary>
    /// A condition predicate — <see cref="Kind"/> (a vocabulary condition id) plus the union of fields the
    /// conditions use, unused fields ignored. Same flat/typed discipline as <c>AbilityEffectDescriptor</c>,
    /// so YAML binds without a polymorphic hierarchy.
    /// </summary>
    [GenerateSerializer]
    public class EcaConditionDescriptor
    {
        [Id(0)] public string Kind { get; set; } = string.Empty;

        // creature_type_is
        [Id(1)] public string? CreatureType { get; set; }

        // chance
        [Id(2)] public double Probability { get; set; }

        // recognized_kind_is (add-identity-recognition)
        [Id(3)] public string? RecognizedKind { get; set; }

        // familiarity_at_least (add-identity-recognition)
        [Id(4)] public double MinFamiliarity { get; set; }

        // first_meeting_is (add-identity-recognition)
        [Id(5)] public bool FirstMeeting { get; set; }
    }

    /// <summary>Which entity an action that resolves a target applies to.</summary>
    [GenerateSerializer]
    public enum EcaActionTarget
    {
        /// <summary>The actor who dealt the killing blow.</summary>
        Killer,
        /// <summary>The entity that died (still present as Dying/Corpse at execution time).</summary>
        Victim,
        /// <summary>The character doing the recognizing (add-identity-recognition).</summary>
        Recognizer,
        /// <summary>The character being recognized (add-identity-recognition).</summary>
        Recognized,
    }

    /// <summary>
    /// An action — <see cref="Kind"/> (a vocabulary action id), an optional <see cref="Target"/>, plus the
    /// union of fields the actions use. Flat/typed like <see cref="EcaConditionDescriptor"/>.
    /// </summary>
    [GenerateSerializer]
    public class EcaActionDescriptor
    {
        [Id(0)] public string Kind { get; set; } = string.Empty;

        /// <summary>Target for actions that resolve one (deal_damage/apply_status); ignored by
        /// spawn_creature, which acts at the death location.</summary>
        [Id(1)] public EcaActionTarget Target { get; set; } = EcaActionTarget.Killer;

        // spawn_creature
        [Id(2)] public string? CreatureId { get; set; }
        [Id(3)] public int OffsetX { get; set; }
        [Id(4)] public int OffsetY { get; set; }

        // deal_damage
        [Id(5)] public string? DamageType { get; set; }
        [Id(6)] public double Amount { get; set; }

        // apply_status
        [Id(7)] public string? StatusId { get; set; }
        [Id(8)] public int DurationTicks { get; set; }
        [Id(9)] public double Magnitude { get; set; }
    }
}
