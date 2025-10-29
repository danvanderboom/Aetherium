using NUnit.Framework;
using ConsoleGame.Geometry;

namespace ConsoleGame.Test
{
    public class GridColoringAdditionalTests
    {
        [Test]
        public void GetColor_Is_Periodic_And_Supports_Negative_Indices()
        {
            var grid = new string[,]
            {
                { "A", "B" },
                { "C", "D" },
            };

            var coloring = new GridColoring<string>(grid);

            // Base cells
            Assert.AreEqual("A", coloring.GetColor(0, 0));
            Assert.AreEqual("B", coloring.GetColor(1, 0));
            Assert.AreEqual("C", coloring.GetColor(0, 1));
            Assert.AreEqual("D", coloring.GetColor(1, 1));

            // Repeat to the right and down
            Assert.AreEqual("A", coloring.GetColor(2, 2));

            // Negative indices use absolute modulo
            Assert.AreEqual("B", coloring.GetColor(-1, 0));
            Assert.AreEqual("C", coloring.GetColor(0, -1));
            Assert.AreEqual("D", coloring.GetColor(-1, -1));
        }

        [Test]
        public void ConnectedCells_Uses_FourWay_Connectivity()
        {
            var grid = new string[,]
            {
                { "X", "Y", "X" },
                { "Y", "X", "Y" },
                { "X", "Y", "X" },
            };

            var coloring = new GridColoring<string>(grid);

            // Center (1,1) has color X. Its 4-neighbors are Y, so component is just itself.
            var cells = coloring.GetConnectedCells(1, 1);
            Assert.AreEqual(1, cells.Count);
        }
    }
}


