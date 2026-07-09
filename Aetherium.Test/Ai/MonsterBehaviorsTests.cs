using NUnit.Framework;
using Aetherium;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server;
using Aetherium.Server.Ai;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Ai
{
    /// <summary>
    /// Integration coverage of the worked-example monster tree (engine gap-analysis §4.5, Phase 1
    /// — see openspec/changes/add-npc-behavior-trees). Confirms the tree reproduces
    /// GameMapGrain.StepNpcsAsync's existing inline decision (attack if adjacent, else wander)
    /// against the live, unmodified CombatSystem.
    /// Verifies "Worked Example Reproduces Current Monster Behavior" in specs/npc-behavior-trees/spec.md.
    /// </summary>
    [TestFixture]
    public class MonsterBehaviorsTests
    {
        private static World NewWorld()
        {
            var world = new World();
            var builder = new TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        /// <summary>Fills an inclusive square of passable/transparent terrain — a fresh World has
        /// no per-tile terrain painted, so every cell defaults to impassable.</summary>
        private static void FillOpenRoom(World world, int x0, int y0, int x1, int y1, int z = 0)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    world.SetTerrain("Indoors", new WorldLocation(x, y, z));
        }

        [Test]
        public void Tick_PlayerAdjacent_Attacks_NotWander()
        {
            var world = NewWorld();
            FillOpenRoom(world, 3, 3, 8, 8);
            var monster = new Monster(world);
            monster.Set(new WorldLocation(5, 5, 0));
            world.AddEntity(monster);

            var player = new Character();
            player.Set(new WorldLocation(6, 5, 0));
            player.Set(new Health(30, 30));
            world.AddEntity(player);

            var tree = MonsterBehaviors.BuildWanderAndMeleeTree(new CombatSystem());
            var status = tree.Tick(world, monster);

            Assert.That(status, Is.EqualTo(BehaviorStatus.Success));
            Assert.That(player.Get<Health>().Level, Is.LessThan(30), "Adjacent player must be attacked, not ignored in favor of wandering.");
            Assert.That(monster.Get<WorldLocation>().X, Is.EqualTo(5), "Monster must not move on an attack tick.");
        }

        [Test]
        public void Tick_NoPlayerAdjacent_Wanders()
        {
            var world = NewWorld();
            FillOpenRoom(world, 8, 8, 13, 13);
            var monster = new Monster(world);
            monster.Set(new WorldLocation(10, 10, 0));
            world.AddEntity(monster);

            var tree = MonsterBehaviors.BuildWanderAndMeleeTree(new CombatSystem());
            var status = tree.Tick(world, monster);

            // With no adjacent target, the attack branch fails and the tree falls through to wander.
            // Wander may itself fail if boxed in, but on an open torus world it should succeed.
            Assert.That(status, Is.EqualTo(BehaviorStatus.Success));
        }
    }
}
