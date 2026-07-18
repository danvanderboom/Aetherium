using System.Collections.Generic;
using Aetherium.Components;
using Aetherium.WorldGen;
using Orleans;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Snapshot of a map's world state, sufficient to hydrate an equivalent
    /// <c>World</c> instance locally.
    ///
    /// <para>
    /// Phase 1 semantics: the snapshot is a <see cref="WorldRecipe"/> (generator,
    /// seed, parameters) plus a list of <see cref="EntityPlacement"/> records.
    /// Two joiners hydrating from the same snapshot get identical layouts and
    /// identical entity IDs but independent <c>World</c> instances — mutations
    /// in one session do NOT propagate to the other or back to the grain.
    /// Live shared mutation is deferred to phase 2.
    /// </para>
    /// </summary>
    [GenerateSerializer]
    public class WorldSnapshot
    {
        [Id(0)] public string WorldId { get; set; } = string.Empty;
        [Id(1)] public string MapId { get; set; } = string.Empty;
        [Id(2)] public WorldSize Size { get; set; } = new WorldSize();
        [Id(3)] public WorldRecipe Recipe { get; set; } = new WorldRecipe();
        [Id(4)] public List<EntityPlacement> Entities { get; set; } = new List<EntityPlacement>();

        /// <summary>
        /// Snapshot format version. Bump in phase 2 when EntityPlacement gains
        /// component state, so hydrators can refuse incompatible payloads.
        /// </summary>
        [Id(5)] public int SnapshotVersion { get; set; } = 1;
    }

    /// <summary>
    /// Recipe for deterministically regenerating a map's terrain. The same
    /// (GeneratorType, Seed, Parameters, Template) tuple SHALL produce the same
    /// terrain output via <see cref="WorldGenerationOrchestrator"/>.
    /// </summary>
    [GenerateSerializer]
    public class WorldRecipe
    {
        [Id(0)] public string GeneratorType { get; set; } = string.Empty;
        [Id(1)] public int Seed { get; set; }
        [Id(2)] public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        [Id(3)] public WorldGenerationTemplate Template { get; set; } = WorldGenerationTemplate.Dungeon;
        [Id(4)] public string GeneratorVersion { get; set; } = "1.0.0";
        [Id(5)] public int Width { get; set; }
        [Id(6)] public int Height { get; set; }
        [Id(7)] public int Levels { get; set; } = 1;

        /// <summary>
        /// Grid tiling name ("square"/"hex"/"tri"/"h3"); null ⇒ square. Persisted so a grain that
        /// regenerates its world from the recipe on reactivation rebuilds it on the same tiling —
        /// without it, an H3 sphere would silently regenerate as a square grid (docs/h3-topology.md).
        /// </summary>
        [Id(8)] public string? Topology { get; set; }
    }

    /// <summary>
    /// A single entity to instantiate when hydrating a snapshot. Phase 1 covers
    /// initial placement; phase 2 will add a serialized-component blob.
    /// </summary>
    [GenerateSerializer]
    public class EntityPlacement
    {
        [Id(0)] public string EntityId { get; set; } = string.Empty;

        /// <summary>
        /// Short type name (e.g. "Key", "Door", "Snake"). Resolved by
        /// <see cref="EntityFactory"/> when hydrating.
        /// </summary>
        [Id(1)] public string TypeName { get; set; } = string.Empty;

        [Id(2)] public int X { get; set; }
        [Id(3)] public int Y { get; set; }
        [Id(4)] public int Z { get; set; }

        /// <summary>
        /// Free-form properties that <see cref="EntityFactory"/> may consume during
        /// instantiation (e.g. a Key's color, a Door's open/closed flag). Phase 1
        /// uses this minimally; phase 2 generalizes to full component state.
        /// </summary>
        [Id(5)] public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        public WorldLocation ToWorldLocation() => new WorldLocation(X, Y, Z);

        public static EntityPlacement FromLocation(string entityId, string typeName, WorldLocation location)
            => new EntityPlacement
            {
                EntityId = entityId,
                TypeName = typeName,
                X = location.X,
                Y = location.Y,
                Z = location.Z
            };
    }

    /// <summary>
    /// Result of <see cref="IGameMapGrain.JoinPlayerAsync"/>. On success carries the
    /// unique spawn location assigned by the grain and the player's authoritative
    /// entity ID.
    /// </summary>
    [GenerateSerializer]
    public class JoinMapResult
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string? Reason { get; set; }
        [Id(2)] public string MapId { get; set; } = string.Empty;
        [Id(3)] public int SpawnX { get; set; }
        [Id(4)] public int SpawnY { get; set; }
        [Id(5)] public int SpawnZ { get; set; }
        [Id(6)] public string PlayerEntityId { get; set; } = string.Empty;

        public WorldLocation SpawnLocation() => new WorldLocation(SpawnX, SpawnY, SpawnZ);

        public static JoinMapResult Ok(string mapId, WorldLocation spawn, string playerEntityId)
            => new JoinMapResult
            {
                Success = true,
                MapId = mapId,
                SpawnX = spawn.X,
                SpawnY = spawn.Y,
                SpawnZ = spawn.Z,
                PlayerEntityId = playerEntityId
            };

        public static JoinMapResult Fail(string reason)
            => new JoinMapResult { Success = false, Reason = reason };
    }

    /// <summary>
    /// Hub-facing return type for <c>GameHub.JoinWorld</c>.
    /// </summary>
    [GenerateSerializer]
    public class JoinWorldResult
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string? Reason { get; set; }
        [Id(2)] public string? WorldId { get; set; }
        [Id(3)] public string? MapId { get; set; }
        [Id(4)] public int SpawnX { get; set; }
        [Id(5)] public int SpawnY { get; set; }
        [Id(6)] public int SpawnZ { get; set; }

        public static JoinWorldResult Ok(string worldId, string mapId, WorldLocation spawn)
            => new JoinWorldResult
            {
                Success = true,
                WorldId = worldId,
                MapId = mapId,
                SpawnX = spawn.X,
                SpawnY = spawn.Y,
                SpawnZ = spawn.Z
            };

        public static JoinWorldResult Fail(string reason)
            => new JoinWorldResult { Success = false, Reason = reason };
    }
}
