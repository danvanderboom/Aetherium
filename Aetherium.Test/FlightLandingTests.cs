using System.Collections.Generic;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server.Flying;
using Aetherium;

namespace Aetherium.Test
{
    /// <summary>
    /// Phase 4 of add-flying-entities: land/takeoff. Verifies the state machine, the valid-landing-terrain and
    /// occupancy gates, takeoff-to-cruise, and arrival-triggered landing at the end of a Once plan.
    /// </summary>
    public class FlightLandingTests
    {
        private static World MakeWorld()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            world.LandingTerrainNames = new HashSet<string> { "Indoors" }; // known-registered landing surface for tests
            return world;
        }

        private static Character AddFlyer(World world, WorldLocation at, Flight flight)
        {
            var flyer = new Character();
            flyer.Set(at);
            flyer.Set(flight);
            world.AddEntity(flyer);
            return flyer;
        }

        // A non-terrain obstruction (monorail deck, bridge, building) that a flyer can come to rest on top of.
        private sealed class TestStructure : Entity { }

        private static void AddStructure(World world, WorldLocation at, int height)
        {
            var s = new TestStructure();
            s.Set(at);
            s.Set(new ObstructsMovement { Obstruction = 1, Height = height });
            world.AddEntity(s);
        }

        [Test]
        public void Land_OnValidTerrain_Succeeds_SetsLandedAtSurface()
        {
            var world = MakeWorld();
            world.SetTerrain("Indoors", new WorldLocation(5, 5, 0));
            var flyer = AddFlyer(world, new WorldLocation(5, 5, 3),
                new Flight { CanLand = true, MinBand = 1, MaxBand = 5, CruiseBand = 3, State = FlightState.Airborne });

            Assert.IsTrue(FlightController.TryLand(world, flyer));
            Assert.AreEqual(new WorldLocation(5, 5, 0), flyer.Get<WorldLocation>());
            Assert.AreEqual(FlightState.Landed, flyer.Get<Flight>().State);
        }

        [Test]
        public void Land_WithoutCanLand_Fails()
        {
            var world = MakeWorld();
            world.SetTerrain("Indoors", new WorldLocation(5, 5, 0));
            var flyer = AddFlyer(world, new WorldLocation(5, 5, 3),
                new Flight { CanLand = false, MinBand = 1, MaxBand = 5, State = FlightState.Airborne });

            Assert.IsFalse(FlightController.TryLand(world, flyer));
            Assert.AreEqual(new WorldLocation(5, 5, 3), flyer.Get<WorldLocation>());
            Assert.AreEqual(FlightState.Airborne, flyer.Get<Flight>().State);
        }

        [Test]
        public void Land_OnInvalidTerrain_Fails()
        {
            var world = MakeWorld();
            world.SetTerrain("Mountain", new WorldLocation(5, 5, 0)); // not in LandingTerrainNames
            var flyer = AddFlyer(world, new WorldLocation(5, 5, 3),
                new Flight { CanLand = true, MinBand = 1, MaxBand = 5, State = FlightState.Airborne });

            Assert.IsFalse(FlightController.TryLand(world, flyer));
            Assert.AreEqual(FlightState.Airborne, flyer.Get<Flight>().State);
        }

        [Test]
        public void Land_OnOccupiedCell_Fails()
        {
            var world = MakeWorld();
            world.SetTerrain("Indoors", new WorldLocation(5, 5, 0));

            var blocker = new Character();
            blocker.Set(new WorldLocation(5, 5, 0));
            world.AddEntity(blocker);

            var flyer = AddFlyer(world, new WorldLocation(5, 5, 3),
                new Flight { CanLand = true, MinBand = 1, MaxBand = 5, State = FlightState.Airborne });

            Assert.IsFalse(FlightController.TryLand(world, flyer));
            Assert.AreEqual(FlightState.Airborne, flyer.Get<Flight>().State);
        }

        [Test]
        public void Takeoff_FromLanded_ReturnsToCruiseBand()
        {
            var world = MakeWorld();
            world.SetTerrain("Indoors", new WorldLocation(5, 5, 0));
            var flyer = AddFlyer(world, new WorldLocation(5, 5, 0),
                new Flight { CanLand = true, MinBand = 1, MaxBand = 5, CruiseBand = 3, State = FlightState.Landed });

            Assert.IsTrue(FlightController.TryTakeoff(world, flyer));
            Assert.AreEqual(new WorldLocation(5, 5, 3), flyer.Get<WorldLocation>());
            Assert.AreEqual(FlightState.Airborne, flyer.Get<Flight>().State);
        }

        [Test]
        public void Takeoff_WhenNotLanded_Fails()
        {
            var world = MakeWorld();
            world.SetTerrain("Indoors", new WorldLocation(5, 5, 0));
            var flyer = AddFlyer(world, new WorldLocation(5, 5, 3),
                new Flight { CanLand = true, CruiseBand = 3, State = FlightState.Airborne });

            Assert.IsFalse(FlightController.TryTakeoff(world, flyer));
        }

        [Test]
        public void OncePlan_ArrivalOverValidTerrain_TriggersLanding()
        {
            var world = MakeWorld();
            world.SetTerrain("Indoors", new WorldLocation(3, 0, 0)); // landing surface under the final leg

            var plan = new FlightPlan
            {
                Source = FlightPlanSource.Patterned,
                Legs = new List<WorldLocation> { new(0, 0, 2), new(3, 0, 2) },
                Loop = LoopMode.Once
            };
            var flyer = AddFlyer(world, plan.Legs[0],
                new Flight { CanLand = true, MinBand = 1, MaxBand = 6, CruiseBand = 2, State = FlightState.Airborne });
            flyer.Set(plan);

            for (int i = 0; i < 10; i++)
                FlightPlanSystem.Step(world);

            Assert.AreEqual(FlightState.Landed, flyer.Get<Flight>().State, "The lander sets down on arrival.");
            Assert.AreEqual(new WorldLocation(3, 0, 0), flyer.Get<WorldLocation>(), "It lands on the surface below the final leg.");
        }

        [Test]
        public void Land_OnTerrainPeak_RestsAtTheTop_NotTheBase()
        {
            // A mountain tile sits at band 0 but occupies DefaultWallHeight bands; a flyer that can land on
            // mountains sets down on the peak, not inside the base.
            var world = MakeWorld();
            world.SetTerrain("Mountain", new WorldLocation(5, 5, 0));
            int peak = world.DefaultWallHeight - 1;

            var bird = AddFlyer(world, new WorldLocation(5, 5, 5),
                new Flight { CanLand = true, MinBand = 0, MaxBand = 6, CruiseBand = 4, State = FlightState.Airborne,
                    LandableTerrain = new HashSet<string> { "Mountain" } });

            Assert.IsTrue(FlightController.TryLand(world, bird));
            Assert.AreEqual(new WorldLocation(5, 5, peak), bird.Get<WorldLocation>(), "Lands on the mountain peak.");
            Assert.AreEqual(FlightState.Landed, bird.Get<Flight>().State);
        }

        [Test]
        public void Land_IsGatedByPerFlyerLandableTerrain()
        {
            // Same terrain, two flyers: the one whose LandableTerrain includes it lands; the other is refused.
            var mountain = new WorldLocation(6, 6, 0);

            var w1 = MakeWorld();
            w1.SetTerrain("Mountain", mountain);
            var bird = AddFlyer(w1, new WorldLocation(6, 6, 5),
                new Flight { CanLand = true, MinBand = 0, MaxBand = 6, State = FlightState.Airborne,
                    LandableTerrain = new HashSet<string> { "Mountain", "Forest" } });
            Assert.IsTrue(FlightController.TryLand(w1, bird), "A bird may land on a mountain.");

            var w2 = MakeWorld();
            w2.SetTerrain("Mountain", mountain);
            var plane = AddFlyer(w2, new WorldLocation(6, 6, 5),
                new Flight { CanLand = true, MinBand = 0, MaxBand = 6, State = FlightState.Airborne,
                    LandableTerrain = new HashSet<string> { "Plains", "Road" } });
            Assert.IsFalse(FlightController.TryLand(w2, plane), "A wheeled plane may not land on a mountain.");
            Assert.AreEqual(FlightState.Airborne, plane.Get<Flight>().State);
        }

        [Test]
        public void Land_OnStructureTop_Succeeds_EvenWithoutTerrainThere()
        {
            // A bird lands on top of a monorail deck at band 4 — a structure top is landable by any flyer.
            var world = MakeWorld();
            world.SetTerrain("Indoors", new WorldLocation(7, 7, 0)); // ground floor far below
            AddStructure(world, new WorldLocation(7, 7, 4), height: 1); // monorail deck at band 4

            var bird = AddFlyer(world, new WorldLocation(7, 7, 6),
                new Flight { CanLand = true, MinBand = 1, MaxBand = 6, CruiseBand = 5, State = FlightState.Airborne });

            Assert.IsTrue(FlightController.TryLand(world, bird));
            Assert.AreEqual(new WorldLocation(7, 7, 4), bird.Get<WorldLocation>(), "Rests on the structure top, above the ground floor.");
            Assert.AreEqual(FlightState.Landed, bird.Get<Flight>().State);
        }

        [Test]
        public void Takeoff_FromStructureTop_AscendsAboveIt_NotDownToCruiseBand()
        {
            // Landed on a monorail deck at band 4 with a cruise band of 2 (below the deck): takeoff must rise
            // above the deck, not descend into it.
            var world = MakeWorld();
            AddStructure(world, new WorldLocation(8, 8, 4), height: 1);

            var bird = AddFlyer(world, new WorldLocation(8, 8, 4),
                new Flight { CanLand = true, MinBand = 1, MaxBand = 6, CruiseBand = 2, State = FlightState.Landed });

            Assert.IsTrue(FlightController.TryTakeoff(world, bird));
            Assert.AreEqual(new WorldLocation(8, 8, 5), bird.Get<WorldLocation>(), "Rises to the first clear band above the deck.");
            Assert.AreEqual(FlightState.Airborne, bird.Get<Flight>().State);
        }
    }
}
