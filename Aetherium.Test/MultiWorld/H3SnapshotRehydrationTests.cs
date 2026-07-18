using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Server.MultiWorld;
using Aetherium.Topology;
using Aetherium.WorldBuilders;
using Aetherium.WorldGen;

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// Regression for a live-smoke finding: after a GameMapGrain hosting an H3 world deactivated and
    /// reactivated, snapshot rehydration rebuilt the world as a SQUARE grid, so a joining player
    /// spawned off-map at (15,15) and saw almost nothing. The cause was that
    /// <see cref="SnapshotWorldBuilder"/> did not propagate the recipe's Topology into the
    /// regeneration request — so OutdoorLayoutPass ran the planar generator instead of the H3 one.
    /// (RegenerateFromRecipe and InitializeAsync both set Topology; only the snapshot-hydrate path,
    /// which takes precedence, omitted it — see WorldRecipe.Topology's own remarks.)
    /// </summary>
    [TestFixture]
    public class H3SnapshotRehydrationTests
    {
        private static WorldSnapshot H3Snapshot(int resolution) => new()
        {
            WorldId = "w", MapId = "w:map:0",
            Size = new WorldSize { Width = 256, Height = 256, Depth = 1 },
            Recipe = new WorldRecipe
            {
                GeneratorType = "h3-terrain",
                Seed = 20260718,
                Template = WorldGenerationTemplate.Outdoor,
                Topology = "h3",
                Width = 256, Height = 256, Levels = 1,
                Parameters = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
                {
                    ["h3Resolution"] = resolution.ToString(),
                    ["minBand"] = "-3", ["maxBand"] = "64",
                    ["capitalCount"] = "6", ["cityCount"] = "24", ["townCount"] = "70", ["villageCount"] = "220",
                    ["transit"] = "1", ["subwayBand"] = "-2", ["satelliteCount"] = "18",
                },
            },
            Entities = new List<EntityPlacement>(),
        };

        [Test]
        public void SnapshotRehydrationKeepsTheSphereWalkable()
        {
            var registry = new MapGeneratorRegistry();
            registry.DiscoverTypes(typeof(IMapGenerator).Assembly);

            var world = new SnapshotWorldBuilder(H3Snapshot(resolution: 3), registry).Build();

            // The rehydrated world must be the SAME tiling it was saved on — not a square fallback.
            Assert.That(world.Topology, Is.SameAs(H3Topology.Instance),
                "snapshot rehydration must rebuild an H3 world on the H3 tiling, not a square grid");

            // And it must still have walkable ground — the join path spawns via SelectRandomPassableLocation.
            var spawn = world.SelectRandomPassableLocation();
            Assert.That(spawn, Is.Not.Null,
                "a rehydrated H3 world must still have passable ground to spawn a joining player");
            Assert.That(world.PassableTerrain(spawn!), Is.True);
        }
    }
}
