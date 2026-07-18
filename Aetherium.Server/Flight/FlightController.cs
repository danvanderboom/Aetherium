using System;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Server.Flying
{
    /// <summary>
    /// Land/takeoff state machine shared by the <c>land</c>/<c>takeoff</c> tools and the flight-plan follower.
    /// Landing lowers an airborne flyer onto the top of the next obstruction below it — a structure top, a
    /// terrain peak, or the ground floor — coming to rest co-located with that band. Takeoff raises a landed
    /// flyer back up above whatever it was resting on, into clear air.
    /// </summary>
    public static class FlightController
    {
        /// <summary>
        /// Land the flyer on the top of the next obstruction below its column. Requires <see cref="Flight.CanLand"/>
        /// and the Airborne state. A terrain surface must be landable for this flyer (its
        /// <see cref="Flight.LandableTerrain"/> set, else the world default); a structure top is landable by any
        /// landing-capable flyer. The resting cell must be unoccupied by another character.
        /// </summary>
        public static bool TryLand(World world, Character flyer)
        {
            if (world == null || flyer == null || !flyer.Has<Flight>())
                return false;

            var flight = flyer.Get<Flight>();
            if (!flight.CanLand || flight.State != FlightState.Airborne)
                return false;

            var cur = flyer.Get<WorldLocation>();
            var surface = world.SurfaceBelow(cur.X, cur.Y, cur.Z);
            if (surface is null) // open sky below — nothing to come to rest on
                return false;

            var s = surface.Value;

            // A terrain surface is gated by what this flyer can set down on; a structure top is always landable.
            if (s.IsTerrain && !world.CanLandOn(flight, s.Terrain))
                return false;

            // The resting cell must not be occupied by another character.
            if (world.Characters.Values.Any(c => c.EntityId != flyer.EntityId && c.Get<WorldLocation>() == s.Cell))
                return false;

            flight.State = FlightState.Landing;
            world.MoveEntity(flyer.EntityId, s.Cell);
            flight.State = FlightState.Landed;
            return true;
        }

        /// <summary>
        /// Take off from a landed cell, ascending above whatever the flyer was resting on into clear air. The
        /// target is the cruise band when it lies above the resting surface, otherwise the first clear band just
        /// above it; either way it is clamped to <c>[MinBand, MaxBand]</c>. Fails if the flyer cannot ascend
        /// clear of the surface within its ceiling.
        /// </summary>
        public static bool TryTakeoff(World world, Character flyer)
        {
            if (world == null || flyer == null || !flyer.Has<Flight>())
                return false;

            var flight = flyer.Get<Flight>();
            if (flight.State != FlightState.Landed)
                return false;

            var cur = flyer.Get<WorldLocation>();

            // Prefer the cruise band, but never below one band above the surface we're resting on.
            int target = Math.Clamp(Math.Max(flight.CruiseBand, cur.Z + 1), flight.MinBand, flight.MaxBand);
            if (target <= cur.Z)
                return false; // the surface is at/above this flyer's ceiling — it cannot clear it

            if (world.ColumnObstructsMovement(cur.X, cur.Y, target))
            {
                // The preferred band is blocked; climb to the lowest clear band above the surface, within the ceiling.
                target = -1;
                for (int z = cur.Z + 1; z <= flight.MaxBand; z++)
                {
                    if (!world.ColumnObstructsMovement(cur.X, cur.Y, z))
                    {
                        target = z;
                        break;
                    }
                }
                if (target < 0)
                    return false;
            }

            flight.State = FlightState.TakingOff;
            world.MoveEntity(flyer.EntityId, new WorldLocation(cur.X, cur.Y, target));
            flight.State = FlightState.Airborne;
            return true;
        }
    }
}
