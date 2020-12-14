using ConsoleGame.Components;
using NUnit.Framework;
using System.Collections.Generic;

namespace ConsoleGame.Test
{
    public class PositionTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void PositionsAreEqual()
        {
            var pos1 = new WorldLocation(1, 2, 3);
            var pos2 = new WorldLocation(1, 2, 3);

            Assert.AreEqual(pos1, pos2);
        }

        [Test]
        public void PositionEqualsPosition()
        {
            var pos1 = new WorldLocation(1, 2, 3);
            var pos2 = new WorldLocation(1, 2, 3);

            Assert.IsTrue(pos1 == pos2);
        }

        [Test]
        public void PositionAsDictionaryKey()
        {
            var d = new Dictionary<WorldLocation, string>();

            var pos1 = new WorldLocation(1, 2, 3);
            d.Add(pos1, "Test");

            var pos2 = new WorldLocation(1, 2, 3);
            var text = d[pos1];

            Assert.AreEqual("Test", text);
        }
    }
}