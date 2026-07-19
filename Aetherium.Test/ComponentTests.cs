using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Test
{
    public class ComponentTests
    {
        [SetUp]
        public void Setup()
        {
        }

        // Pins the null-equality contract for WorldLocation's overloaded operator ==/!=.
        // The both-null branch in WorldLocation.Equals was historically commented out, so
        // `loc == null` returned false even when loc was actually null (and `loc != null`
        // returned true), causing NREs downstream. See WorldLocation.Equals.

        [Test]
        public void WorldLocation_NullEqualsNull_IsTrue()
        {
            WorldLocation? a = null;
            WorldLocation? b = null;

            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.IsTrue(WorldLocation.Equals(a, b));
        }

        [Test]
        public void WorldLocation_NullComparedToInstance_IsNotEqual()
        {
            WorldLocation? nullLoc = null;
            var loc = new WorldLocation(1, 2, 3);

            Assert.IsFalse(loc == nullLoc);
            Assert.IsTrue(loc != nullLoc);
            Assert.IsFalse(nullLoc == loc);
            Assert.IsTrue(nullLoc != loc);
        }

        [Test]
        public void WorldLocation_NullGuardWithEqualsOperator_DetectsNull()
        {
            // The real-world symptom: guarding on `== null` must detect an actual null.
            WorldLocation? location = null;
            Assert.IsTrue(location == null, "location == null must be true when location is null");
            Assert.IsFalse(location != null, "location != null must be false when location is null");
        }

        [Test]
        public void WorldLocation_EqualCoordinates_AreEqual()
        {
            var a = new WorldLocation(4, 5, 6);
            var b = new WorldLocation(4, 5, 6);
            var c = new WorldLocation(4, 5, 7);

            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.IsFalse(a == c);
            Assert.IsTrue(a != c);
        }

        // Pins the sentinel contract: WorldLocation.None must NOT compare equal to a real
        // (0, 0, 0) coordinate, even though both stringify to the same X/Y/Z. Equals ignored
        // IsNone previously, so None collided with the origin in both == and GetHashCode.

        [Test]
        public void WorldLocation_None_IsNotEqualToRealOrigin()
        {
            var origin = new WorldLocation(0, 0, 0);

            Assert.IsFalse(WorldLocation.None == origin, "None must not equal a real (0,0,0)");
            Assert.IsTrue(WorldLocation.None != origin);
            Assert.IsFalse(origin == WorldLocation.None);
            Assert.IsFalse(WorldLocation.None.Equals(origin));
            Assert.IsFalse(origin.Equals(WorldLocation.None));
        }

        [Test]
        public void WorldLocation_None_EqualsNone()
        {
            var anotherNone = new WorldLocation(); // parameterless ctor => IsNone == true

            Assert.IsTrue(WorldLocation.None.IsNone);
            Assert.IsTrue(anotherNone.IsNone);
            Assert.IsTrue(WorldLocation.None == anotherNone, "two None values are equal");
            Assert.IsFalse(WorldLocation.None != anotherNone);
        }

        [Test]
        public void WorldLocation_None_HashDistinctFromRealOrigin()
        {
            // Hash must stay consistent with Equals so a dictionary keyed by real coordinates
            // never collides None with the origin bucket.
            var origin = new WorldLocation(0, 0, 0);
            var anotherNone = new WorldLocation();

            Assert.AreNotEqual(WorldLocation.None.GetHashCode(), origin.GetHashCode());
            Assert.AreEqual(WorldLocation.None.GetHashCode(), anotherNone.GetHashCode());

            var byLocation = new Dictionary<WorldLocation, string>
            {
                [origin] = "origin",
                [WorldLocation.None] = "none",
            };
            Assert.AreEqual(2, byLocation.Count, "None and real (0,0,0) must be distinct keys");
            Assert.AreEqual("origin", byLocation[new WorldLocation(0, 0, 0)]);
            Assert.AreEqual("none", byLocation[WorldLocation.None]);
        }

        [Test]
        public void WorldLocation_RealOrigin_StillEqualsRealOrigin()
        {
            // Guard against over-fixing: two genuine (0,0,0) locations remain equal and
            // share a hash bucket (dictionary behavior for real coordinates is unchanged).
            var a = new WorldLocation(0, 0, 0);
            var b = new WorldLocation(0, 0, 0);

            Assert.IsTrue(a == b);
            Assert.IsFalse(a.IsNone);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
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

