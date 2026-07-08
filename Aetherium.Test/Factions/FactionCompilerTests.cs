using System.Collections.Generic;
using NUnit.Framework;
using Aetherium.Server.Factions;
using Aetherium.Model.Factions;

namespace Aetherium.Test.Factions
{
    /// <summary>
    /// Verifies "Per-World Faction Config" and "Faction Relations As Data"
    /// (openspec/changes/wire-factions-live/specs/factions/spec.md): the <see cref="FactionCompiler"/>
    /// turns pure-data <see cref="FactionConfig"/> content into the runtime registry/relations tier.
    /// </summary>
    [TestFixture]
    public class FactionCompilerTests
    {
        [Test]
        public void CompileRegistry_ProducesFactionsWithWorkingDoctrines()
        {
            var defs = new List<FactionDefinition>
            {
                new()
                {
                    Id = "town", Name = "Rivertown", Tags = new List<string> { "settlement" },
                    DoctrineDeltas = new Dictionary<string, double> { ["kill:zombie"] = 5, ["kill:townsfolk"] = -50 },
                },
            };

            var registry = new FactionCompiler().CompileRegistry(defs);

            Assert.That(registry.TryGet("town", out var town), Is.True);
            Assert.That(town!.Name, Is.EqualTo("Rivertown"));
            Assert.That(town.Tags, Does.Contain("settlement"));
            Assert.That(town.Doctrine.DeltaFor("kill:zombie"), Is.EqualTo(5));
            Assert.That(town.Doctrine.DeltaFor("kill:townsfolk"), Is.EqualTo(-50));
            Assert.That(town.Doctrine.DeltaFor("kill:unrelated"), Is.EqualTo(0), "A tag the doctrine has no rule for must be a no-op.");
        }

        [Test]
        public void CompileRelations_DirectedAndMutual()
        {
            var defs = new List<FactionRelationDefinition>
            {
                // Directed: the vassal answers to the empire, not vice versa.
                new() { FromFactionId = "vassal", ToFactionId = "empire", Disposition = FactionDispositionKind.Subordinate, Mutual = false },
                // Mutual: war cuts both ways.
                new() { FromFactionId = "town", ToFactionId = "cult", Disposition = FactionDispositionKind.War, Mutual = true },
            };

            var relations = new FactionCompiler().CompileRelations(defs);

            Assert.That(relations.GetDisposition("vassal", "empire"), Is.EqualTo(FactionDisposition.Subordinate));
            Assert.That(relations.GetDisposition("empire", "vassal"), Is.EqualTo(FactionDisposition.Neutral),
                "A directed relation must not be mirrored.");
            Assert.That(relations.GetDisposition("town", "cult"), Is.EqualTo(FactionDisposition.War));
            Assert.That(relations.GetDisposition("cult", "town"), Is.EqualTo(FactionDisposition.War),
                "A mutual relation must be set in both directions.");
        }

        [Test]
        public void CompileRegistry_Null_ProducesEmptyRegistry()
        {
            var registry = new FactionCompiler().CompileRegistry(null);
            Assert.That(registry.All, Is.Empty);
            Assert.That(registry.TryGet("anything", out _), Is.False);
        }
    }
}
