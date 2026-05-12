using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Flattens a live <see cref="World"/> into a serializable <see cref="WorldSnapshot"/>.
    /// Phase 1: ships the recipe plus an <see cref="EntityPlacement"/> for every
    /// non-terrain, non-live-player entity currently in the world. Terrain
    /// regenerates from the recipe on hydration; live <see cref="Character"/>s
    /// (joined players) are excluded — those join dynamically per-session.
    /// </summary>
    public static class WorldSnapshotBuilder
    {
        /// <summary>
        /// Flattens <paramref name="world"/> into a <see cref="WorldSnapshot"/>.
        ///
        /// <para>
        /// Phase 1 behavior: terrain is recipe-driven (skipped here, regenerated on
        /// hydration); all <see cref="Character"/> entities were filtered out so each
        /// joiner saw an empty Character roster.
        /// </para>
        /// <para>
        /// Phase 2 behavior: Character entities ARE included so other joiners see
        /// existing players. The optional <paramref name="excludePlayerEntityId"/>
        /// parameter omits exactly the joining player's own Character to prevent
        /// the duplication that would otherwise occur (the joiner's local
        /// <c>GameSession</c> creates a fresh Player on hydration).
        /// </para>
        /// </summary>
        public static WorldSnapshot SnapshotOf(
            World world,
            WorldRecipe recipe,
            string worldId,
            string mapId,
            WorldSize size,
            string? excludePlayerEntityId = null)
        {
            var snapshot = new WorldSnapshot
            {
                WorldId = worldId,
                MapId = mapId,
                Size = size,
                Recipe = recipe,
                SnapshotVersion = 1,
            };

            // Filter: terrain is recipe-driven; the joining player's own Character
            // is excluded so they don't see themselves twice. Everything else —
            // including other player Characters, monsters, doors, items — ships.
            var entities = world.Entities.Values
                .Where(e => e is not Terrain)
                .Where(e => excludePlayerEntityId is null || e.EntityId != excludePlayerEntityId);

            foreach (var entity in entities)
            {
                var location = entity.Get<WorldLocation>();
                if (location is null)
                    continue;

                var placement = EntityPlacement.FromLocation(
                    entity.EntityId,
                    entity.GetType().Name,
                    location);

                EntityFactory.ExtractProperties(entity, placement);

                snapshot.Entities.Add(placement);
            }

            return snapshot;
        }
    }
}
