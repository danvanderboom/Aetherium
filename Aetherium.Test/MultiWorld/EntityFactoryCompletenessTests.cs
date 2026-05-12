using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Server.MultiWorld;
using Xunit;

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// Defense against snapshot rot: when a new concrete Entity subclass lands in
    /// Aetherium.Server.Entities, EntityFactory needs to be able to construct it
    /// (either via parameterless ctor, default-value ctor, or an explicit special-
    /// case branch). This test scans the assembly and fails if any Entity subclass
    /// isn't creatable from a synthetic placement.
    ///
    /// Intentional exclusions: Terrain (regenerated from recipe), Character
    /// (live players join dynamically rather than via snapshot).
    /// </summary>
    public class EntityFactoryCompletenessTests
    {
        private static readonly HashSet<Type> Excluded = new()
        {
            typeof(Terrain),
            typeof(Character),
        };

        // Tile types that engine entities (Monster, Zombie) look up in their
        // constructors. The completeness test uses a world that already has these
        // registered — matching the situation an entity sees in production after
        // the worldgen orchestrator has set up the world.
        private static World CreateTestWorld()
        {
            var world = new World();
            world.TileTypes["Monster"] = new TileType { Name = "Monster" };
            world.TileTypes["Player"] = new TileType { Name = "Player" };
            return world;
        }

        [Fact]
        public void Every_Concrete_Entity_Subclass_Has_A_Working_Factory_Path()
        {
            var entityAssembly = typeof(Door).Assembly;
            var concreteEntityTypes = entityAssembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(Entity).IsAssignableFrom(t))
                .Where(t => !Excluded.Contains(t))
                .ToList();

            Assert.NotEmpty(concreteEntityTypes);

            // Use a real world for the factory's parameter; some entity types
            // (Monster, Zombie) need a World reference and look up tile types.
            var world = CreateTestWorld();
            var factory = new EntityFactory(world);

            var failures = new List<string>();
            foreach (var type in concreteEntityTypes)
            {
                var placement = new EntityPlacement
                {
                    EntityId = Guid.NewGuid().ToString(),
                    TypeName = type.Name,
                    X = 1,
                    Y = 1,
                    Z = 0,
                    Properties = new Dictionary<string, string>(),
                };

                Entity? entity;
                try
                {
                    entity = factory.Create(placement);
                }
                catch (Exception ex)
                {
                    failures.Add($"{type.Name}: threw {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                if (entity is null)
                {
                    failures.Add($"{type.Name}: EntityFactory.Create returned null. Add a special-case branch or a parameterless ctor.");
                    continue;
                }

                if (entity.EntityId != placement.EntityId)
                    failures.Add($"{type.Name}: EntityId not overridden to placement value");
            }

            Assert.True(failures.Count == 0,
                "EntityFactory cannot materialize all concrete Entity subclasses:\n" + string.Join("\n", failures));
        }
    }
}
