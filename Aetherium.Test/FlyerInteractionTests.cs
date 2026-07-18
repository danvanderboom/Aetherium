using System.Linq;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server.Flying;
using Aetherium;

namespace Aetherium.Test
{
    /// <summary>
    /// Phase 5 of add-flying-entities: flyer interaction. Verifies altitude-aware affordances, hack over an
    /// uplink at range (band-agnostic), summon → AdHoc plan followed to the caller, and attack gated by a small
    /// band delta so a grounded player can reach a low drone but not a flyer in a high band.
    /// </summary>
    public class FlyerInteractionTests
    {
        private static World MakeWorld()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        private static Character AddCharacter(World world, WorldLocation at)
        {
            var c = new Character();
            c.Set(at);
            world.AddEntity(c);
            return c;
        }

        private static Character AddFlyer(World world, WorldLocation at, FlyerProfile profile,
            Flight? flight = null, Health? health = null)
        {
            var flyer = new Character();
            flyer.Set(at);
            flyer.Set(profile);
            if (flight != null) flyer.Set(flight);
            if (health != null) flyer.Set(health);
            world.AddEntity(flyer);
            return flyer;
        }

        [Test]
        public void Affordances_Satellite_OffersHack_NotAttack()
        {
            var world = MakeWorld();
            var observer = AddCharacter(world, new WorldLocation(0, 0, 0));
            var satellite = AddFlyer(world, new WorldLocation(2, 0, 6), FlyerProfiles.Satellite());

            var aff = FlyerInteractionSystem.Affordances(world, observer, satellite);

            Assert.IsTrue(aff.Any(a => a.Id == "hack" && a.Available), "A satellite offers a reachable hack.");
            Assert.IsFalse(aff.Any(a => a.Id == "attack"), "A satellite is not attackable, so attack is not offered.");
            Assert.IsTrue(aff.Any(a => a.Id == "inspect"), "Inspect is always offered.");
        }

        [Test]
        public void Hack_WithinUplinkRange_Succeeds_AndMarksHacked()
        {
            var world = MakeWorld();
            var observer = AddCharacter(world, new WorldLocation(0, 0, 0));
            var satellite = AddFlyer(world, new WorldLocation(2, 0, 6), FlyerProfiles.Satellite());

            var outcome = FlyerInteractionSystem.TryHack(world, observer, satellite);

            Assert.IsTrue(outcome.Success, outcome.Reason);
            Assert.IsTrue(satellite.Has<Hacked>(), "A successful hack marks the flyer.");
            Assert.AreEqual(observer.EntityId, satellite.Get<Hacked>().ControllerEntityId);
        }

        [Test]
        public void Hack_OutOfUplinkRange_Fails()
        {
            var world = MakeWorld();
            var observer = AddCharacter(world, new WorldLocation(0, 0, 0));
            // Beyond the satellite's 256-tile uplink range.
            var satellite = AddFlyer(world, new WorldLocation(300, 0, 6), FlyerProfiles.Satellite());

            var outcome = FlyerInteractionSystem.TryHack(world, observer, satellite);

            Assert.IsFalse(outcome.Success);
            Assert.IsFalse(satellite.Has<Hacked>());
        }

        [Test]
        public void Attack_LowDrone_WithinReach_Succeeds_DecrementsHealth()
        {
            var world = MakeWorld();
            var observer = AddCharacter(world, new WorldLocation(0, 0, 0));
            var drone = AddFlyer(world, new WorldLocation(3, 0, 1), FlyerProfiles.Drone(), health: new Health(3, 3));

            var outcome = FlyerInteractionSystem.TryAttack(world, observer, drone);

            Assert.IsTrue(outcome.Success, outcome.Reason);
            Assert.AreEqual(2, drone.Get<Health>().Level, "A hit decrements the target's health.");
        }

        [Test]
        public void Attack_HighFlyer_Fails_TooHighToReach()
        {
            var world = MakeWorld();
            var observer = AddCharacter(world, new WorldLocation(0, 0, 0));
            // Same drone kind, but at a high band beyond the weapon's band reach.
            var drone = AddFlyer(world, new WorldLocation(3, 0, 6), FlyerProfiles.Drone(), health: new Health(3, 3));

            var outcome = FlyerInteractionSystem.TryAttack(world, observer, drone);

            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(3, drone.Get<Health>().Level, "A blocked attack does no damage.");
            StringAssert.Contains("high", outcome.Reason.ToLowerInvariant());
        }

        [Test]
        public void Affordances_RespectAltitude_AttackWithheldWhenHigh_HackRemains()
        {
            var world = MakeWorld();
            var observer = AddCharacter(world, new WorldLocation(0, 0, 0));
            // A drone that is both hackable and attackable, but sitting in a high band.
            var drone = AddFlyer(world, new WorldLocation(3, 0, 6), FlyerProfiles.Drone());

            var aff = FlyerInteractionSystem.Affordances(world, observer, drone);

            Assert.IsTrue(aff.Any(a => a.Id == "hack" && a.Available), "Uplink hack remains reachable at altitude.");
            var attack = aff.FirstOrDefault(a => a.Id == "attack");
            Assert.IsNotNull(attack, "Attack is offered for an attackable flyer.");
            Assert.IsFalse(attack!.Available, "But attack is withheld when the flyer is too high.");
        }

        [Test]
        public void Summon_AirTaxi_IssuesAdHocPlan_FollowedToTheCaller()
        {
            var world = MakeWorld();
            var caller = AddCharacter(world, new WorldLocation(0, 0, 0));
            var taxi = AddFlyer(world, new WorldLocation(10, 0, 3), FlyerProfiles.AirTaxi(),
                flight: new Flight { State = FlightState.Airborne, MinBand = 1, MaxBand = 5, CruiseBand = 3, CanLand = true });

            var outcome = FlyerInteractionSystem.TrySummon(world, caller, taxi);
            Assert.IsTrue(outcome.Success, outcome.Reason);

            var plan = taxi.Get<FlightPlan>();
            Assert.AreEqual(FlightPlanSource.AdHoc, plan.Source, "Summon issues an AdHoc plan.");
            Assert.AreEqual(new WorldLocation(0, 0, 3), plan.Legs[0], "Routed to the caller's column at the approach band.");

            for (int i = 0; i < 20; i++)
                FlightPlanSystem.Step(world);

            var at = taxi.Get<WorldLocation>();
            Assert.AreEqual(0, at.X, "The taxi flew to the caller's column.");
            Assert.AreEqual(0, at.Y);
            Assert.IsTrue(taxi.Get<FlightPlan>().Complete, "The AdHoc route completed over the caller.");
        }
    }
}
