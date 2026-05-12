using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Server.MultiWorld;
using Aetherium.WorldBuilders;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Passes;
using Xunit;

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// Verifies the WorldSnapshotBuilder / SnapshotWorldBuilder pair: a world
    /// hydrated from a snapshot of another world has the same terrain layout, the
    /// same non-terrain entity IDs at the same positions, and is an independent
    /// instance (mutating one doesn't affect the other).
    /// </summary>
    public class WorldSnapshotRoundTripTests
    {
        private static (World world, WorldRecipe recipe) BuildCanonicalWorld(int seed)
        {
            var registry = new MapGeneratorRegistry();
            registry.DiscoverTypes(typeof(IMapGenerator).Assembly);

            var request = new WorldGenerationRequest
            {
                LayoutGenerator = "AdvancedDungeon",
                Template = WorldGenerationTemplate.Dungeon,
                Width = 40,
                Height = 40,
                Levels = 1,
                Seed = seed,
                GeneratorVersion = "1.0.0",
            };

            var passes = new IWorldGenerationPass[]
            {
                new DungeonLayoutPass(),
                new DungeonInteractionsPass(),
                new PortalNetworkPass(),
                new DungeonValidationPass()
            };
            var orchestrator = new WorldGenerationOrchestrator(registry, passes);
            var result = orchestrator.Generate(request);
            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.NotNull(result.World);

            var recipe = new WorldRecipe
            {
                GeneratorType = request.LayoutGenerator,
                Seed = seed,
                Template = request.Template,
                GeneratorVersion = request.GeneratorVersion,
                Width = request.Width,
                Height = request.Height,
                Levels = request.Levels,
                Parameters = new Dictionary<string, string>(),
            };

            return (result.World!, recipe);
        }

        private static MapGeneratorRegistry BuildRegistry()
        {
            var registry = new MapGeneratorRegistry();
            registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
            return registry;
        }

        [Fact]
        public void Hydrated_World_Has_Same_Terrain_Layout()
        {
            var (source, recipe) = BuildCanonicalWorld(seed: 1337);
            var snapshot = WorldSnapshotBuilder.SnapshotOf(source, recipe, "w1", "m1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 });

            var hydrated = new SnapshotWorldBuilder(snapshot, BuildRegistry()).Build();

            // Every cell with terrain in the source should have terrain in the hydrated world.
            var sourceTerrainCells = source.Entities.Values
                .OfType<Terrain>()
                .Select(t => t.Get<WorldLocation>())
                .ToHashSet();
            var hydratedTerrainCells = hydrated.Entities.Values
                .OfType<Terrain>()
                .Select(t => t.Get<WorldLocation>())
                .ToHashSet();

            Assert.Equal(sourceTerrainCells.Count, hydratedTerrainCells.Count);
            foreach (var loc in sourceTerrainCells)
                Assert.Contains(loc, hydratedTerrainCells);
        }

        [Fact]
        public void Hydrated_World_Has_Same_NonTerrain_Entity_Ids_At_Same_Positions()
        {
            var (source, recipe) = BuildCanonicalWorld(seed: 9001);
            var snapshot = WorldSnapshotBuilder.SnapshotOf(source, recipe, "w1", "m1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 });

            var hydrated = new SnapshotWorldBuilder(snapshot, BuildRegistry()).Build();

            // Source non-terrain entities (the ones the snapshot captures): everything
            // except Terrain and Character.
            var sourceById = source.Entities.Values
                .Where(e => e is not Terrain && e is not Character)
                .ToDictionary(e => e.EntityId);
            var hydratedById = hydrated.Entities.Values
                .Where(e => e is not Terrain && e is not Character)
                .ToDictionary(e => e.EntityId);

            Assert.Equal(sourceById.Count, hydratedById.Count);
            foreach (var (id, src) in sourceById)
            {
                Assert.True(hydratedById.TryGetValue(id, out var hyd),
                    $"Entity {id} ({src.GetType().Name}) missing from hydrated world");
                Assert.Equal(src.Get<WorldLocation>(), hyd!.Get<WorldLocation>());
                Assert.Equal(src.GetType(), hyd.GetType());
            }
        }

        [Fact]
        public void Two_Hydrations_From_Same_Snapshot_Are_Independent()
        {
            var (source, recipe) = BuildCanonicalWorld(seed: 42);
            var snapshot = WorldSnapshotBuilder.SnapshotOf(source, recipe, "w1", "m1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 });

            var registry = BuildRegistry();
            var a = new SnapshotWorldBuilder(snapshot, registry).Build();
            var b = new SnapshotWorldBuilder(snapshot, registry).Build();

            // Pick the first non-terrain, non-character entity in A and remove it.
            var victim = a.Entities.Values
                .FirstOrDefault(e => e is not Terrain && e is not Character);
            if (victim is null)
                return; // nothing to test — empty world; the assertion above already covers parity.

            var victimId = victim.EntityId;
            Assert.True(a.TryRemoveEntity(victimId));

            // B must still have it. Phase 1 semantics: independent instances.
            Assert.True(b.Entities.ContainsKey(victimId),
                "Mutation in one hydrated world leaked into another — phase 1 hydration should produce independent instances.");
        }

        [Fact]
        public void Hydrated_Door_Preserves_Open_Closed_State()
        {
            var (source, recipe) = BuildCanonicalWorld(seed: 12345);

            // Find a door in the source world and force it open.
            var door = source.Entities.Values
                .FirstOrDefault(e => e is Door);
            if (door is null)
                return; // generator didn't place a door for this seed; skip.

            var opens = door.Get<Aetherium.Components.OpensAndCloses>();
            Assert.NotNull(opens);
            opens!.IsOpen = true;

            var snapshot = WorldSnapshotBuilder.SnapshotOf(source, recipe, "w1", "m1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 });
            var hydrated = new SnapshotWorldBuilder(snapshot, BuildRegistry()).Build();

            Assert.True(hydrated.Entities.TryGetValue(door.EntityId, out var hydratedDoor));
            var hydratedOpens = hydratedDoor!.Get<Aetherium.Components.OpensAndCloses>();
            Assert.NotNull(hydratedOpens);
            Assert.True(hydratedOpens!.IsOpen,
                "Door open state should round-trip through the snapshot's properties bag.");
        }
    }
}
