using System.Collections.Concurrent;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Server.Satellites
{
    /// <summary>
    /// Advances every orbiter one cell around its <see cref="OrbitPath"/> when its per-orbiter tick counter
    /// comes due, looping forever. Slotted into the map tick (<c>GameMapGrain.TickAsync</c>). Topology-free:
    /// the ring was precomputed at spawn, so a step is just "move to the next ring cell". A fast no-op on any
    /// world with no orbiters. Because each satellite rides its own band, moving them never collides.
    /// </summary>
    public sealed class SatelliteSystem
    {
        public void Step(World world)
        {
            var sats = SatelliteRegistry.ForWorld(world);
            if (sats.Count == 0) return;

            foreach (var sat in sats)
            {
                if (!sat.Has<OrbitPath>()) continue;
                var orbit = sat.Get<OrbitPath>();
                if (orbit.Ring.Count < 2) continue;

                if (++orbit.TickAccum < orbit.TicksPerStep) continue;
                orbit.TickAccum = 0;
                orbit.Cursor = (orbit.Cursor + 1) % orbit.Ring.Count;
                MoveTo(world, sat, orbit.Ring[orbit.Cursor]);
            }
        }

        // Reposition a non-Character entity: pull it from its old location bucket, restamp its
        // WorldLocation, drop it in the new bucket. (world.TryMove is Character-only.)
        private static void MoveTo(World world, Entity e, WorldLocation to)
        {
            var from = e.Get<WorldLocation>();
            if (from.Equals(to)) return;

            if (world.EntitiesByLocation.TryGetValue(from, out var fromBucket))
                fromBucket.TryRemove(e.EntityId, out _);

            e.Set(to);
            var toBucket = world.EntitiesByLocation.GetOrAdd(to, _ => new ConcurrentDictionary<string, Entity>());
            toBucket[e.EntityId] = e;
        }
    }
}
