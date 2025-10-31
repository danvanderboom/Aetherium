extern alias Server;
using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using ServerGridColoring = Server::Aetherium.Geometry.GridColoring<string>;

namespace Aetherium.Test
{
    public class GridColoringTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void DetectCellsConnectedByColoring()
        {
            var coloring = GridColoring3x3();
            var cells = coloring.GetConnectedCells(1, 1);

            Assert.AreEqual(4, cells.Count);
        }

        [Test]
        public void CheckColors()
        {
            var coloring = GridColoring3x3();

            var colors = new string[10, 10];

            for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                    colors[y, x] = coloring.GetColor(x, y);
        }

        ServerGridColoring GridColoring2x2() => new ServerGridColoring(
            new string[,]
            {
                { "Red", "Blue" },
                { "Blue", "White" },
            });

        ServerGridColoring GridColoring3x3() => new ServerGridColoring(
            new string[,]
            {
                { "White", "Yellow", "Yellow" },
                { "Blue", "Blue", "Yellow" },
                { "Blue", "Yellow", "Blue" },
            });
    }
}

