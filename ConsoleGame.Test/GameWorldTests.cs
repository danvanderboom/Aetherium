using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using ConsoleGame.Core;

namespace ConsoleGame.Test
{
    public class GameWorldTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void IsGood()
        {
            var world = new GameWorld(100, 100, 1);

        }
    }
}
