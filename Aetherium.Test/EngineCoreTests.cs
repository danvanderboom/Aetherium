using System;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium;

namespace Aetherium.Test
{
    public class EngineCoreTests
    {
        [Test]
        public void Entity_Has_Default_Location_And_Tile()
        {
            var character = new Character();

            Assert.IsTrue(character.Has<WorldLocation>());
            Assert.IsTrue(character.Has<Tile>());
        }

        [Test]
        public void Component_Set_Get_Has_Work()
        {
            var character = new Character();
            var goal = new Goal { Created = DateTime.UtcNow, Location = new WorldLocation(1, 2, 3) };

            character.Set(goal);

            Assert.IsTrue(character.Has<Goal>());
            var fetched = character.Get<Goal>();
            Assert.AreSame(goal, fetched);
        }

        [Test]
        public void World_Add_And_Remove_Character_Update_All_Indexes()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            var character = new Character();
            character.Set(new WorldLocation(10, 10, 0));
            world.AddEntity(character);

            Assert.IsTrue(world.Entities.ContainsKey(character.EntityId));
            Assert.IsTrue(world.EntitiesByLocation.ContainsKey(character.Get<WorldLocation>()));
            Assert.IsTrue(world.Characters.ContainsKey(character.EntityId));

            world.RemoveEntity(character.EntityId);

            Assert.IsFalse(world.Entities.ContainsKey(character.EntityId));
            Assert.IsFalse(world.EntitiesByLocation.ContainsKey(new WorldLocation(10, 10, 0)));
            Assert.IsFalse(world.Characters.ContainsKey(character.EntityId));
        }

        [Test]
        public void TryMove_Blocked_By_Impassable_Terrain()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            var from = new WorldLocation(0, 0, 0);
            var to = new WorldLocation(1, 0, 0);

            // Make both cells exist in the world index
            world.SetTerrain("Indoors", from);
            world.SetTerrain("Mountain", to); // impassable

            var character = new Character();
            character.Set(from);
            world.AddEntity(character);

            var moved = world.TryMove(character, to);
            Assert.IsFalse(moved);
            Assert.AreEqual(from, character.Get<WorldLocation>());
        }

        [Test]
        public void TryMove_Up_Requires_CanAscend()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            var from = new WorldLocation(0, 0, 0);
            var up = new WorldLocation(0, 0, 1);

            world.SetTerrain("Indoors", from);
            world.SetTerrain("Indoors", up);

            var character = new Character();
            character.Set(from);
            world.AddEntity(character);

            // Without CanAscend
            var moved = world.TryMove(character, up);
            Assert.IsFalse(moved);
            Assert.AreEqual(from, character.Get<WorldLocation>());

            // With CanAscend on current location
            character.Get<WorldLocation>().Set(new CanAscend());
            moved = world.TryMove(character, up);
            Assert.IsTrue(moved);
            Assert.AreEqual(up, character.Get<WorldLocation>());
        }

        [Test]
        public void TryMove_Down_Requires_CanDescend()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            var from = new WorldLocation(0, 0, 0);
            var down = new WorldLocation(0, 0, -1);

            world.SetTerrain("Indoors", from);
            world.SetTerrain("Indoors", down);

            var character = new Character();
            character.Set(from);
            world.AddEntity(character);

            // Without CanDescend
            var moved = world.TryMove(character, down);
            Assert.IsFalse(moved);
            Assert.AreEqual(from, character.Get<WorldLocation>());

            // With CanDescend on current location
            character.Get<WorldLocation>().Set(new CanDescend());
            moved = world.TryMove(character, down);
            Assert.IsTrue(moved);
            Assert.AreEqual(down, character.Get<WorldLocation>());
        }

        [Test]
        public void TryMove_Into_Other_Character_Fails_And_Damages()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            var aLoc = new WorldLocation(5, 5, 0);
            var bLoc = new WorldLocation(6, 5, 0);

            world.SetTerrain("Indoors", aLoc);
            world.SetTerrain("Indoors", bLoc);

            var a = new Character();
            a.Set(aLoc);
            a.Set(new Health { Level = 10, MaxLevel = 10 });
            world.AddEntity(a);

            var b = new Character();
            b.Set(bLoc);
            b.Set(new Health { Level = 10, MaxLevel = 10 });
            world.AddEntity(b);

            var moved = world.TryMove(a, bLoc);
            Assert.IsFalse(moved);
            Assert.AreEqual(9, a.Get<Health>().Level);
            Assert.AreEqual(aLoc, a.Get<WorldLocation>());
        }

        [Test]
        public void SetTerrain_Creates_Terrain_Entity_With_TileType()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            var where = new WorldLocation(3, 4, 0);
            var terrain = world.SetTerrain("Indoors", where);

            Assert.IsNotNull(terrain);
            Assert.AreEqual("Indoors", terrain!.Type.Name);
            Assert.AreEqual("Indoors", terrain.Get<Tile>().Type.Name);
        }
    }
}



