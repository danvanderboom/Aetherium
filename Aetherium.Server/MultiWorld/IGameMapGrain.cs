using Orleans;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain interface for a single game map within a world.
    /// Each map represents a region, floor, district, etc.
    /// </summary>
    public interface IGameMapGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Initializes the map with a generated world.
        /// </summary>
        Task InitializeAsync(string worldId, string mapName, WorldSize size, string generatorType, Dictionary<string, object> parameters);

        /// <summary>
        /// Gets the current world state for this map.
        /// </summary>
        Task<string?> GetWorldAsync();

        /// <summary>
        /// Gets map metadata.
        /// </summary>
        Task<MapMetadata?> GetMetadataAsync();

        /// <summary>
        /// Adds a player to this map (legacy entry). Preserved for cross-map moves
        /// triggered by IWorldGrain where spawn details aren't needed.
        /// New code in GameHub should call <see cref="JoinPlayerAsync"/>.
        /// </summary>
        Task<bool> AddPlayerAsync(string playerId);

        /// <summary>
        /// Registers a player on this map and returns the joining context: a unique
        /// spawn location and the player's authoritative entity ID. Used by
        /// GameHub.JoinWorld to bind a session to this map.
        /// </summary>
        Task<JoinMapResult> JoinPlayerAsync(string playerId);

        /// <summary>
        /// Returns a snapshot of the map's canonical World sufficient to hydrate an
        /// equivalent World instance locally. Phase 1: recipe + entity placements.
        /// </summary>
        Task<WorldSnapshot> GetWorldSnapshotAsync();

        /// <summary>
        /// Returns a snapshot, omitting the joining player's own Character so they
        /// don't appear twice in their hydrated world. Phase 2 entry point.
        /// </summary>
        Task<WorldSnapshot> GetWorldSnapshotForJoinerAsync(string joinerPlayerId);

        // ------------------------------------------------------------------
        // Phase 2b+c — grain-authoritative mutation methods.
        // Each method mutates the grain's _world for the player identified by
        // sessionId, emits the appropriate MapDelta to the map's SignalR group,
        // and returns a typed result for the caller. Orleans's single-threaded
        // grain contract serializes concurrent invocations.
        // ------------------------------------------------------------------

        Task<MoveResult> MoveAsync(string sessionId, Aetherium.Model.RelativeDirection direction, int distance);

        Task<RotateResult> RotateAsync(string sessionId, int degrees);

        Task<ChangeLevelResult> ChangeLevelAsync(string sessionId, int deltaZ);

        Task<Aetherium.Model.InteractionResultDto> PickupAsync(string sessionId, string targetEntityId);

        Task<Aetherium.Model.InteractionResultDto> DropAsync(string sessionId, string itemEntityId);

        Task<Aetherium.Model.InteractionResultDto> UseAsync(string sessionId, string itemEntityId, string onEntityId, string? usageId);

        Task<Aetherium.Model.InteractionResultDto> OpenAsync(string sessionId, string targetEntityId);

        Task<Aetherium.Model.InteractionResultDto> CloseAsync(string sessionId, string targetEntityId);

        /// <summary>
        /// Resolves a melee attack by the session's Character against an adjacent target, applying
        /// damage to canonical state and fanning out a health-change or entity-removed delta.
        /// </summary>
        Task<Aetherium.Model.AttackResultDto> AttackAsync(string sessionId, string targetEntityId);

        /// <summary>Rolling combat analytics for this map: monsters defeated + total damage dealt (P3-7 slice 2).</summary>
        Task<Aetherium.Model.CombatStatsDto> GetCombatStatsAsync();

        /// <summary>
        /// Removes a player's Character from <c>_world</c> on disconnect or explicit
        /// LeaveWorld. Emits an EntityRemovedDelta so other sessions see them leave.
        /// </summary>
        Task LeavePlayerAsync(string sessionId);

        /// <summary>
        /// Removes a player from this map.
        /// </summary>
        Task RemovePlayerAsync(string playerId);

        /// <summary>
        /// Gets all players currently in this map.
        /// </summary>
        Task<List<string>> GetPlayersAsync();

        /// <summary>
        /// Computes a perception snapshot (serialized <c>PerceptionDto</c>) for an
        /// in-world entity from the canonical world — for autonomous agents that
        /// occupy the map as a Character but have no SignalR session. Null if the
        /// map isn't initialized or the entity isn't present.
        /// </summary>
        Task<string?> ComputeAgentPerceptionAsync(string entityId);

        /// <summary>
        /// Processes a game tick for this map (NPC movement, etc.).
        /// </summary>
        /// <param name="gameTimeElapsed">Elapsed game time for this tick</param>
        Task TickAsync(TimeSpan gameTimeElapsed);

        /// <summary>
        /// Saves all regions in this map to persistent storage.
        /// </summary>
        Task SaveMapAsync();

        /// <summary>
        /// Captures the grain's current world state into a <see cref="Aetherium.Server.Persistence.RegionStateSnapshot"/>
        /// keyed by <c>(MapState.WorldId, MapState.MapId)</c>, persists it via
        /// <see cref="Aetherium.Server.Persistence.IWorldSnapshotStore.SaveSnapshotAsync"/>, and compacts the delta log up to
        /// the captured sequence so subsequent cold starts replay a bounded tail. Returns
        /// the sequence the snapshot covers (zero when no snapshot store is wired).
        /// </summary>
        Task<long> ForceSnapshotAsync();

        /// <summary>
        /// Reports persistence health for this map (P3-8): whether the append-only delta log is
        /// keeping up, the cumulative delta-append-failure count, and the last error. Lets
        /// operators and tests observe delta-persistence failures rather than having them
        /// silently swallowed.
        /// </summary>
        Task<Aetherium.Model.PersistenceHealthDto> GetPersistenceHealthAsync();

        /// <summary>
        /// Loads map regions from persistent storage.
        /// </summary>
        Task<bool> LoadMapAsync();

        /// <summary>
        /// Spawns an entity in the world at the specified location.
        /// </summary>
        Task<SpawnEntityResult> SpawnEntityAsync(SpawnEntityRequest request);

        /// <summary>
        /// Builds a structure in the world at the specified location.
        /// </summary>
        Task<BuildStructureResult> BuildStructureAsync(BuildStructureRequest request);
    }

    /// <summary>
    /// Metadata about a game map.
    /// </summary>
    [GenerateSerializer]
    public class MapMetadata
    {
        [Id(0)] public string MapId { get; set; } = string.Empty;
        [Id(1)] public string WorldId { get; set; } = string.Empty;
        [Id(2)] public string MapName { get; set; } = string.Empty;
        [Id(3)] public WorldSize Size { get; set; } = new WorldSize();
        [Id(4)] public string GeneratorType { get; set; } = string.Empty;
        [Id(5)] public int PlayerCount { get; set; }
        [Id(6)] public System.DateTime CreatedAt { get; set; }
    }
}


