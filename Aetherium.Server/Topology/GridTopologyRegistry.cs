using System;
using System.Collections.Generic;
using System.Linq;

namespace Aetherium.Topology
{
    /// <summary>
    /// Stateless topology singletons by name. A world's <c>topology</c> config field is
    /// resolved here exactly once at grain init (the ContentCompiler/EcaRuntime
    /// compile-once pattern) onto <see cref="Aetherium.Core.World.Topology"/>; an
    /// omitted or null field means <see cref="DefaultName"/> ("square"), byte-identically
    /// to the pre-seam engine. P1 registers "hex", P2 "tri" (docs/grid-topologies.md).
    /// </summary>
    public static class GridTopologyRegistry
    {
        public const string DefaultName = "square";

        private static readonly Dictionary<string, IGridTopology> Topologies =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [SquareTopology.Instance.Name] = SquareTopology.Instance,
            };

        /// <summary>Resolves a topology by name; null/empty falls back to square.
        /// Throws on an unknown name — the bundle validator rejects those upstream,
        /// so reaching here with one means a raw world-creation request carried a
        /// typo, and failing world creation loudly beats a silently-square world.</summary>
        public static IGridTopology Get(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Topologies[DefaultName];

            if (Topologies.TryGetValue(name, out var topology))
                return topology;

            throw new ArgumentException(
                $"Unknown grid topology '{name}' (known: {string.Join(", ", Names)}).", nameof(name));
        }

        public static bool TryGet(string? name, out IGridTopology topology)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                topology = Topologies[DefaultName];
                return true;
            }

            return Topologies.TryGetValue(name, out topology!);
        }

        public static IReadOnlyCollection<string> Names => Topologies.Keys.ToList();
    }
}
