using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Aetherium.Model.Games;
using Aetherium.Server.Games;

namespace Aetherium.Test.Games
{
    /// <summary>
    /// Verifies "Game Definition Registry"
    /// (openspec/changes/add-game-definition-loader/specs/game-definitions/spec.md) — including
    /// against the real shipped sample bundles under the repo's Data/Games, which doubles as the
    /// canary that keeps the samples loading and validating cleanly as config types evolve.
    /// </summary>
    [TestFixture]
    public class GameDefinitionRegistryTests
    {
        /// <summary>Walks up from the test output directory to the repo root's Data/Games.</summary>
        private static string ShippedGamesPath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Data", "Games");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
            Assert.Fail("Could not locate the repo's Data/Games directory from the test output path.");
            throw new InvalidOperationException("unreachable");
        }

        [Test]
        public void LoadsAllValidBundlesFromDirectory()
        {
            var registry = new GameDefinitionRegistry(ShippedGamesPath());
            registry.LoadAll();

            var ids = registry.All.Select(d => d.Id).ToList();
            Assert.That(ids, Does.Contain("emberfall"));
            Assert.That(ids, Does.Contain("neonveil"));
            Assert.That(ids, Does.Contain("hexhaven"));
            Assert.That(ids, Does.Contain("trigrove"));
            Assert.That(registry.Diagnostics.Where(d => d.Severity == GameDefinitionDiagnosticSeverity.Error), Is.Empty,
                "The shipped sample bundles must load and validate cleanly: " + string.Join("; ", registry.Diagnostics));

            // The non-square sample bundles carry their tiling as data (docs/grid-topologies.md).
            Assert.That(registry.TryGet("hexhaven", out var hexhaven), Is.True);
            Assert.That(hexhaven!.World.Topology, Is.EqualTo("hex"));
            Assert.That(hexhaven.World.GeneratorType, Is.EqualTo("hex-caves"));
            Assert.That(registry.TryGet("trigrove", out var trigrove), Is.True);
            Assert.That(trigrove!.World.Topology, Is.EqualTo("tri"));
        }

        [Test]
        public void GetById_ReturnsLoadedDefinition()
        {
            var registry = new GameDefinitionRegistry(ShippedGamesPath());
            registry.LoadAll();

            Assert.That(registry.TryGet("emberfall", out var emberfall), Is.True);
            Assert.That(emberfall!.Name, Is.EqualTo("Emberfall"));
            Assert.That(emberfall.Abilities!.Abilities.Select(a => a.Id), Does.Contain("fireball"));
            Assert.That(emberfall.Factions!.Factions.Select(f => f.Id), Does.Contain("town"));

            Assert.That(registry.TryGet("neonveil", out var neonveil), Is.True);
            Assert.That(neonveil!.Death!.Permadeath, Is.True);
            Assert.That(neonveil.Abilities!.CharacterResourcePools.Single().Tag, Is.EqualTo("bandwidth"));
        }

        [Test]
        public void DuplicateGameId_SecondBundleRejected()
        {
            var root = Path.Combine(Path.GetTempPath(), $"aetherium-gamedef-{Guid.NewGuid():N}");
            try
            {
                const string manifest = """
                    id: samegame
                    name: Same Game
                    version: 1.0.0
                    world:
                      generatorType: maze
                    """;
                Directory.CreateDirectory(Path.Combine(root, "a"));
                Directory.CreateDirectory(Path.Combine(root, "b"));
                File.WriteAllText(Path.Combine(root, "a", "game.yaml"), manifest);
                File.WriteAllText(Path.Combine(root, "b", "game.yaml"), manifest.Replace("Same Game", "Impostor"));

                var registry = new GameDefinitionRegistry(root);
                registry.LoadAll();

                Assert.That(registry.All, Has.Count.EqualTo(1));
                Assert.That(registry.TryGet("samegame", out var winner), Is.True);
                Assert.That(winner!.Name, Is.EqualTo("Same Game"), "First bundle (ordinal directory order) wins.");
                Assert.That(registry.Diagnostics.Any(d => d.Message.Contains("Duplicate game id")), Is.True);
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }
    }
}
