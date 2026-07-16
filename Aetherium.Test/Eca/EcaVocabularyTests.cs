using System.Linq;
using NUnit.Framework;
using Aetherium.Model.Eca;
using Aetherium.Server.Eca;

namespace Aetherium.Test.Eca
{
    /// <summary>
    /// Verifies "Reflectable Vocabulary Registry"
    /// (openspec/changes/add-eca-scripting/specs/eca-scripting/spec.md): the vocabulary is
    /// programmatically enumerable, internally consistent, and agrees with the runtime's tile-id
    /// constants — so validation, docs, and evaluation can never name a kind differently.
    /// </summary>
    [TestFixture]
    public class EcaVocabularyTests
    {
        [Test]
        public void EveryTileHasUniqueIdAndParameters()
        {
            var all = EcaVocabulary.All;
            Assert.That(all, Is.Not.Empty);

            var ids = all.Select(d => d.Id).ToList();
            Assert.That(ids, Is.Unique, "Tile ids must be unique.");

            foreach (var tile in all)
            {
                Assert.That(tile.Id, Is.Not.Empty);
                Assert.That(tile.Description, Is.Not.Empty, $"Tile '{tile.Id}' needs a description (it's the doc text).");
                var paramNames = tile.Parameters.Select(p => p.Name).ToList();
                Assert.That(paramNames, Is.Unique, $"Tile '{tile.Id}' has duplicate parameter names.");
                foreach (var p in tile.Parameters)
                    Assert.That(p.Description, Is.Not.Empty, $"Parameter '{tile.Id}.{p.Name}' needs a description.");
            }
        }

        [Test]
        public void RuntimeKinds_MatchVocabularyIds()
        {
            // The id constants the runtime and validator switch on must be exactly the vocabulary's
            // set — a tile added to one but not the other is a bug this test catches.
            var runtimeIds = new[]
            {
                CreatureDiedTrigger.Id,
                CreatureTypeIsCondition.Id,
                ChanceCondition.Id,
                SpawnCreatureAction.Id,
                DealDamageAction.Id,
                ApplyStatusAction.Id,
            };

            Assert.That(EcaVocabulary.All.Select(d => d.Id), Is.EquivalentTo(runtimeIds));
        }

        [Test]
        public void Roles_ArePartitionedAsExpected()
        {
            Assert.That(EcaVocabulary.ByRole(EcaTileRole.Trigger).Select(d => d.Id), Is.EquivalentTo(new[] { "creature_died" }));
            Assert.That(EcaVocabulary.ByRole(EcaTileRole.Condition).Select(d => d.Id), Is.EquivalentTo(new[] { "creature_type_is", "chance" }));
            Assert.That(EcaVocabulary.ByRole(EcaTileRole.Action).Select(d => d.Id), Is.EquivalentTo(new[] { "spawn_creature", "deal_damage", "apply_status" }));
        }
    }
}
