using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Server.MultiWorld;
using Aetherium.WorldGen;
using Passes = Aetherium.WorldGen.Passes;

namespace Aetherium.WorldBuilders
{
    /// <summary>
    /// Hydrates a <see cref="World"/> from a <see cref="WorldSnapshot"/>.
    ///
    /// <para>
    /// Phase 1: regenerate the world by replaying the snapshot's
    /// <see cref="WorldRecipe"/> through <see cref="WorldGenerationOrchestrator"/>,
    /// then replace the generator-placed entities with the snapshot's entity
    /// placements (so entity IDs match the grain's canonical world). Terrain comes
    /// from the recipe; everything else comes from the snapshot.
    /// </para>
    ///
    /// <para>
    /// Phase 1 semantics: the resulting <see cref="World"/> is an independent
    /// instance. Mutations to it do NOT propagate to the grain's canonical world
    /// or to other sessions hydrated from the same snapshot. Phase 2 will replace
    /// this with grain-authoritative mutation and delta-based session mirrors.
    /// </para>
    /// </summary>
    public sealed class SnapshotWorldBuilder : WorldBuilder
    {
        private readonly WorldSnapshot _snapshot;
        private readonly MapGeneratorRegistry _generatorRegistry;

        public SnapshotWorldBuilder(WorldSnapshot snapshot, MapGeneratorRegistry generatorRegistry)
        {
            _snapshot = snapshot;
            _generatorRegistry = generatorRegistry;
        }

        public override World Build()
        {
            var recipe = _snapshot.Recipe;

            var request = new WorldGenerationRequest
            {
                LayoutGenerator = recipe.GeneratorType,
                Width = recipe.Width > 0 ? recipe.Width : _snapshot.Size.Width,
                Height = recipe.Height > 0 ? recipe.Height : _snapshot.Size.Height,
                Levels = recipe.Levels > 0 ? recipe.Levels : _snapshot.Size.Depth,
                Seed = recipe.Seed,
                GeneratorVersion = recipe.GeneratorVersion,
                Template = recipe.Template,
                Parameters = new System.Collections.Generic.Dictionary<string, string>(recipe.Parameters, System.StringComparer.OrdinalIgnoreCase),
            };

            var passes = BuildPasses(recipe.Template);
            var orchestrator = new WorldGenerationOrchestrator(_generatorRegistry, passes);
            var result = orchestrator.Generate(request);

            if (!result.Success || result.World is null)
            {
                var details = new List<string>(result.Errors);
                if (result.Validation?.Errors != null)
                    details.AddRange(result.Validation.Errors);
                throw new System.InvalidOperationException(
                    $"Snapshot hydration failed during terrain regeneration: {string.Join(", ", details)}");
            }

            var world = result.World;

            // Strip ALL generator-placed non-terrain entities — including Characters,
            // now that the population pass places monsters (which are Characters).
            // They were re-rolled with fresh Guid IDs; we overlay the snapshot's
            // entities (with the grain's authoritative IDs) immediately after, so
            // deltas that reference an entity by ID resolve identically on every
            // session mirror. Keeping regenerated Characters here would duplicate
            // every monster on join.
            var stripIds = world.Entities.Values
                .Where(e => e is not Terrain)
                .Select(e => e.EntityId)
                .ToList();
            foreach (var id in stripIds)
                world.TryRemoveEntity(id);

            // Overlay snapshot entities with their captured IDs and component state.
            // Track drops loudly: an entity in the snapshot that fails to hydrate becomes a
            // permanent GHOST in this mirror — present and lethal in the canonical world,
            // invisible here, and unhealable because ApplyEntityMoved skips deltas for
            // unknown entity ids (observed live as damage from empty floor).
            var factory = new EntityFactory(world);
            var created = 0;
            var dropped = new List<string>();
            foreach (var placement in _snapshot.Entities)
            {
                var entity = factory.Create(placement);
                if (entity is null)
                {
                    dropped.Add($"{placement.TypeName}#{placement.EntityId}");
                    continue;
                }

                // The factory has already overridden the EntityId and set the location.
                // AddEntity will throw if the ID is somehow already taken — which would
                // be a real bug (duplicate IDs in snapshot) and we want it loud.
                world.AddEntity(entity);
                created++;
            }

            System.Console.WriteLine(
                $"[SnapshotWorldBuilder] hydrated {created}/{_snapshot.Entities.Count} snapshot entities" +
                (dropped.Count > 0 ? $"; DROPPED {dropped.Count}: {string.Join(", ", dropped.Take(8))}" : string.Empty));

            World = world;
            return world;
        }

        // Must match GameMapGrain exactly — both delegate to the shared catalog, so
        // regeneration always replays the same pipeline the grain used originally.
        private static IWorldGenerationPass[] BuildPasses(WorldGenerationTemplate template)
            => WorldGenerationPassCatalog.BuildPasses(template);
    }
}
