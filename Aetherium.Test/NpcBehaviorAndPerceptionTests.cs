extern alias Server;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Aetherium.Model;
using World = Server::Aetherium.Core.World;
using WorldLocation = Server::Aetherium.Components.WorldLocation;
using Monster = Server::Aetherium.Monster;
using Character = Server::Aetherium.Character;
using WorldDirection = Server::Aetherium.WorldDirection;
using PerceptionService = Server::Aetherium.Server.PerceptionService;
using DungeonCrawlerWorldBuilder = Server::Aetherium.WorldBuilders.DungeonCrawlerWorldBuilder;

namespace Aetherium.Test
{
    /// <summary>
    /// Phase 5 vertical slice — NPCs visible and ticking. Covers the two halves at
    /// the level where they can be asserted deterministically (no map seed): the
    /// monster wander decision (<see cref="Monster.NextWanderDirection"/> + the
    /// validated World move it feeds), and the perception encoding that carries
    /// visible characters to the client (<c>PerceptionDto.VisibleCharacters</c>).
    /// The grain-driven tick → fan-out wiring is covered in
    /// <c>EndToEndSharedMutationTests</c>.
    /// </summary>
    public class NpcBehaviorAndPerceptionTests
    {
        private static World CreateWorldWithTiles()
        {
            var world = new World();
            var builder = new DungeonCrawlerWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        /// <summary>Fills an inclusive square of Indoors (passable, transparent) terrain.</summary>
        private static void FillIndoors(World world, int x0, int y0, int x1, int y1, int z = 0)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    world.SetTerrain("Indoors", new WorldLocation(x, y, z));
        }

        // ----- monster wander decision -----------------------------------

        [Test]
        public void NextWanderDirection_In_Open_Space_Returns_A_Cardinal()
        {
            var world = CreateWorldWithTiles();
            FillIndoors(world, 4, 4, 6, 6); // 3x3 open room

            var monster = new Monster(world);
            monster.Set(new WorldLocation(5, 5, 0));
            world.AddEntity(monster);

            var direction = monster.NextWanderDirection();

            Assert.That(direction, Is.Not.Null, "an open cell should yield a wander direction");
            Assert.That(direction!.Value, Is.AnyOf(
                WorldDirection.North, WorldDirection.South,
                WorldDirection.East, WorldDirection.West),
                "wandering is cardinal-only — never vertical");
        }

        [Test]
        public void NextWanderDirection_When_Boxed_In_Returns_Null()
        {
            var world = CreateWorldWithTiles();
            // Only the monster's own cell is passable; all four neighbours are unset
            // (and therefore impassable), so it is fully enclosed.
            world.SetTerrain("Indoors", new WorldLocation(5, 5, 0));

            var monster = new Monster(world);
            monster.Set(new WorldLocation(5, 5, 0));
            world.AddEntity(monster);

            Assert.That(monster.NextWanderDirection(), Is.Null,
                "a boxed-in monster must not crash and must report no move");
        }

        [Test]
        public void Monster_Wanders_To_An_Adjacent_Open_Cell()
        {
            var world = CreateWorldWithTiles();
            FillIndoors(world, 4, 4, 6, 6);

            var monster = new Monster(world);
            monster.Set(new WorldLocation(5, 5, 0));
            world.AddEntity(monster);

            var direction = monster.NextWanderDirection();
            Assert.That(direction, Is.Not.Null);

            // Drive the move exactly as GameMapGrain.StepNpcsAsync does.
            var outcome = world.TryMoveSteps(monster, direction!.Value, 1);
            Assert.That(outcome.Success, Is.True, outcome.BlockedReason);

            var after = monster.Get<WorldLocation>()!;
            var manhattan = System.Math.Abs(after.X - 5) + System.Math.Abs(after.Y - 5);
            Assert.That(manhattan, Is.EqualTo(1), "the monster should have stepped exactly one cardinal cell");
            Assert.That(after.Z, Is.EqualTo(0));
        }

        // ----- perception encoding of visible characters ------------------

        [Test]
        public void Perception_Includes_A_Visible_Monster_With_Its_Tile()
        {
            var world = CreateWorldWithTiles();
            FillIndoors(world, 0, 0, 4, 4);

            // Perceiving player at the centre; a monster one cell to the north.
            var player = new Character();
            player.Set(new WorldLocation(2, 2, 0));
            world.AddEntity(player);

            var monster = new Monster(world);
            monster.Set(new WorldLocation(2, 1, 0));
            world.AddEntity(monster);

            var perception = new PerceptionService().ComputePerception(
                world, new WorldLocation(2, 2, 0), WorldDirection.North, new Size(15, 15));

            var seenMonster = perception.VisibleCharacters.SingleOrDefault(c => c.IsHostile);
            Assert.That(seenMonster, Is.Not.Null, "the monster to the north should be perceived");
            Assert.That(seenMonster!.Name, Is.EqualTo("Monster"));
            Assert.That(seenMonster.Tile, Is.Not.Null, "the character must carry a tile for the client to render");
            // The glyph is whatever this world defines for a Monster — assert the DTO
            // faithfully carries that tile, rather than hardcoding a builder-specific char.
            var expectedGlyph = world.TileTypes["Monster"].Settings["MapCharacter"];
            Assert.That(seenMonster.Tile!.Settings["MapCharacter"], Is.EqualTo(expectedGlyph));

            // Relative coordinates: monster (2,1) − player (2,2) = (0,-1).
            Assert.That(seenMonster.Location, Is.Not.Null);
            Assert.That((seenMonster.Location!.X, seenMonster.Location.Y, seenMonster.Location.Z),
                Is.EqualTo((0, -1, 0)));
        }

        [Test]
        public void Perception_Excludes_The_Perceiving_Player_From_VisibleCharacters()
        {
            var world = CreateWorldWithTiles();
            FillIndoors(world, 0, 0, 4, 4);

            var player = new Character();
            player.Set(new WorldLocation(2, 2, 0));
            world.AddEntity(player);

            var perception = new PerceptionService().ComputePerception(
                world, new WorldLocation(2, 2, 0), WorldDirection.North, new Size(15, 15));

            // The player is always the centre marker, never a "seen" character.
            Assert.That(perception.VisibleCharacters.Any(
                    c => c.Location != null && c.Location.X == 0 && c.Location.Y == 0 && c.Location.Z == 0),
                Is.False,
                "the perceiving player must not appear in VisibleCharacters");
        }
    }
}
