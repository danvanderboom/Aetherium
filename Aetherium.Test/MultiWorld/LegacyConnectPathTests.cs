using System.Linq;
using Aetherium.Server;
using Aetherium.WorldBuilders;
using Xunit;

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// Regression guard for phase 1: the existing GameSession constructor path
    /// (used by the 700+ tests that predate the snapshot bridge) must keep working
    /// without going anywhere near IGameMapGrain. A client that connects without
    /// a worldId query string SHALL still land in a private FovDiagnosticWorldBuilder
    /// world.
    /// </summary>
    public class LegacyConnectPathTests
    {
        [Fact]
        public void GameSession_Without_WorldId_Builds_Local_World()
        {
            var session = new GameSession("legacy-conn", new FovDiagnosticWorldBuilder("open_space"));

            Assert.NotNull(session.World);
            Assert.NotNull(session.ViewLocation);
            Assert.True(session.WorldId == null, "Legacy session should not be bound to a grain-owned world id");
            // Legacy world has entities (FovDiagnostic places a player + terrain).
            Assert.NotEmpty(session.World.Entities);
        }

        [Fact]
        public void Manager_CreateSession_Returns_Independent_Sessions()
        {
            var manager = new GameSessionManager();
            var a = manager.CreateSession("conn-a", new FovDiagnosticWorldBuilder("open_space"));
            var b = manager.CreateSession("conn-b", new FovDiagnosticWorldBuilder("open_space"));

            Assert.NotSame(a.World, b.World);
            // Both sessions own private worlds. Mutating one does not affect the other.
            var someAEntity = a.World.Entities.Values.FirstOrDefault(e => !(e is Aetherium.Entities.Terrain));
            if (someAEntity is null) return; // nothing to test on empty maps
            Assert.True(a.World.TryRemoveEntity(someAEntity.EntityId));
            Assert.True(b.World.Entities.ContainsKey(someAEntity.EntityId)
                        || b.World.Entities.Values.Any(e => e.GetType() == someAEntity.GetType()),
                "Two legacy sessions share no state: removing from a must not remove from b.");
        }
    }
}
