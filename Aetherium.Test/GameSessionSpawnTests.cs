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
    /// originally pinned down: <see cref="WorldLocation"/> is a class that overloaded <c>operator==</c>
    /// so that a null operand always compared unequal (its Equals returned false when either side was
    /// null). That made <c>startLocation == null</c> evaluate false even when startLocation was null, so
    /// InitializePlayer skipped spawn selection and dropped every auto-spawned player onto (15,15,0). It
    /// looked fine on square maps (where (15,15,0) is a valid cell near the origin) but spawned off-map on
    /// H3/spherical worlds whose cells use large packed coordinates. The operator== null bug is now fixed
    /// centrally (see ComponentTests.WorldLocation_*), so InitializePlayer's <c>is null</c> guards are
    /// belt-and-suspenders rather than load-bearing — this fixture still guards the end-to-end spawn path.
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

        // The WorldLocation operator== null bug is now fixed centrally: null == null is TRUE, and
        // `x == null` correctly detects a null operand. GameSession.InitializePlayer's `is null` guards
        // remain valid (robust regardless of the operator), so no change is needed there.
        [Test]
        public void WorldLocation_NullEqualsNull_IsTrue()
        {
            WorldLocation? a = null;
            WorldLocation? b = null;
            Assert.That(a == b, Is.True,
                "WorldLocation.operator== now returns true when both operands are null");
            Assert.That(a != b, Is.False);
            Assert.That(a is null, Is.True, "the `is null` pattern also correctly detects null");
        }
    }
}
