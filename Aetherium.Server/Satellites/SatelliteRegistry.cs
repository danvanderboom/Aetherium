using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Server.Satellites
{
    /// <summary>
    /// A per-world cache of the orbiting entities, so neither the orbit system (each tick) nor the radio
    /// perception channel (each frame it's active) has to scan the terrain-inflated <c>world.Entities</c>
    /// (~288k cells) to find a few dozen satellites. Keyed weakly on the world so it evicts with it.
    /// Satellites are all created at worldgen, before the first tick or frame, so the seeder primes the
    /// cache and nothing needs to invalidate it.
    /// </summary>
    public static class SatelliteRegistry
    {
        private static readonly ConditionalWeakTable<World, List<Entity>> Cache = new();

        /// <summary>Prime the cache at spawn time so no full-world scan is ever needed.</summary>
        public static void Register(World world, IEnumerable<Entity> satellites)
            => Cache.AddOrUpdate(world, satellites.ToList());

        /// <summary>The world's orbiters. Falls back to a one-time scan if the cache wasn't primed.</summary>
        public static IReadOnlyList<Entity> ForWorld(World world)
        {
            if (Cache.TryGetValue(world, out var list))
                return list;
            var scanned = world.Entities.Values.Where(e => e.Has<OrbitPath>()).ToList();
            Cache.AddOrUpdate(world, scanned);
            return scanned;
        }
    }
}
