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

        //[Test]
        //public void MonsterHasPositionHealthMind()
        //{
        //    var world = new GameWorld(100, 100, 1);
        //    var monster = new Monster(world);

        //    Assert.IsTrue(monster.HasComponent(typeof(Location)));
        //    Assert.IsTrue(monster.HasComponent(typeof(Health)));
        //    Assert.IsTrue(monster.HasComponent(typeof(Memory)));
        //}

        //[Test]
        //public void IterateComponentTree()
        //{
        //    var world = new GameWorld(100, 100, 1);
        //    var monster = new Monster(world);
        //    monster.Get<Memory>().Components.TryAdd(typeof(Health), new Health());

        //    var components = monster.AllComponents.ToList();
        //    var count = components.Count(c => typeof(Health).IsAssignableFrom(c.GetType()));

        //    Assert.AreEqual(2, count);
        //}

        //[Test]
        //public void MonsterHasAllComponents()
        //{
        //    var world = new GameWorld(100, 100, 1);
        //    var monster = new Monster(world);

        //    var hasAllComponents = monster.HasAllComponents(new List<Type> 
        //    {
        //        typeof(Location),
        //        typeof(Health),
        //        typeof(Memory)
        //    });

        //    Assert.IsTrue(hasAllComponents);
        //}

        //[Test]
        //public void MonsterHasNotAllComponents()
        //{
        //    var world = new GameWorld(100, 100, 1);
        //    var monster = new Monster(world);

        //    var hasAllComponents = monster.HasAllComponents(new List<Type>
        //    {
        //        typeof(Location),
        //        typeof(Health),
        //        typeof(Goal)
        //    });

        //    Assert.IsFalse(hasAllComponents);
        //}
    }
}
