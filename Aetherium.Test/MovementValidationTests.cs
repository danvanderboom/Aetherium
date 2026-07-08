using System;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;
using Aetherium;

namespace Aetherium.Test
{
    /// <summary>
    /// Tests for the validated movement API (World.TryMoveSteps / TryChangeLevel)
    /// that every live movement path routes through — see P0-1 in
    /// docs/audits/2026-07-03-initial-subsystem-audit/RECOMMENDATIONS.md. These would have caught the original
    /// walk-through-walls / teleport-between-floors defects.
    /// </summary>
    public class MovementValidationTests
    {
        private static World CreateWorld()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        private static Character PlaceCharacter(World world, WorldLocation at)
        {
            var character = new Character();
            character.Set(at);
            world.AddEntity(character);
            return character;
        }

        [Test]
        public void TryMoveSteps_Moves_Into_Open_Cell()
        {
            var world = CreateWorld();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(1, 0, 0));
            var character = PlaceCharacter(world, new WorldLocation(0, 0, 0));

            var outcome = world.TryMoveSteps(character, WorldDirection.East, 1);

            Assert.IsTrue(outcome.Success);
            Assert.AreEqual(1, outcome.StepsTaken);
            Assert.AreEqual(new WorldLocation(1, 0, 0), character.Get<WorldLocation>());
        }

        [Test]
        public void TryMoveSteps_Blocked_By_Impassable_Terrain()
        {
            var world = CreateWorld();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Mountain", new WorldLocation(1, 0, 0)); // impassable
            var character = PlaceCharacter(world, new WorldLocation(0, 0, 0));

            var outcome = world.TryMoveSteps(character, WorldDirection.East, 1);

            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(0, outcome.StepsTaken);
            Assert.IsNotNull(outcome.BlockedReason);
            Assert.AreEqual(new WorldLocation(0, 0, 0), character.Get<WorldLocation>());
        }

        [Test]
        public void TryMoveSteps_Blocked_By_Map_Edge()
        {
            var world = CreateWorld();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            // (1,0,0) is never created — the void beyond the map.
            var character = PlaceCharacter(world, new WorldLocation(0, 0, 0));

            var outcome = world.TryMoveSteps(character, WorldDirection.East, 1);

            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(new WorldLocation(0, 0, 0), character.Get<WorldLocation>());
        }

        [Test]
        public void TryMoveSteps_MultiStep_Stops_At_First_Blocked_Cell()
        {
            var world = CreateWorld();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(1, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(2, 0, 0));
            world.SetTerrain("Mountain", new WorldLocation(3, 0, 0)); // wall mid-path
            world.SetTerrain("Indoors", new WorldLocation(4, 0, 0));
            var character = PlaceCharacter(world, new WorldLocation(0, 0, 0));

            var outcome = world.TryMoveSteps(character, WorldDirection.East, 4);

            // Partial success: moved 2 of 4, never crossed the wall.
            Assert.IsTrue(outcome.Success);
            Assert.AreEqual(2, outcome.StepsTaken);
            Assert.IsNotNull(outcome.BlockedReason);
            Assert.AreEqual(new WorldLocation(2, 0, 0), character.Get<WorldLocation>());
        }

        [Test]
        public void TryMoveSteps_Blocked_By_Closed_Door_Entity()
        {
            var world = CreateWorld();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(1, 0, 0));
            var character = PlaceCharacter(world, new WorldLocation(0, 0, 0));

            var door = new Door(); // Door ctor sets ObstructsMovement (closed)
            door.Set(new WorldLocation(1, 0, 0));
            world.AddEntity(door);

            var outcome = world.TryMoveSteps(character, WorldDirection.East, 1);

            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(new WorldLocation(0, 0, 0), character.Get<WorldLocation>());
        }

        [Test]
        public void TryMoveSteps_Allowed_Through_Open_Door()
        {
            var world = CreateWorld();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(1, 0, 0));
            var character = PlaceCharacter(world, new WorldLocation(0, 0, 0));

            var door = new Door();
            door.Set(new WorldLocation(1, 0, 0));
            world.AddEntity(door);
            // Open the door the same way InteractionSystem.ToggleDoor does.
            door.Get<OpensAndCloses>().IsOpen = true;
            door.Clear<ObstructsMovement>();
            door.Clear<ObstructsView>();

            var outcome = world.TryMoveSteps(character, WorldDirection.East, 1);

            Assert.IsTrue(outcome.Success);
            Assert.AreEqual(new WorldLocation(1, 0, 0), character.Get<WorldLocation>());
        }

        [Test]
        public void TryMoveSteps_Blocked_By_Other_Character()
        {
            var world = CreateWorld();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(1, 0, 0));
            var character = PlaceCharacter(world, new WorldLocation(0, 0, 0));
            PlaceCharacter(world, new WorldLocation(1, 0, 0)); // occupant

            var outcome = world.TryMoveSteps(character, WorldDirection.East, 1);

            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(new WorldLocation(0, 0, 0), character.Get<WorldLocation>());
        }

        [Test]
        public void TryChangeLevel_Fails_Without_Stairs()
        {
            var world = CreateWorld();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 1));
            var character = PlaceCharacter(world, new WorldLocation(0, 0, 0));

            var outcome = world.TryChangeLevel(character, 1);

            Assert.IsFalse(outcome.Success);
            StringAssert.Contains("stairs", outcome.BlockedReason!.ToLowerInvariant());
            Assert.AreEqual(0, character.Get<WorldLocation>()!.Z);
        }

        [Test]
        public void TryChangeLevel_Succeeds_On_Stair_Terrain()
        {
            var world = CreateWorld();
            world.SetTerrain("Upstairs", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 1));
            var character = PlaceCharacter(world, new WorldLocation(0, 0, 0));

            var outcome = world.TryChangeLevel(character, 1);

            Assert.IsTrue(outcome.Success);
            Assert.AreEqual(1, character.Get<WorldLocation>()!.Z);
        }

        [Test]
        public void TryChangeLevel_Fails_When_Landing_Is_Void()
        {
            var world = CreateWorld();
            world.SetTerrain("Upstairs", new WorldLocation(0, 0, 0));
            // No cell exists at (0,0,1) — the old code teleported players there.
            var character = PlaceCharacter(world, new WorldLocation(0, 0, 0));

            var outcome = world.TryChangeLevel(character, 1);

            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(0, character.Get<WorldLocation>()!.Z);
        }

        [Test]
        public void TryChangeLevel_Honors_CanAscend_Component()
        {
            var world = CreateWorld();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 1));
            var character = PlaceCharacter(world, new WorldLocation(0, 0, 0));
            character.Get<WorldLocation>()!.Set(new CanAscend());

            var outcome = world.TryChangeLevel(character, 1);

            Assert.IsTrue(outcome.Success);
            Assert.AreEqual(1, character.Get<WorldLocation>()!.Z);
        }

        [Test]
        public void GameSession_MoveView_Blocked_Keeps_View_On_Player()
        {
            var session = new Aetherium.Server.GameSession(
                "test-connection", new WorldBuilders.FovDiagnosticWorldBuilder("open_space"));
            Assert.IsNotNull(session.Player);

            var playerLoc = session.Player!.Get<WorldLocation>()!;
            // Wall directly north of the player.
            session.World.SetTerrain("Wall", new WorldLocation(playerLoc.X, playerLoc.Y - 1, playerLoc.Z));
            session.Heading = WorldDirection.North;

            var outcome = session.MoveView(Aetherium.Model.RelativeDirection.Forward, 1);

            Assert.IsFalse(outcome.Success);
            var after = session.Player.Get<WorldLocation>()!;
            Assert.AreEqual(playerLoc, after);
            Assert.AreEqual(after.X, session.ViewLocation!.X);
            Assert.AreEqual(after.Y, session.ViewLocation!.Y);
        }

        [Test]
        public void GameSession_MoveView_Succeeds_Into_Open_Space()
        {
            var session = new Aetherium.Server.GameSession(
                "test-connection", new WorldBuilders.FovDiagnosticWorldBuilder("open_space"));
            Assert.IsNotNull(session.Player);
            session.Heading = WorldDirection.North;
            var before = session.Player!.Get<WorldLocation>()!;

            var outcome = session.MoveView(Aetherium.Model.RelativeDirection.Forward, 1);

            Assert.IsTrue(outcome.Success);
            var after = session.Player.Get<WorldLocation>()!;
            Assert.AreEqual(before.Y - 1, after.Y);
            Assert.AreEqual(after.Y, session.ViewLocation!.Y);
        }
    }
}
