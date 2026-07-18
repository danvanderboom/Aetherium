using Aetherium.Components;
using Aetherium.Topology;
using H3;
using H3.Extensions;
using H3.Model;

namespace Aetherium.WorldGen.Generators.Outdoor
{
    /// <summary>
    /// Small conversion + geometry helpers shared by the sphere-native feature planners (rivers,
    /// settlements, roads). Worldgen speaks <see cref="WorldLocation"/>; the topology speaks
    /// <see cref="GridCoord"/>; H3's own spherical operations speak <see cref="H3Index"/>/<see cref="LatLng"/>.
    /// These keep the three conversions in one place so the planners read cleanly.
    ///
    /// <para><b>Great-circle distance</b> (in radians, body-agnostic) is the honest metric on a sphere and
    /// what we use to space settlements and weight the road graph — H3's packed X/Y are two halves of a
    /// cell index and are meaningless to subtract, and the topology's azimuthal <c>Delta</c> distorts at
    /// continental range. Adjacent cell centres are ~one mean-edge-length apart, so radians double as a
    /// natural "cells apart" scale once divided by the edge length.</para>
    /// </summary>
    internal static class H3SphereGeo
    {
        public static GridCoord ToCoord(WorldLocation l) => new GridCoord(l.X, l.Y, l.Z);

        public static WorldLocation ToLoc(GridCoord c) => new WorldLocation(c.X, c.Y, c.Z);

        public static H3Index ToH3(WorldLocation l) => new H3Index(H3Topology.ToH3(ToCoord(l)));

        public static LatLng Center(WorldLocation l) => ToH3(l).ToLatLng();

        /// <summary>Great-circle distance between two cells' centres, in radians.</summary>
        public static double GreatCircleRadians(WorldLocation a, WorldLocation b)
            => Center(a).GetGreatCircleDistanceInRadians(Center(b));

        /// <summary>Great-circle distance between two already-resolved centres, in radians.</summary>
        public static double GreatCircleRadians(LatLng a, LatLng b)
            => a.GetGreatCircleDistanceInRadians(b);
    }
}
