using System.Linq;
using System.Threading.Tasks;
using Aetherium.Components;
using Aetherium.Entities;
using Aetherium.Model;
using Aetherium.Server;
using Aetherium.Server.MultiWorld;
using Aetherium.WorldBuilders;
using Xunit;

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// LocalMutationGateway is the phase-2a shim that lets tools mutate through an
    /// abstraction without changing behaviour. These tests verify that each gateway
    /// method produces the same observable outcome as the pre-2a direct paths.
    /// </summary>
    public class LocalMutationGatewayTests
    {
        private static GameSession NewSession() =>
            new GameSession("gw-test", new FovDiagnosticWorldBuilder("open_space"));

        [Fact]
        public async Task MoveAsync_Updates_ViewLocation_Same_As_MoveView()
        {
            var session = NewSession();
            var beforeY = session.ViewLocation!.Y;

            var gateway = new LocalMutationGateway(session);
            var result = await gateway.MoveAsync(Aetherium.Model.RelativeDirection.Forward, 1);

            Assert.True(result.Success);
            // Forward when facing North reduces Y by 1 (canonical convention).
            Assert.Equal(beforeY - 1, session.ViewLocation!.Y);
        }

        [Fact]
        public async Task RotateAsync_Updates_HeadingDegrees()
        {
            var session = NewSession();
            session.HeadingDegrees = 0;

            var gateway = new LocalMutationGateway(session);
            var result = await gateway.RotateAsync(90);

            Assert.True(result.Success);
            Assert.Equal(90, result.HeadingDegrees);
            Assert.Equal(90, session.HeadingDegrees);
        }

        [Fact]
        public async Task ChangeLevelAsync_Updates_ViewLocation_Z()
        {
            var session = NewSession();
            var beforeZ = session.ViewLocation!.Z;

            var gateway = new LocalMutationGateway(session);
            var result = await gateway.ChangeLevelAsync(1);

            // ChangeLevel may not actually succeed if the destination has no
            // terrain entry — the legacy method itself doesn't validate that.
            // Result reflects the post-call ViewLocation.Z value.
            Assert.True(result.Success);
            Assert.Equal(beforeZ + 1, session.ViewLocation!.Z);
        }

        [Fact]
        public async Task MoveAsync_Without_ViewLocation_Fails_Cleanly()
        {
            var session = NewSession();
            session.ViewLocation = null!;

            var gateway = new LocalMutationGateway(session);
            var result = await gateway.MoveAsync(Aetherium.Model.RelativeDirection.Forward, 1);

            Assert.False(result.Success);
            Assert.NotNull(result.Reason);
        }

        [Fact]
        public async Task PickupAsync_Delegates_To_InteractionSystem()
        {
            // Build a session, drop a carriable item at the player's location, pickup via gateway.
            var session = NewSession();
            var item = new KeyItem("test-key");
            item.Set(new WorldLocation(session.ViewLocation!.X, session.ViewLocation.Y, session.ViewLocation.Z));
            session.World.AddEntity(item);

            var gateway = new LocalMutationGateway(session);
            var result = await gateway.PickupAsync(item.EntityId);

            Assert.True(result.Success);
            // Entity removed from world, present in inventory.
            Assert.False(session.World.Entities.ContainsKey(item.EntityId));
            var inv = session.Player!.Get<Inventory>();
            Assert.True(inv!.Items.ContainsKey(item.EntityId));
        }

        [Fact]
        public async Task PickupAsync_Of_Unknown_Entity_Returns_Failure_With_Reason()
        {
            var session = NewSession();
            var gateway = new LocalMutationGateway(session);

            var result = await gateway.PickupAsync("does-not-exist");

            Assert.False(result.Success);
            Assert.NotNull(result.Reason);
        }

        [Fact]
        public async Task UseAsync_Returns_DTO_With_Reason_On_Failure()
        {
            // Phase-2a is a pure shim: when InteractionSystem returns a failure
            // (e.g. unknown item), the gateway DTO carries the failure reason
            // verbatim. We don't try to engineer a multi-option disambiguation
            // case here — that's behavioural coverage owned by InteractionSystemTests.
            var session = NewSession();
            var gateway = new LocalMutationGateway(session);

            var result = await gateway.UseAsync("nonexistent-item", "nonexistent-target", usageId: null);

            Assert.False(result.Success);
            Assert.False(string.IsNullOrEmpty(result.Reason));
        }

        [Fact]
        public async Task DropAsync_Round_Trips_Inventory_To_World()
        {
            var session = NewSession();
            var item = new KeyItem("drop-test");
            session.Player!.Get<Inventory>()!.TryAdd(item.EntityId, item);

            var gateway = new LocalMutationGateway(session);
            var result = await gateway.DropAsync(item.EntityId);

            Assert.True(result.Success);
            Assert.False(session.Player.Get<Inventory>()!.Items.ContainsKey(item.EntityId));
            Assert.True(session.World.Entities.ContainsKey(item.EntityId));
        }
    }
}
