using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium
{
    public class Monster : Character
    {
        WorldDirection? PreviousDirection;

        World world;

        Random rand;

        public Monster(World world) : base()
        {
            this.world = world;

            rand = new Random();

            Set(new Memory());
            // Monsters are weaker than player characters (which default to 100 HP): a few hits kill.
            Set(new Health(30, 30));
            // Monsters hit for less than a base character (10): retaliation on the tick
            // (P3-7 slice 2) chips a player down rather than bursting them.
            Set(new AttackPower(6));
            Set(new Tile { Type = world.TileTypes["Monster"] });

            // Monsters emit slightly lower heat than characters but still high
            Set(new HeatSignature(0.8, TimeSpan.FromSeconds(8)));

            // Continuous action pipeline (engine gap-analysis §4.1): a monster accrues AP each
            // world tick and only acts once it can afford an action, so speed differentiates how
            // often it acts — no global turn order. The default (Speed == MaxBudget == cost) makes
            // a plain Monster act every eligible tick, matching pre-pipeline behavior; slower or
            // faster creatures override these to act less/more often. See
            // GameMapGrain.StepNpcsAsync and openspec/changes/wire-npc-action-budget-live.
            Set(new ActionSpeed(speed: 1.0, maxBudget: 1.0));
        }

        /// <summary>
        /// Legacy self-driven step. The live tick pipeline no longer calls this —
        /// <see cref="GameMapGrain"/> drives monster movement so it can broadcast
        /// the resulting delta — but it is kept working (and no longer crashes when
        /// boxed in) for tests and any direct caller.
        /// </summary>
        public virtual void Heartbeat()
        {
            var direction = NextWanderDirection();
            if (direction is null)
                return; // boxed in — wait it out rather than crash

            world.TryMove(this, direction.Value);
        }

        /// <summary>
        /// Chooses the next cardinal step for a wandering monster, or <c>null</c>
        /// if it is boxed in on all four sides. A 50% chance keeps the previous
        /// heading (momentum) when that direction is still passable, otherwise a
        /// random passable cardinal is picked. This is advisory only — it does not
        /// move the monster; the caller performs the validated move (which also
        /// enforces occupancy) and broadcasts the result. The chosen direction is
        /// recorded as the momentum hint for next time.
        /// </summary>
        public WorldDirection? NextWanderDirection()
        {
            var validDirections = GetValidCardinalDirections();
            if (validDirections.Count == 0)
                return null;

            WorldDirection chosen;
            if (PreviousDirection.HasValue
                && validDirections.Contains(PreviousDirection.Value)
                && rand.NextDouble() < 0.5)
            {
                chosen = PreviousDirection.Value;
            }
            else
            {
                chosen = validDirections[rand.Next(0, validDirections.Count)];
            }

            PreviousDirection = chosen;
            return chosen;
        }

        /// <summary>
        /// The passable cardinal (N/S/E/W) directions from the monster's current
        /// cell. Monsters wander on the horizontal plane only; vertical travel is
        /// reserved for stair-aware movement. Returns an empty list if the monster
        /// has no location or is fully enclosed.
        /// </summary>
        private IList<WorldDirection> GetValidCardinalDirections()
        {
            var location = Get<WorldLocation>();
            if (location == null)
                return new List<WorldDirection>();

            // Candidates come from the world's topology (square: the four cardinals);
            // WorldDirection is the square-legacy currency TryMoveSteps still speaks —
            // a non-square wander needs a heading-based variant of this API.
            var cell = Aetherium.Topology.GridCoord.From(location);
            var valid = new List<WorldDirection>(world.Topology.MaxDirectionCount);
            foreach (var step in world.Topology.Steps(cell))
            {
                if (world.PassableTerrain(step.Target.ToWorldLocation()))
                    valid.Add(HeadingToCardinal(step.HeadingDegrees));
            }

            return valid;
        }

        /// <summary>Nearest cardinal to an edge heading — exact on square (edge headings
        /// are multiples of 90°); a lossy square-legacy adapter anywhere else.</summary>
        private static WorldDirection HeadingToCardinal(int degrees)
        {
            int n = ((degrees % 360) + 360) % 360;
            if (n < 45 || n >= 315) return WorldDirection.North;
            if (n < 135) return WorldDirection.East;
            if (n < 225) return WorldDirection.South;
            return WorldDirection.West;
        }

        //public void SetGoal()
        //{
        //    // find the strongest recent memories of forest terrain
        //    var memories = Get<Memory>().AllSpaceTimeMemories
        //        .Where(m => m.ContentType == "Terrain"
        //            && m.Content == "Forest"
        //            && m.Impressions > 30
        //            && m.TimeSinceLastSeen.TotalMinutes < 120)
        //        .OrderByDescending(m => m.Impressions)
        //        .Take(3)
        //        .ToList();

        //    Location destination;

        //    // if forest memories exist, there's a strong chance they'll be chosen (90%)
        //    var useMemoryForSettingGoal = memories.Any() && rand.NextDouble() < 0.90;

        //    if (useMemoryForSettingGoal)
        //    {
        //        var memory = memories[rand.Next(0, memories.Count)];
        //        destination = memory.Location;
        //    }
        //    else
        //    {
        //        destination = world.SelectRandomPassableLocation();
        //    }

        //    goal = new Goal
        //    {
        //        Created = DateTime.Now,
        //        Location = destination
        //    };
        //}

        //private void PerceiveEnvironment()
        //{
        //    RememberThisTerrain();
        //}

        //public void RememberThisTerrain()
        //{
        //    var location = Get<Location>();

        //    var bias = world.GetTerrain(location) switch
        //    {
        //        TerrainType.Forest => 1,
        //        TerrainType.Cave => 0.8,
        //        TerrainType.Indoors => 0.1,
        //        TerrainType.Road => 0.3,
        //        TerrainType.Plains => 0.3,
        //        _ => 0.5
        //    };

        //    Get<Memory>().Remember(location, "Terrain", world.GetTerrain(location).ToString());
        //}
    }
}

