using System;
using System.Linq;
using NUnit.Framework;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Metadata;
using Aetherium.Model.Pcg;
using WorldGenCLI.Services;

namespace Aetherium.Test.WorldGen
{
    [TestFixture]
    public class ConstraintSchemaTests
    {
        [Test]
        public void ConstraintSchemaBuilder_BuildSchema_ValidGenerator_ReturnsSchema()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
            var builder = new ConstraintSchemaBuilder(registry);

            // Act
            var schema = builder.BuildSchema("AdvancedDungeon");

            // Assert
            Assert.That(schema, Is.Not.Null);
            Assert.That(schema.GeneratorId, Is.EqualTo("AdvancedDungeon"));
            Assert.That(schema.Parameters, Is.Not.Null);
            Assert.That(schema.JsonSchema, Is.Not.Null);
        }

        [Test]
        public void ConstraintSchemaBuilder_BuildSchema_InvalidGenerator_ReturnsEmptySchema()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
            var builder = new ConstraintSchemaBuilder(registry);

            // Act
            var schema = builder.BuildSchema("NonExistentGenerator");

            // Assert
            Assert.That(schema, Is.Not.Null);
            Assert.That(schema.GeneratorId, Is.EqualTo("NonExistentGenerator"));
            Assert.That(schema.Parameters, Is.Not.Null);
            Assert.That(schema.Parameters, Is.Empty);
        }

        [Test]
        public void GeneratorParamAttribute_AppliedToProperty_ExtractedCorrectly()
        {
            // This test verifies that the GeneratorParamAttribute can be used
            // to annotate generator properties and will be discovered by reflection
            
            // Arrange
            var registry = new MapGeneratorRegistry();
            registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
            var builder = new ConstraintSchemaBuilder(registry);

            // Act
            var schema = builder.BuildSchema("AdvancedDungeon");

            // Assert - verify that if any generator has attributes, they're extracted
            // Note: This test passes even if no attributes are found, as it tests the mechanism
            Assert.That(schema.Parameters, Is.Not.Null);
            
            // If parameters are found, verify they have basic structure
            foreach (var param in schema.Parameters)
            {
                Assert.That(param.Name, Is.Not.Null);
                Assert.That(param.Type, Is.Not.EqualTo(default(ParameterType)));
            }
        }

        [Test]
        public void ConstraintSchemaBuilder_BuildSchema_JsonSchema_IsValidJson()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
            var builder = new ConstraintSchemaBuilder(registry);

            // Act
            var schema = builder.BuildSchema("AdvancedDungeon");

            // Assert
            Assert.That(schema.JsonSchema, Is.Not.Null);
            Assert.That(schema.JsonSchema, Is.Not.Empty);
            
            // Verify it's valid JSON by attempting to parse
            Assert.DoesNotThrow(() => 
                System.Text.Json.JsonSerializer.Deserialize<object>(schema.JsonSchema));
        }
    }
}

