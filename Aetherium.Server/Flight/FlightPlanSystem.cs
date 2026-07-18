using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Server.Flying
{
    /// <summary>
    /// Tick-driven follower: advances every airborne flyer that carries a non-Manual <see cref="FlightPlan"/>
    /// by one tile toward its current leg (or one wander step). Called once per map tick. Iterates
    /// <c>world.Characters</c> — never the terrain-inflated <c>world.Entities</c>.
    /// </summary>
    public static class FlightPlanSystem
    {
        public static void Step(World world)
        {
            if (world == null)
                return;

            foreach (var flyer in world.Characters.Values.ToList())
            {
                if (!flyer.Has<FlightPlan>() || !flyer.Has<Flight>())
                    continue;

                var plan = flyer.Get<FlightPlan>();
                if (plan.Source == FlightPlanSource.Manual)
                    continue; // driven directly by a controller

                StepOne(world, flyer, plan);
            }
        }

        private static void StepOne(World world, Character flyer, FlightPlan plan)
        {
            if (plan.PatternId == "wander")
            {
                StepWander(world, flyer, plan);
                return;
            }

            if (plan.Legs.Count == 0 || plan.Complete)
                return;

            var cur = flyer.Get<WorldLocation>();

            // On arrival at the current leg, advance to the next per the loop mode.
            if (cur == plan.Legs[plan.Cursor])
            {
                AdvanceCursor(plan);
                if (plan.Complete)
                {
                    // Arrival behavior: a lander sets down when its route ends over valid terrain.
                    var flight = flyer.Get<Flight>();
                    if (flight.CanLand && flight.State == FlightState.Airborne)
                        FlightController.TryLand(world, flyer);
                    return;
                }
            }

            StepToward(world, flyer, plan.Legs[plan.Cursor]);
        }

        private static void AdvanceCursor(FlightPlan plan)
        {
            switch (plan.Loop)
            {
                case LoopMode.Once:
                    if (plan.Cursor >= plan.Legs.Count - 1)
                        plan.Complete = true;
                    else
                        plan.Cursor++;
                    break;

                case LoopMode.Loop:
                    plan.Cursor = (plan.Cursor + 1) % plan.Legs.Count;
                    break;

                case LoopMode.PingPong:
                    if (plan.Legs.Count == 1)
                        break;
                    var next = plan.Cursor + plan.Direction;
                    if (next < 0 || next >= plan.Legs.Count)
                    {
                        plan.Direction = -plan.Direction;
                        next = plan.Cursor + plan.Direction;
                    }
                    plan.Cursor = Math.Clamp(next, 0, plan.Legs.Count - 1);
                    break;
            }
        }

        private static void StepToward(World world, Character flyer, WorldLocation target)
        {
            var cur = flyer.Get<WorldLocation>();
            int sx = Math.Sign(target.X - cur.X);
            int sy = Math.Sign(target.Y - cur.Y);
            int sz = Math.Sign(target.Z - cur.Z);
            if (sx == 0 && sy == 0 && sz == 0)
                return;

            world.TryMove(flyer, new WorldLocation(cur.X + sx, cur.Y + sy, cur.Z + sz));
        }

        private static void StepWander(World world, Character flyer, FlightPlan plan)
        {
            var cur = flyer.Get<WorldLocation>();
            var home = plan.Home ?? cur;

            var options = new List<WorldLocation>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    var cand = new WorldLocation(cur.X + dx, cur.Y + dy, cur.Z);
                    if (Math.Abs(cand.X - home.X) > plan.WanderRadius || Math.Abs(cand.Y - home.Y) > plan.WanderRadius)
                        continue;
                    if (world.IsPassable(cand, flyer))
                        options.Add(cand);
                }
            }

            if (options.Count == 0)
                return;

            world.TryMove(flyer, options[world.NextRandom(options.Count)]);
        }
    }
}
