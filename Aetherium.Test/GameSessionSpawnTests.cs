using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server;

namespace Aetherium.Test
{
    /// <summary>
    /// Regression guard for the headless / auto-spawn path in <see cref="GameSession"/>.
    ///
    /// A <see cref="GameSession"/> constructed with a null start location must place the player on a
    /// real passable cell chosen from the world, not on the hardcoded (15,15,0) fallback. The bug this
    /// pins down: <see cref="WorldLocation"/> is a class that overloads <c>operator==</c> so that a null
    /// operand always compares unequal (its Equals returns false when either side is null). That made
    /// <c>startLocation == null</c> evaluate false even when startLocation was null, so InitializePlayer
    /// skipped spawn selection and dropped every auto-spawned player onto (15,15,0). It looked fine on
    /// square maps (where (15,15,0) is a valid cell near the origin) but spawned off-map on H3/spherical
    /// worlds whose cells use large packed coordinates.
    /// </summary>
    [TestFixture]
    public class GameSessionSpawnTests
    {
        // A cell far from the origin, mimicking H3's large packed coordinates. (15,15,0) — the old
        // hardcoded fallback — is deliberately NOT a key in this world.
        private static readonly WorldLocation FarPassableCell = new WorldLocation(136_389_375, -1, 0);

        private static World BuildWorldWithSingleFarPassableCell()
        {
            var world = new World();
            world.AddTerrainTypes(new[]
            {
                new TerrainType { Name = "Ground", IsPassable = true },
            });
            world.SetTerrain("Ground", FarPassableCell);
            return world;
        }

        [Test]
        public void HeadlessAutoSpawn_PlacesPlayerOnPassableCell_NotHardcodedFallback()
        {
            var world = BuildWorldWithSingleFarPassableCell();

            // Multi-world ctor with a null start location => auto-spawn via SelectRandomPassableLocation.
            var session = new GameSession("headless:test", "world:test", world, startLocation: null);

            Assert.That(session.Player, Is.Not.Null, "auto-spawn must create the player");
            Assert.That(session.ViewLocation, Is.Not.Null);

            var loc = session.Player!.Get<WorldLocation>();
            Assert.That((loc.X, loc.Y, loc.Z), Is.EqualTo((FarPassableCell.X, FarPassableCell.Y, FarPassableCell.Z)),
                "player must spawn on the world's only passable cell, not the (15,15,0) fallback");
            Assert.That((loc.X, loc.Y, loc.Z), Is.Not.EqualTo((15, 15, 0)),
                "the (15,15,0) fallback is off-map on non-square topologies and must not be used when a passable cell exists");
        }

        [Test]
        public void ExplicitStartLocation_IsHonored()
        {
            var world = BuildWorldWithSingleFarPassableCell();
            var explicitStart = new WorldLocation(136_389_375, -1, 0);

            var session = new GameSession("headless:test2", "world:test", world, startLocation: explicitStart);

            Assert.That(session.Player, Is.Not.Null);
            var loc = session.Player!.Get<WorldLocation>();
            Assert.That((loc.X, loc.Y, loc.Z), Is.EqualTo((explicitStart.X, explicitStart.Y, explicitStart.Z)));
        }

        // Documents the operator quirk that motivated the `is null` fix: null == null is FALSE for
        // WorldLocation. If this ever changes, revisit the `is null` guards in GameSession.InitializePlayer.
        [Test]
        public void WorldLocation_NullEqualsNull_IsFalse_ByDesign()
        {
            WorldLocation? a = null;
            WorldLocation? b = null;
            Assert.That(a == b, Is.False,
                "WorldLocation.operator== returns false when either operand is null; InitializePlayer must use `is null`");
            Assert.That(a is null, Is.True, "the `is null` pattern still correctly detects null");
        }
    }
}
