using System.Collections.Generic;
using System.Collections.Concurrent;
using Aetherium.Core;

namespace Aetherium.Server.Services
{
    /// <summary>
    /// In-process bridge that exposes the live ECS <see cref="World"/> generated inside
    /// <c>GameMapGrain</c> to services that must read or drive it directly — headless session
    /// provisioning and world snapshots (see <c>IGameManagementGrain.CreateHeadlessSessionAsync</c>
    /// and <c>GetWorldSnapshotAsync</c>).
    ///
    /// This is a single-process/single-silo construct: it holds references to World objects that
    /// live in the same process as the grains. The operator/debug tooling it backs runs over the
    /// localhost Orleans path, so this assumption holds for its intended use.
    /// </summary>
    public class WorldRegistry
    {
        private readonly ConcurrentDictionary<string, World> _byMapId = new();
        private readonly ConcurrentDictionary<string, string> _worldToPrimaryMap = new();

        /// <summary>
        /// Publishes a map's live world. The first map registered for a world becomes its primary map.
        /// </summary>
        public void Register(string worldId, string mapId, World world)
        {
            _byMapId[mapId] = world;
            _worldToPrimaryMap.TryAdd(worldId, mapId);
        }

        public void Unregister(string worldId, string mapId)
        {
            _byMapId.TryRemove(mapId, out _);
            _worldToPrimaryMap.TryRemove(new KeyValuePair<string, string>(worldId, mapId));
        }

        public World? GetByMapId(string mapId) =>
            _byMapId.TryGetValue(mapId, out var world) ? world : null;

        public World? GetByWorldId(string worldId) =>
            _worldToPrimaryMap.TryGetValue(worldId, out var mapId) ? GetByMapId(mapId) : null;

        public string? GetPrimaryMapId(string worldId) =>
            _worldToPrimaryMap.TryGetValue(worldId, out var mapId) ? mapId : null;

        /// <summary>Resolves a world by worldId first, then falls back to treating the key as a mapId.</summary>
        public World? Resolve(string idOrMapId) =>
            GetByWorldId(idOrMapId) ?? GetByMapId(idOrMapId);
    }
}
