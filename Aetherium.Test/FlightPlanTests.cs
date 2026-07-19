using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server.Flying;
using Aetherium;

namespace Aetherium.Test
{
    /// <summary>
    /// Phase 3 of add-flying-entities: the tick-driven flight-plan follower, pattern generators, loop modes,
    /// the semicircular cruise rule, and the collision policy.
    /// </summary>
    public class FlightPlanTests
    {
        private static World MakeWorld()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        private static Character AddFlyer(World world, WorldLocation at, FlightPlan plan, int minBand = 0, int maxBand = 6)
        {
            var flyer = new Character();
            flyer.Set(at);
            flyer.Set(new Flight { MinBand = minBand, MaxBand = maxBand, CruiseBand = at.Z, State = FlightState.Airborne });
            flyer.Set(plan);
            world.AddEntity(flyer);
            return flyer;
        }

        private static List<WorldLocation> RunFollower(World world, Character flyer, int ticks)
        {
            var visited = new List<WorldLocation> { flyer.Get<WorldLocation>() };
            for (int i = 0; i < ticks; i++)
            {
                FlightPlanSystem.Step(world);
                visited.Add(flyer.Get<WorldLocation>());
            }
            return visited;
        }

        [Test]
        public void Orbit_Follower_TracesRing_AtConstantBand()
        {
            var world = MakeWorld();
            var center = new WorldLocation(10, 10, 3);
            var plan = FlightPlanGenerator.OrbitPlan(center, radius: 2, band: 3);
            var flyer = AddFlyer(world, plan.Legs[0], plan);

            var visited = RunFollower(world, flyer, 24);

            // Visits every corner of the ring...
            foreach (var corner in plan.Legs)
                Assert.IsTrue(visited.Any(l => l == corner), $"Orbit should visit corner {corner}.");
            // ...and never leaves the band.
            Assert.IsTrue(visited.All(l => l.Z == 3), "Orbit stays at its band.");
        }

        [Test]
        public void Patrol_PingPong_ReachesEndThenReturns()
        {
            var world = MakeWorld();
            var anchors = new List<WorldLocation> { new(0, 0, 2), new(3, 0, 2), new(6, 0, 2) };
            var plan = FlightPlanGenerator.PatrolPlan(anchors, LoopMode.PingPong);
            var flyer = AddFlyer(world, anchors[0], plan);

            var visited = RunFollower(world, flyer, 20);

            var end = new WorldLocation(6, 0, 2);
            var start = new WorldLocation(0, 0, 2);
            int firstEnd = visited.FindIndex(l => l == end);
            int lastStart = visited.FindLastIndex(l => l == start);

            Assert.Greater(firstEnd, 0, "Patrol should reach the far anchor.");
            Assert.Greater(lastStart, firstEnd, "PingPong should return toward the start after reaching the end.");
        }

        [Test]
        public void Hover_StaysInPlace()
        {
            var world = MakeWorld();
            var cell = new WorldLocation(5, 5, 3);
            var flyer = AddFlyer(world, cell, FlightPlanGenerator.HoverPlan(cell));

            RunFollower(world, flyer, 10);

            Assert.AreEqual(cell, flyer.Get<WorldLocation>(), "A hovering flyer holds its cell.");
        }

        [Test]
        public void Wander_StaysWithinRadiusAndBand_AndMoves()
        {
            var world = MakeWorld();
            var home = new WorldLocation(10, 10, 3);
            var flyer = AddFlyer(world, home, FlightPlanGenerator.WanderPlan(home, radius: 2));

            var visited = RunFollower(world, flyer, 30);

            Assert.IsTrue(visited.All(l => System.Math.Abs(l.X - home.X) <= 2 && System.Math.Abs(l.Y - home.Y) <= 2),
                "A wanderer stays within its radius of home.");
            Assert.IsTrue(visited.All(l => l.Z == 3), "A wanderer stays at its band.");
            Assert.Greater(visited.Distinct().Count(), 1, "A wanderer actually moves.");
        }

        [Test]
        public void Manual_Plan_IsNotAdvancedByFollower()
        {
            var world = MakeWorld();
            var at = new WorldLocation(5, 5, 3);
            var manual = new FlightPlan { Source = FlightPlanSource.Manual, Legs = new List<WorldLocation> { new(9, 5, 3) } };
            var flyer = AddFlyer(world, at, manual);

            RunFollower(world, flyer, 10);

            Assert.AreEqual(at, flyer.Get<WorldLocation>(), "Manual plans are driven externally, not by the follower.");
        }

        [Test]
        public void Once_Plan_CompletesAtLastLeg()
        {
            var world = MakeWorld();
            var plan = new FlightPlan
            {
                Source = FlightPlanSource.Patterned,
                Legs = new List<WorldLocation> { new(0, 0, 2), new(3, 0, 2) },
                Loop = LoopMode.Once
            };
            var flyer = AddFlyer(world, plan.Legs[0], plan);

            RunFollower(world, flyer, 10);

            Assert.AreEqual(new WorldLocation(3, 0, 2), flyer.Get<WorldLocation>(), "A Once plan ends at its final leg.");
            Assert.IsTrue(plan.Complete, "A Once plan marks itself complete.");
        }

        [Test]
        public void CruiseRule_SeparatesOpposingTraffic_ByBand()
        {
            var rule = new CruiseRule();

            var eastband = rule.BandForHeading(1, 0);
            var westband = rule.BandForHeading(-1, 0);

            Assert.IsNotNull(eastband);
            Assert.IsNotNull(westband);
            Assert.AreNotEqual(eastband, westband, "Opposing headings cruise at different bands.");
            Assert.IsNull(rule.BandForHeading(0, 0), "No horizontal movement yields no cruise band.");
        }

        [Test]
        public void PatrolWithCruiseBands_RebandsLegsByHeading()
        {
            var rule = new CruiseRule(); // eastbound -> 2
            var anchors = new List<WorldLocation> { new(0, 0, 0), new(5, 0, 0) };

            var plan = FlightPlanGenerator.PatrolPlanWithCruiseBands(anchors, rule);

            Assert.AreEqual(rule.EastboundBands[0], plan.Legs[1].Z,
                "An eastbound leg is placed on an eastbound cruise band.");
        }

        [Test]
        public void CollisionPolicy_Collidable_EmitsEvent_SeparatedDoesNot()
        {
            foreach (var (policy, expectEvent) in new[] { (CollisionPolicy.Collidable, true), (CollisionPolicy.Separated, false) })
            {
                var world = MakeWorld();
                world.CollisionPolicy = policy;
                world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
                world.SetTerrain("Indoors", new WorldLocation(1, 0, 0));

                var a = new Character();
                a.Set(new WorldLocation(0, 0, 0));
                a.Set(new Health { Level = 10, MaxLevel = 10 });
                world.AddEntity(a);

                var b = new Character();
                b.Set(new WorldLocation(1, 0, 0));
                world.AddEntity(b);

                bool collided = false;
                world.WorldEvents += e => { if (e.EventType == WorldEventType.Collision) collided = true; };

                world.TryMove(a, new WorldLocation(1, 0, 0)); // into b

                Assert.AreEqual(expectEvent, collided, $"Policy {policy} collision-event expectation.");
            }
        }
    }
}
