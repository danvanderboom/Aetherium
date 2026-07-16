using System.Collections.Generic;

namespace Aetherium.Model.Eca
{
    /// <summary>
    /// The role a vocabulary tile plays in a rule (add-eca-scripting). Mirrors the shape families of
    /// the ECA design vision (docs/audits/2026-07-06-engine-gap-analysis/design-eca-visual-scripting.md
    /// §4.2): a Trigger is the <c>when</c>, a Condition an <c>if</c> predicate, an Action a <c>do</c> verb.
    /// </summary>
    public enum EcaTileRole
    {
        Trigger,
        Condition,
        Action,
    }

    /// <summary>
    /// The declared type of a tile parameter. Beyond the primitives, <see cref="CreatureRef"/> and
    /// <see cref="StatusRef"/> are the cross-reference types that let validation (and, later, editor
    /// autocomplete) resolve a parameter against the world's content / the shipped status set without
    /// per-tile special-casing.
    /// </summary>
    public enum EcaValueType
    {
        Boolean,
        Integer,
        Number,
        Text,
        /// <summary>Names a creature id that must exist in the world's ContentConfig.</summary>
        CreatureRef,
        /// <summary>Names a shipped status effect ("burning"/"slowed"/"prone").</summary>
        StatusRef,
        /// <summary>One of a fixed set of string choices (see <see cref="EcaParameter.EnumChoices"/>).</summary>
        EnumChoice,
    }

    /// <summary>One typed parameter of a vocabulary tile — the metadata validation and doc-gen read.</summary>
    public sealed class EcaParameter
    {
        public string Name { get; }
        public EcaValueType ValueType { get; }
        public bool Required { get; }
        public string Description { get; }
        /// <summary>For <see cref="EcaValueType.EnumChoice"/>: the allowed string values.</summary>
        public IReadOnlyList<string> EnumChoices { get; }

        public EcaParameter(string name, EcaValueType valueType, bool required, string description,
            IReadOnlyList<string>? enumChoices = null)
        {
            Name = name;
            ValueType = valueType;
            Required = required;
            Description = description;
            EnumChoices = enumChoices ?? System.Array.Empty<string>();
        }
    }

    /// <summary>
    /// The reflectable definition of one vocabulary tile (a trigger, condition, or action). This is the
    /// programmatic type metadata the whole ECA subsystem draws from: the validator checks rules against
    /// it, <c>EcaVocabularyDoc</c> renders it, the runtime keys its switch on <see cref="Id"/>, and a
    /// future editor palette / plugin SDK reads the same surface — one definition, many consumers. The
    /// concrete definitions live on the server tile types; this Model type keeps the metadata reachable
    /// by any layer (clients, tools) without a server dependency.
    /// </summary>
    public sealed class EcaTileDefinition
    {
        public string Id { get; }
        public EcaTileRole Role { get; }
        public string Description { get; }
        public IReadOnlyList<EcaParameter> Parameters { get; }
        /// <summary>For actions that resolve a target: the valid <see cref="EcaActionTarget"/> names;
        /// empty for triggers, conditions, and actions whose target is implicit.</summary>
        public IReadOnlyList<string> ValidTargets { get; }

        public EcaTileDefinition(string id, EcaTileRole role, string description,
            IReadOnlyList<EcaParameter>? parameters = null, IReadOnlyList<string>? validTargets = null)
        {
            Id = id;
            Role = role;
            Description = description;
            Parameters = parameters ?? System.Array.Empty<EcaParameter>();
            ValidTargets = validTargets ?? System.Array.Empty<string>();
        }
    }
}
