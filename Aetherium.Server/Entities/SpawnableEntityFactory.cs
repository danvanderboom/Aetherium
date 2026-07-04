using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;

namespace Aetherium.Entities
{
    /// <summary>
    /// Creates concrete <see cref="Entity"/> instances by (case-insensitive) type name. The set of
    /// spawnable types is every non-abstract <see cref="Entity"/> subclass in the entity assembly
    /// that has a public parameterless constructor — <see cref="Terrain"/> and any constructor-only
    /// entity (e.g. <c>Button</c>) are excluded automatically. Shared by the world-building
    /// <c>SpawnEntityTool</c> and the prefab stamper so simple name→entity resolution stays in one
    /// place. (Distinct from <c>Aetherium.Server.MultiWorld.EntityFactory</c>, which reconstructs
    /// entities from snapshot <c>EntityPlacement</c> records with full property application.)
    /// </summary>
    public static class SpawnableEntityFactory
    {
        private static readonly Lazy<IReadOnlyDictionary<string, Type>> Types = new(() =>
            typeof(Item).Assembly.GetTypes()
                .Where(t => typeof(Entity).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .ToDictionary(t => t.Name.ToLowerInvariant(), t => t));

        /// <summary>
        /// The names of all spawnable entity types, sorted — useful for error messages and discovery.
        /// </summary>
        public static IReadOnlyList<string> SupportedTypeNames =>
            Types.Value.Values.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>
        /// True if <paramref name="entityType"/> resolves to a spawnable entity type.
        /// </summary>
        public static bool IsKnownType(string? entityType) =>
            !string.IsNullOrWhiteSpace(entityType) && Types.Value.ContainsKey(entityType.ToLowerInvariant());

        /// <summary>
        /// Attempts to create a new entity of the named type. Returns false (without throwing) when
        /// the name is empty or unknown; may throw only if the resolved type's constructor throws.
        /// </summary>
        public static bool TryCreate(string? entityType, out Entity entity)
        {
            entity = null!;
            if (string.IsNullOrWhiteSpace(entityType))
                return false;
            if (!Types.Value.TryGetValue(entityType.ToLowerInvariant(), out var clrType))
                return false;

            entity = (Entity)Activator.CreateInstance(clrType)!;
            return true;
        }
    }
}
