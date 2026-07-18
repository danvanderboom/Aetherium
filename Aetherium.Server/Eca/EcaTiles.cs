using System.Collections.Generic;
using Aetherium.Model.Eca;

namespace Aetherium.Server.Eca
{
    /// <summary>
    /// A vocabulary tile — one trigger, condition, or action of the ECA language
    /// (add-eca-scripting). A tile carries only its <see cref="Definition"/> this slice (pure
    /// metadata); the runtime executes by keying on the tile's id constant. Later slices give action
    /// tiles an Execute method, matching the design vision's <c>[Eca*]</c>/tool-registry model — the
    /// interface is the seam that grows. Every tile has a parameterless constructor so
    /// <see cref="EcaVocabulary"/> can discover it by reflection.
    /// </summary>
    public interface IEcaTile
    {
        EcaTileDefinition Definition { get; }
    }

    // --- Triggers ---

    /// <summary>Raised at the shared monster-defeat chokepoint when a creature dies.</summary>
    public sealed class CreatureDiedTrigger : IEcaTile
    {
        public const string Id = "creature_died";
        public EcaTileDefinition Definition { get; } = new(
            Id, EcaTileRole.Trigger,
            "Fires when a creature is defeated (by melee or ability). Binds the victim's creature type, " +
            "the killer, and the death location.");
    }

    /// <summary>Raised by the canonical-world recognition sweep when a character recognizes another
    /// (add-identity-recognition), once per encounter.</summary>
    public sealed class CharacterRecognizedTrigger : IEcaTile
    {
        public const string Id = "character_recognized";
        public EcaTileDefinition Definition { get; } = new(
            Id, EcaTileRole.Trigger,
            "Fires when one character recognizes another within range (once per encounter). Binds the " +
            "recognizer, the recognized character, both kinds, the effective familiarity, whether this " +
            "is a first meeting, and the recognizer's location.");
    }

    // --- Conditions ---

    /// <summary>True when the dead creature's type equals a given content creature id.</summary>
    public sealed class CreatureTypeIsCondition : IEcaTile
    {
        public const string Id = "creature_type_is";
        public const string CreatureTypeParam = "creatureType";
        public EcaTileDefinition Definition { get; } = new(
            Id, EcaTileRole.Condition,
            "True when the event's creature is of the named type.",
            new[]
            {
                new EcaParameter(CreatureTypeParam, EcaValueType.CreatureRef, required: true,
                    "The content creature id to match against the event's victim."),
            });
    }

    /// <summary>A probabilistic gate — true with the given probability, from the seeded RNG.</summary>
    public sealed class ChanceCondition : IEcaTile
    {
        public const string Id = "chance";
        public const string ProbabilityParam = "probability";
        public EcaTileDefinition Definition { get; } = new(
            Id, EcaTileRole.Condition,
            "Passes with the given probability (0..1), drawn from the world's seeded rule RNG.",
            new[]
            {
                new EcaParameter(ProbabilityParam, EcaValueType.Number, required: true,
                    "Probability in [0, 1] that this condition passes."),
            });
    }

    /// <summary>True when the recognized character's kind matches (add-identity-recognition).</summary>
    public sealed class RecognizedKindIsCondition : IEcaTile
    {
        public const string Id = "recognized_kind_is";
        public const string KindParam = "kind";
        public EcaTileDefinition Definition { get; } = new(
            Id, EcaTileRole.Condition,
            "True when the recognized character's kind equals the named kind (creature type, or " +
            "\"character\" for a player).",
            new[]
            {
                new EcaParameter(KindParam, EcaValueType.Text, required: true,
                    "The kind to match against the recognized character."),
            });
    }

    /// <summary>True when the event's effective familiarity meets a minimum (add-identity-recognition).</summary>
    public sealed class FamiliarityAtLeastCondition : IEcaTile
    {
        public const string Id = "familiarity_at_least";
        public const string MinParam = "minFamiliarity";
        public EcaTileDefinition Definition { get; } = new(
            Id, EcaTileRole.Condition,
            "True when the recognizer's effective familiarity with the recognized character is at least " +
            "the given value (0..1).",
            new[]
            {
                new EcaParameter(MinParam, EcaValueType.Number, required: true,
                    "Minimum effective familiarity in [0, 1]."),
            });
    }

    /// <summary>Gates on whether the recognition is a first meeting (add-identity-recognition).</summary>
    public sealed class FirstMeetingIsCondition : IEcaTile
    {
        public const string Id = "first_meeting_is";
        public const string ValueParam = "firstMeeting";
        public EcaTileDefinition Definition { get; } = new(
            Id, EcaTileRole.Condition,
            "True when the event's first-meeting flag equals the given value — distinguish a stranger " +
            "(true) from a known individual (false).",
            new[]
            {
                new EcaParameter(ValueParam, EcaValueType.Boolean, required: true,
                    "Whether the rule fires only on a first meeting (true) or only on a re-encounter (false)."),
            });
    }

    // --- Actions ---

    /// <summary>Spawns a content-catalog creature at the death location plus an optional offset.</summary>
    public sealed class SpawnCreatureAction : IEcaTile
    {
        public const string Id = "spawn_creature";
        public const string CreatureIdParam = "creatureId";
        public const string OffsetXParam = "offsetX";
        public const string OffsetYParam = "offsetY";
        public EcaTileDefinition Definition { get; } = new(
            Id, EcaTileRole.Action,
            "Spawns a creature from the world's content catalog at the death location (plus offset).",
            new[]
            {
                new EcaParameter(CreatureIdParam, EcaValueType.CreatureRef, required: true,
                    "The content creature id to spawn."),
                new EcaParameter(OffsetXParam, EcaValueType.Integer, required: false,
                    "Tiles east of the death location (default 0)."),
                new EcaParameter(OffsetYParam, EcaValueType.Integer, required: false,
                    "Tiles south of the death location (default 0)."),
            });
    }

    /// <summary>Deals damage to the killer or victim through the map's damage pipeline.</summary>
    public sealed class DealDamageAction : IEcaTile
    {
        public const string Id = "deal_damage";
        public const string AmountParam = "amount";
        public const string DamageTypeParam = "damageType";
        public EcaTileDefinition Definition { get; } = new(
            Id, EcaTileRole.Action,
            "Deals damage to the target through the map's damage pipeline (e.g. a death-surge that " +
            "hurts the killer).",
            new[]
            {
                new EcaParameter(AmountParam, EcaValueType.Number, required: true,
                    "Damage amount (must be > 0)."),
                new EcaParameter(DamageTypeParam, EcaValueType.Text, required: false,
                    "Damage type tag (campaign-defined; defaults to \"physical\")."),
            },
            validTargets: new[]
            {
                nameof(EcaActionTarget.Killer), nameof(EcaActionTarget.Victim),
                nameof(EcaActionTarget.Recognizer), nameof(EcaActionTarget.Recognized),
            });
    }

    /// <summary>Applies a shipped status effect (burning/slowed/prone) to the killer or victim.</summary>
    public sealed class ApplyStatusAction : IEcaTile
    {
        public const string Id = "apply_status";
        public const string StatusIdParam = "statusId";
        public const string DurationTicksParam = "durationTicks";
        public const string MagnitudeParam = "magnitude";

        /// <summary>The shipped status ids an <see cref="ApplyStatusAction"/> may name — the same set
        /// <c>AbilityCompiler.BuildStatus</c> understands. This list drives both the tile's EnumChoice
        /// metadata and (through it) validation, so the two never diverge.</summary>
        public static readonly string[] KnownStatuses = { "burning", "slowed", "prone" };

        public EcaTileDefinition Definition { get; } = new(
            Id, EcaTileRole.Action,
            "Applies a shipped status effect to the target for a duration.",
            new[]
            {
                new EcaParameter(StatusIdParam, EcaValueType.StatusRef, required: true,
                    "The status to apply.", enumChoices: KnownStatuses),
                new EcaParameter(DurationTicksParam, EcaValueType.Integer, required: false,
                    "How many ticks the status lasts."),
                new EcaParameter(MagnitudeParam, EcaValueType.Number, required: false,
                    "Per-status magnitude: damage-per-tick for burning, speed multiplier for slowed; " +
                    "ignored for prone."),
            },
            validTargets: new[]
            {
                nameof(EcaActionTarget.Killer), nameof(EcaActionTarget.Victim),
                nameof(EcaActionTarget.Recognizer), nameof(EcaActionTarget.Recognized),
            });
    }
}
