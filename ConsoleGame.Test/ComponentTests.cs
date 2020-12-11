using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Test
{
    public class ComponentTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void MonsterHasPositionHealthMind()
        {
            var world = new GameWorld(100, 100, 1);
            var monster = new Monster(world);

            Assert.IsTrue(monster.HasComponent(typeof(Position)));
            Assert.IsTrue(monster.HasComponent(typeof(Health)));
            Assert.IsTrue(monster.HasComponent(typeof(Mind)));
        }

        [Test]
        public void IterateComponentTree()
        {
            var world = new GameWorld(100, 100, 1);
            var monster = new Monster(world);

            var components = monster.AllComponents.ToList();
            var count = components.Count(c => c.GetType() == typeof(Health));

            Assert.AreEqual(2, count);
        }
    }
}
