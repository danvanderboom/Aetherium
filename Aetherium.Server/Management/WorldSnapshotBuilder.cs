using System;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Model;

namespace Aetherium.Server.Management
{
    /// <summary>
    /// Builds an omniscient <see cref="WorldSnapshotDto"/> from a live ECS <see cref="World"/>.
    /// Reports all entities and tiles with absolute coordinates, independent of any player's
    /// field of view. Caps output to <paramref name="maxEntries"/> and flags truncation rather
    /// than silently dropping content.
    /// </summary>
    public static class WorldSnapshotBuilder
    {
        public const int DefaultMaxEntries = 5000;

        public static WorldSnapshotDto Build(World world, string worldId, string mapId, int maxEntries = DefaultMaxEntries)
        {
            var snapshot = new WorldSnapshotDto
            {
                WorldId = worldId,
                MapId = mapId
            };

            var entities = world.Entities.Values.ToList();
            snapshot.EntityCount = entities.Count;

            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;

            foreach (var entity in entities)
            {
                var loc = entity.Has<WorldLocation>() ? entity.Get<WorldLocation>() : new WorldLocation(0, 0, 0);

                if (loc.X < minX) minX = loc.X;
                if (loc.X > maxX) maxX = loc.X;
                if (loc.Y < minY) minY = loc.Y;
                if (loc.Y > maxY) maxY = loc.Y;
                if (loc.Z < minZ) minZ = loc.Z;
                if (loc.Z > maxZ) maxZ = loc.Z;

                if (snapshot.Entities.Count < maxEntries)
                {
                    snapshot.Entities.Add(new EntitySnapshotDto
                    {
                        EntityId = entity.EntityId,
                        Type = entity.GetType().Name,
                        Location = new WorldLocationDto(loc.X, loc.Y, loc.Z),
                        Components = entity.Components.Keys.Select(t => t.Name).OrderBy(n => n).ToList()
                    });
                }
                else
                {
                    snapshot.Truncated = true;
                }
            }

            snapshot.TileCount = world.EntitiesByLocation.Count;
            foreach (var kvp in world.EntitiesByLocation)
            {
                var location = kvp.Key;
                var terrainName = world.GetTerrain(location)?.Type?.Name;
                if (terrainName == null)
                    continue;

                if (snapshot.Tiles.Count >= maxEntries)
                {
                    snapshot.Truncated = true;
                    break;
                }

                snapshot.Tiles.Add(new TileSnapshotDto
                {
                    Location = new WorldLocationDto(location.X, location.Y, location.Z),
                    Terrain = terrainName
                });
            }

            if (entities.Count > 0)
            {
                snapshot.Width = Math.Max(0, maxX - minX + 1);
                snapshot.Height = Math.Max(0, maxY - minY + 1);
                snapshot.Depth = Math.Max(0, maxZ - minZ + 1);
            }

            return snapshot;
        }
    }
}
