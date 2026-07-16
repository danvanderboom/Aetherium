using NUnit.Framework;
using ClientLayout = Aetherium.Client.Contracts.GridCellLayout;
using ServerLayout = Aetherium.Model.GridCellLayout;

namespace Aetherium.Client.Tests
{
    /// <summary>
    /// Keeps the client mirror of the layout math in lockstep with the server-side original
    /// (Aetherium.Model.GridCellLayout — not referenceable from Unity builds): every method,
    /// every topology, across a coordinate window including negative rows and both triangle
    /// parities. Same drift discipline as the DTO mirrors.
    /// </summary>
    [TestFixture]
    public class GridCellLayoutDriftTests
    {
        private static readonly string?[] Topologies = { null, "square", "hex", "tri", "h3" };

        [Test]
        public void EveryMethod_MatchesTheServerOriginal_AcrossTheWindow()
        {
            foreach (var topology in Topologies)
            {
                Assert.That(ClientLayout.CellCharWidth(topology), Is.EqualTo(ServerLayout.CellCharWidth(topology)),
                    $"CellCharWidth({topology})");

                for (int relY = -5; relY <= 5; relY++)
                {
                    Assert.That(ClientLayout.RowStaggerChars(topology, relY),
                        Is.EqualTo(ServerLayout.RowStaggerChars(topology, relY)),
                        $"RowStaggerChars({topology}, {relY})");

                    for (int relX = -5; relX <= 5; relX++)
                    {
                        Assert.That(ClientLayout.CharColumnOffset(topology, relX, relY),
                            Is.EqualTo(ServerLayout.CharColumnOffset(topology, relX, relY)),
                            $"CharColumnOffset({topology}, {relX}, {relY})");

                        Assert.That(ClientLayout.RelXForCellIndex(topology, relX + 5, relY, 5),
                            Is.EqualTo(ServerLayout.RelXForCellIndex(topology, relX + 5, relY, 5)),
                            $"RelXForCellIndex({topology}, {relX + 5}, {relY}, 5)");

                        foreach (var parity in new int?[] { null, 0, 1 })
                        {
                            Assert.That(ClientLayout.CellParity(topology, parity, relX, relY),
                                Is.EqualTo(ServerLayout.CellParity(topology, parity, relX, relY)),
                                $"CellParity({topology}, {parity}, {relX}, {relY})");

                            var client = ClientLayout.CellLayoutPosition(topology, relX, relY, parity);
                            var server = ServerLayout.CellLayoutPosition(topology, relX, relY, parity);
                            Assert.That(client.X, Is.EqualTo(server.X).Within(1e-12),
                                $"CellLayoutPosition({topology}, {relX}, {relY}, {parity}).X");
                            Assert.That(client.Y, Is.EqualTo(server.Y).Within(1e-12),
                                $"CellLayoutPosition({topology}, {relX}, {relY}, {parity}).Y");
                        }
                    }
                }
            }
        }
    }
}
