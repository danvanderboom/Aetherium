using System.Drawing;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;
using Aetherium.Server;
using Aetherium.Model;
using Aetherium;

namespace Aetherium.Test
{
    /// <summary>
    /// Section 5.3 of add-adaptive-depth-visualization: the altitude-gauge data. The server surfaces the
    /// perceiver's flight envelope ([MinBand, MaxBand] + current band + state) on the (relative-coordinate)
    /// PerceptionDto only when they carry a Flight component. Additive and non-breaking — null for non-flyers.
    /// </summary>
    public class PerceptionFlightEnvelopeTests
    {
        private static World MakeWorld()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        private static Character MakeFlyer(World world, WorldLocation at, int min, int max, FlightState state)
        {
            var player = new Character();
            player.Set(at);
            player.Set(new Inventory()); // PerceptionService reads Inventory; real players always have one
            player.Set(new Flight { MinBand = min, MaxBand = max, State = state });
            world.AddEntity(player);
            return player;
        }

        private static PerceptionDto Perceive(World world, Entity? self, WorldLocation at) =>
            new PerceptionService().ComputePerception(world, at, WorldDirection.North, new Size(42, 22), self: self);

        [Test]
        public void FlightEnvelope_PopulatedForFlyingPlayer()
        {
            var world = MakeWorld();
            var at = new WorldLocation(20, 20, 3);
            var player = MakeFlyer(world, at, min: 1, max: 5, state: FlightState.Airborne);

            var perception = Perceive(world, player, at);

            Assert.IsNotNull(perception.FlightEnvelope, "A flyer should carry a flight envelope");
            Assert.AreEqual(1, perception.FlightEnvelope!.MinBand);
            Assert.AreEqual(5, perception.FlightEnvelope.MaxBand);
            Assert.AreEqual(3, perception.FlightEnvelope.CurrentBand, "Current band is the real Z, not the relative 0");
            Assert.AreEqual("Airborne", perception.FlightEnvelope.State);
        }

        [Test]
        public void FlightEnvelope_CurrentBandTracksZ()
        {
            var world = MakeWorld();
            var at = new WorldLocation(20, 20, -2);
            var player = MakeFlyer(world, at, min: -4, max: 2, state: FlightState.Airborne);

            var perception = Perceive(world, player, at);

            Assert.AreEqual(-2, perception.FlightEnvelope!.CurrentBand);
        }

        [Test]
        public void FlightEnvelope_ReflectsState()
        {
            var world = MakeWorld();
            var at = new WorldLocation(20, 20, 0);
            var player = MakeFlyer(world, at, min: 0, max: 4, state: FlightState.Landed);

            var perception = Perceive(world, player, at);

            Assert.AreEqual("Landed", perception.FlightEnvelope!.State);
        }

        [Test]
        public void FlightEnvelope_NullForNonFlyer()
        {
            var world = MakeWorld();
            var at = new WorldLocation(20, 20, 0);
            var player = new Character();
            player.Set(at);
            player.Set(new Inventory());
            world.AddEntity(player);

            var perception = Perceive(world, player, at);

            Assert.IsNull(perception.FlightEnvelope, "A player with no Flight component gets no gauge");
        }

        [Test]
        public void FlightEnvelope_NullWhenNoSelfSupplied()
        {
            var world = MakeWorld();
            var at = new WorldLocation(20, 20, 0);

            var perception = Perceive(world, self: null, at: at);

            Assert.IsNull(perception.FlightEnvelope, "Without the perceiving entity there is no envelope to report");
        }
    }
}
