using NUnit.Framework;
using Aetherium.Entities;

namespace Aetherium.Test.Entities
{
    /// <summary>
    /// Covers the shared entity factory used by both the world-building SpawnEntityTool and the
    /// prefab stamper for simple name → entity resolution.
    /// </summary>
    [TestFixture]
    public class SpawnableEntityFactoryTests
    {
        [Test]
        public void TryCreate_KnownType_CreatesInstance()
        {
            Assert.That(SpawnableEntityFactory.TryCreate("Item", out var item), Is.True);
            Assert.That(item, Is.InstanceOf<Item>());
        }

        [Test]
        public void TryCreate_IsCaseInsensitive()
        {
            Assert.That(SpawnableEntityFactory.TryCreate("dOoR", out var door), Is.True);
            Assert.That(door, Is.InstanceOf<Door>());
        }

        [Test]
        public void TryCreate_UnknownType_ReturnsFalse()
        {
            Assert.That(SpawnableEntityFactory.TryCreate("Dragon", out _), Is.False);
        }

        [Test]
        public void TryCreate_EmptyOrNull_ReturnsFalse()
        {
            Assert.That(SpawnableEntityFactory.TryCreate("", out _), Is.False);
            Assert.That(SpawnableEntityFactory.TryCreate(null, out _), Is.False);
        }

        [Test]
        public void IsKnownType_ReflectsSupportedSet()
        {
            Assert.That(SpawnableEntityFactory.IsKnownType("Item"), Is.True);
            Assert.That(SpawnableEntityFactory.IsKnownType("NotAnEntity"), Is.False);
        }

        [Test]
        public void SupportedTypeNames_IncludesConcreteEntities_ExcludesTerrain()
        {
            var names = SpawnableEntityFactory.SupportedTypeNames;
            Assert.That(names, Does.Contain("Item"));
            Assert.That(names, Does.Contain("Door"));
            // Terrain has no parameterless constructor (created via setterrain), so it is excluded.
            Assert.That(names, Does.Not.Contain("Terrain"));
        }
    }
}
