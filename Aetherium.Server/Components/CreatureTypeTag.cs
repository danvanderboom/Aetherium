using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Preserves the spawn request's <c>CreatureType</c> string ("wolf", "bandit", …) on the spawned
    /// entity. Several creature types map onto the same C# class (e.g. <c>Monster</c>), so without
    /// this tag their identity collapses to the type name at every downstream site. First consumer is
    /// the faction standing loop's <c>kill:&lt;creature-type&gt;</c> action tags (engine gap-analysis
    /// §4.6 — see wire-factions-live), but it is a general identity tag, not faction-specific.
    /// </summary>
    public class CreatureTypeTag : Component
    {
        public string Value { get; set; } = string.Empty;

        public CreatureTypeTag() { }

        public CreatureTypeTag(string value)
        {
            Value = value;
        }
    }
}
