using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Components;

namespace Aetherium.Test
{
    public class WorldTests
    {
        [SetUp]
        public void Setup()
        {
        }

        Bomb CreateTestBomb()
        {
            var bomb = new Bomb();

            bomb.Set(new DelayedExplosion
            {
                BlastRadius = 10,
                DetonationSeconds = 10,
                Strength = 5
            });

            bomb.Set(new WorldLocation { X = 1, Y = 2, Z = -5 });

            return bomb;
        }


        [Test]
        public void CanAddEntityWithLocation()
        {
            var world = new World();
            var bomb = CreateTestBomb();
            world.AddEntity(bomb);

            Assert.IsTrue(world.Entities.ContainsKey(bomb.EntityId));
            Assert.IsTrue(world.EntitiesByLocation.ContainsKey(bomb.Get<WorldLocation>()));
            Assert.IsTrue(world.EntitiesByLocation[bomb.Get<WorldLocation>()].ContainsKey(bomb.EntityId));
        }

        [Test]
        public void CanRemoveEntityWithLocation()
        {
            var world = new World();
            var bomb = CreateTestBomb();
            world.AddEntity(bomb);

            world.RemoveEntity(bomb.EntityId);

            Assert.IsFalse(world.Entities.ContainsKey(bomb.EntityId));
            Assert.IsFalse(world.EntitiesByLocation.ContainsKey(bomb.Get<WorldLocation>()));
        }
    }
}

