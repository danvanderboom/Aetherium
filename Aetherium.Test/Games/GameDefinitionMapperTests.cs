using System.Collections.Generic;
using NUnit.Framework;
using Aetherium.Model.Combat;
using Aetherium.Model.Abilities;
using Aetherium.Model.Factions;
using Aetherium.Model.Games;
using Aetherium.Model.Progression;
using Aetherium.Server.Games;

namespace Aetherium.Test.Games
{
    /// <summary>
    /// Verifies (part of) "Game Instance Creation"
    /// (openspec/changes/add-game-definition-loader/specs/game-definitions/spec.md): the pure
    /// definition→CreateWorldRequest mapping carries every field — including all four gameplay
    /// configs and the definition id/version bookkeeping — onto the existing creation contract.
    /// </summary>
    [TestFixture]
    public class GameDefinitionMapperTests
    {
        [Test]
        public void MapsEveryField_ToCreateWorldRequest()
        {
            var death = new DeathPolicy { Permadeath = true };
            var abilities = new AbilityConfig();
            var progression = new ProgressionConfig();
            var factions = new FactionConfig();
            var content = new Aetherium.Model.Content.ContentConfig();
            var rules = new Aetherium.Model.Eca.EcaConfig();

            var definition = new GameDefinition
            {
                Id = "emberfall",
                Name = "Emberfall",
                Version = "1.2.3",
                Description = "A fantasy RPG.",
                World = new GameWorldDefinition
                {
                    GeneratorType = "maze",
                    GeneratorParameters = new Dictionary<string, object> { ["roomCount"] = 12 },
                    Size = new Aetherium.Model.Worlds.WorldDimensions { Width = 60, Height = 50, Depth = 2 },
                    MaxPlayers = 40,
                    NarrativeId = "ember-narrative",
                },
                Death = death,
                Abilities = abilities,
                Progression = progression,
                Factions = factions,
                Content = content,
                Rules = rules,
                Player = new GamePlayerDefinition { StartingCurrency = 250.0 },
            };

            var request = GameDefinitionMapper.ToCreateWorldRequest(definition);

            Assert.That(request.Name, Is.EqualTo("Emberfall"));
            Assert.That(request.Description, Is.EqualTo("A fantasy RPG."));
            Assert.That(request.GeneratorType, Is.EqualTo("maze"));
            Assert.That(request.GeneratorParameters["roomCount"], Is.EqualTo(12));
            Assert.That(request.NarrativeId, Is.EqualTo("ember-narrative"));
            Assert.That(request.MaxPlayers, Is.EqualTo(40));
            Assert.That(request.Size, Is.Not.Null);
            Assert.That((request.Size!.Width, request.Size.Height, request.Size.Depth), Is.EqualTo((60, 50, 2)));
            Assert.That(request.DeathPolicy, Is.SameAs(death));
            Assert.That(request.AbilityConfig, Is.SameAs(abilities));
            Assert.That(request.ProgressionConfig, Is.SameAs(progression));
            Assert.That(request.FactionConfig, Is.SameAs(factions));
            Assert.That(request.ContentConfig, Is.SameAs(content));
            Assert.That(request.EcaConfig, Is.SameAs(rules));
            Assert.That(request.StartingCurrency, Is.EqualTo(250.0));
            Assert.That(request.GameDefinitionId, Is.EqualTo("emberfall"));
            Assert.That(request.GameDefinitionVersion, Is.EqualTo("1.2.3"));
        }

        [Test]
        public void InstanceName_OverridesDefinitionName_WhenProvided()
        {
            var definition = new GameDefinition { Id = "g", Name = "Game", Version = "1.0.0" };

            Assert.That(GameDefinitionMapper.ToCreateWorldRequest(definition).Name, Is.EqualTo("Game"));
            Assert.That(GameDefinitionMapper.ToCreateWorldRequest(definition, "Game #2").Name, Is.EqualTo("Game #2"));
        }
    }
}
