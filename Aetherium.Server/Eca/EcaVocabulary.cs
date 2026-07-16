using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aetherium.Model.Eca;

namespace Aetherium.Server.Eca
{
    /// <summary>
    /// The reflectable registry of every ECA vocabulary tile (add-eca-scripting) — the single source of
    /// truth the validator, the documentation generator, the runtime, and (later) an editor palette all
    /// read. Tiles are discovered by reflecting the <see cref="IEcaTile"/> implementations in this
    /// assembly and reading each one's <see cref="EcaTileDefinition"/>, so adding a tile type is the only
    /// step needed to extend the language — no registration list to keep in sync. This mirrors the
    /// shipped <c>AgentToolRegistry</c> pattern the ECA design vision names as the extensibility model.
    /// </summary>
    public static class EcaVocabulary
    {
        private static readonly Dictionary<string, EcaTileDefinition> ById =
            DiscoverTiles().ToDictionary(d => d.Id, StringComparer.Ordinal);

        /// <summary>Every tile definition, ordered by role then id for stable enumeration/doc output.</summary>
        public static IReadOnlyList<EcaTileDefinition> All { get; } =
            ById.Values.OrderBy(d => d.Role).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();

        public static bool TryGet(string id, out EcaTileDefinition definition) => ById.TryGetValue(id, out definition!);

        public static bool Contains(string id) => ById.ContainsKey(id);

        public static IEnumerable<EcaTileDefinition> ByRole(EcaTileRole role) => All.Where(d => d.Role == role);

        private static IEnumerable<EcaTileDefinition> DiscoverTiles()
        {
            var tileType = typeof(IEcaTile);
            foreach (var type in typeof(EcaVocabulary).Assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || !tileType.IsAssignableFrom(type))
                    continue;
                if (Activator.CreateInstance(type) is IEcaTile tile)
                    yield return tile.Definition;
            }
        }
    }
}
