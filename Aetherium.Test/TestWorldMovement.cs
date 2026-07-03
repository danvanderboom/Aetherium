using Aetherium.Components;
using Aetherium.Server;

namespace Aetherium.Test
{
    /// <summary>
    /// Helpers for movement-dependent tests. Since P0-1 (validated server-side
    /// movement), moves are checked against walls, closed doors, occupancy, map
    /// bounds, and stair placement — so tests whose subject is tool plumbing or
    /// session state (not world geometry) must guarantee open terrain around the
    /// player instead of relying on a maze world happening to have a clear path.
    /// </summary>
    internal static class TestWorldMovement
    {
        /// <summary>
        /// Carves passable terrain in a square of the given radius around the
        /// player so short moves in any direction succeed.
        /// </summary>
        public static void CarveOpenArea(GameSession session, int radius = 2)
        {
            var loc = session.Player!.Get<WorldLocation>()!;
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                    session.World.SetTerrain("Indoors", new WorldLocation(loc.X + dx, loc.Y + dy, loc.Z));
        }

        /// <summary>
        /// Puts stair terrain under the player's CURRENT location (and along the
        /// column for multi-level deltas) plus an open landing, so a ChangeLevel
        /// by <paramref name="deltaZ"/> is valid. Call it after any moves, at the
        /// position the level change will happen from.
        /// </summary>
        public static void CarveStairsAtPlayer(GameSession session, int deltaZ)
        {
            var loc = session.Player!.Get<WorldLocation>()!;
            int step = System.Math.Sign(deltaZ);
            for (int i = 0; i < System.Math.Abs(deltaZ); i++)
                session.World.SetTerrain("Upstairs", new WorldLocation(loc.X, loc.Y, loc.Z + i * step));
            session.World.SetTerrain("Indoors", new WorldLocation(loc.X, loc.Y, loc.Z + deltaZ));
        }
    }
}
