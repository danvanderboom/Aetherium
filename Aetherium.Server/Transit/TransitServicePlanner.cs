using System.Collections.Generic;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;

namespace Aetherium.Server.Transit
{
    /// <summary>
    /// Places <see cref="Station"/> markers along a transit line at generation time (add-transit-networks
    /// Phase 1). This is the topology-agnostic bridge between line generation (which knows <em>where</em>
    /// the stops are, as <see cref="WorldLocation"/>s) and the runtime service (a
    /// <c>TransitServiceGrain</c>, which drives a train between those docks): the generator carves the line
    /// and calls <see cref="PlaceStations"/> to stamp visible platform markers; a service is then stood up
    /// over the same ordered stops. Markers are non-obstructing, so they co-locate with the settlement
    /// core and never block the train that docks there.
    /// </summary>
    public static class TransitServicePlanner
    {
        /// <summary>
        /// Stamps a <see cref="StationEntity"/> at each stop, in order, tagging it with
        /// <paramref name="lineId"/> and its ordinal. Returns the placed station entities in stop order.
        /// </summary>
        public static IReadOnlyList<StationEntity> PlaceStations(
            World world, string lineId, IReadOnlyList<(WorldLocation Location, string Name)> stops)
        {
            var placed = new List<StationEntity>();
            if (world is null || stops is null)
                return placed;

            for (int i = 0; i < stops.Count; i++)
            {
                var (loc, name) = stops[i];
                var station = new StationEntity();
                station.Set(new WorldLocation(loc.X, loc.Y, loc.Z));
                station.Set(new Station
                {
                    LineId = lineId ?? string.Empty,
                    StopIndex = i,
                    Name = name ?? string.Empty,
                });
                world.AddEntity(station);
                placed.Add(station);
            }

            return placed;
        }
    }
}
