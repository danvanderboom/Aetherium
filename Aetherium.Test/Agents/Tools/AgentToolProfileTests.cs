using System.Linq;
using NUnit.Framework;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.Movement;
using Aetherium.Server.Agents.Tools.Interaction;
using Aetherium.Server.Agents.Tools.Narrative;

namespace Aetherium.Test.Agents.Tools
{
    [TestFixture]
    public class AgentToolProfileTests
    {
        [Test]
        public void Explorer_ShouldHaveBasicMovementCapability()
        {
            // Arrange
            var profile = AgentToolProfile.Explorer;

            // Assert
            Assert.That(profile.GrantedCapabilities, Does.Contain("basic_movement"));
            Assert.That(profile.GrantedCapabilities, Does.Contain("vision"));
        }

        [Test]
        public void Explorer_ShouldNotHaveInventoryAccess()
        {
            // Arrange
            var profile = AgentToolProfile.Explorer;

            // Assert
            Assert.That(profile.GrantedCapabilities, Does.Not.Contain("inventory_access"));
        }

        [Test]
        public void FullAccess_ShouldHavePlayerCapabilities()
        {
            // Arrange
            var profile = AgentToolProfile.FullAccess;

            // Assert
            Assert.That(profile.GrantedCapabilities, Does.Contain("basic_movement"));
            Assert.That(profile.GrantedCapabilities, Does.Contain("inventory_access"));
            Assert.That(profile.GrantedCapabilities, Does.Contain("vision"));
            Assert.That(profile.GrantedCapabilities, Does.Contain("interaction"));
        }

        [Test]
        public void Admin_ShouldHaveAdminCapability()
        {
            // Arrange
            var profile = AgentToolProfile.Admin;

            // Assert
            Assert.That(profile.GrantedCapabilities, Does.Contain("admin"));
            Assert.That(profile.GrantedCapabilities, Does.Contain("world_edit"));
        }

        [Test]
        public void WorldBuilder_ShouldHaveWorldEditCapability()
        {
            // Arrange
            var profile = AgentToolProfile.WorldBuilder;

            // Assert
            Assert.That(profile.GrantedCapabilities, Does.Contain("world_edit"));
            Assert.That(profile.GrantedCapabilities, Does.Contain("world_generate"));
        }

        [Test]
        public void NarrativeDesigner_ShouldHaveNarrativeEditCapability()
        {
            // Arrange
            var profile = AgentToolProfile.NarrativeDesigner;

            // Assert
            Assert.That(profile.GrantedCapabilities, Does.Contain("narrative_edit"));
        }

        [Test]
        public void Player_ShouldAllowQuestTools()
        {
            // The GameHub enforces the Player profile at its boundary, so the quest tools must be
            // reachable by it for players (and player-profile agents) to accept/list/log quests.
            var profile = AgentToolProfile.Player;

            Assert.That(profile.IsToolAllowed(new ListQuestsTool()), Is.True);
            Assert.That(profile.IsToolAllowed(new AcceptQuestTool()), Is.True);
            Assert.That(profile.IsToolAllowed(new QuestLogTool()), Is.True);
        }

        [Test]
        public void Explorer_ShouldNotAllowQuestTools()
        {
            // Quest tools live in the "quest" category, which Explorer (movement/vision only) lacks.
            var profile = AgentToolProfile.Explorer;

            Assert.That(profile.IsToolAllowed(new AcceptQuestTool()), Is.False);
            Assert.That(profile.IsToolAllowed(new QuestLogTool()), Is.False);
        }

        [Test]
        public void IsToolAllowed_ShouldRespectDenyList()
        {
            // Arrange
            var profile = new AgentToolProfile
            {
                DeniedTools = new() { "move" },
                AllowedCategories = new() { "movement" }
            };
            
            var moveTool = new MoveTool();

            // Act
            var isAllowed = profile.IsToolAllowed(moveTool);

            // Assert
            Assert.That(isAllowed, Is.False, "Denied tools should not be allowed");
        }

        [Test]
        public void IsToolAllowed_ShouldAllowExplicitlyAllowedTools()
        {
            // Arrange
            var profile = new AgentToolProfile
            {
                AllowedTools = new() { "move" }
            };
            
            var moveTool = new MoveTool();

            // Act
            var isAllowed = profile.IsToolAllowed(moveTool);

            // Assert
            Assert.That(isAllowed, Is.True);
        }

        [Test]
        public void IsToolAllowed_ShouldAllowToolsByCategory()
        {
            // Arrange
            var profile = new AgentToolProfile
            {
                AllowedCategories = new() { "movement" },
                GrantedCapabilities = new() { "basic_movement" } // Required for MoveTool
            };
            
            var moveTool = new MoveTool();

            // Act
            var isAllowed = profile.IsToolAllowed(moveTool);

            // Assert
            Assert.That(isAllowed, Is.True);
        }

        [Test]
        public void IsToolAllowed_ShouldCheckCapabilities()
        {
            // Arrange
            var profile = new AgentToolProfile
            {
                GrantedCapabilities = new() { "basic_movement" }
            };
            
            var moveTool = new MoveTool();

            // Act
            var isAllowed = profile.IsToolAllowed(moveTool);

            // Assert
            Assert.That(isAllowed, Is.True);
        }

        [Test]
        public void IsToolAllowed_ShouldDenyToolsWithoutCapabilities()
        {
            // Arrange
            var profile = new AgentToolProfile
            {
                GrantedCapabilities = new() { "something_else" }
            };
            
            var moveTool = new MoveTool();

            // Act
            var isAllowed = profile.IsToolAllowed(moveTool);

            // Assert
            Assert.That(isAllowed, Is.False, "Tools without required capabilities should not be allowed");
        }

        [Test]
        public void GetPredefinedProfile_ShouldReturnExplorer()
        {
            // Act
            var profile = AgentToolProfile.GetPredefinedProfile("explorer");

            // Assert
            Assert.That(profile.ProfileName, Is.EqualTo("explorer"));
        }

        [Test]
        public void GetPredefinedProfile_ShouldReturnFullAccess()
        {
            // Act - FullAccess is now mapped to Player
            var profile1 = AgentToolProfile.GetPredefinedProfile("full");
            var profile2 = AgentToolProfile.GetPredefinedProfile("fullaccess");

            // Assert - both should return Player profile
            Assert.That(profile1.ProfileName, Is.EqualTo("player"));
            Assert.That(profile2.ProfileName, Is.EqualTo("player"));
        }

        [Test]
        public void GetPredefinedProfile_ShouldDefaultToPlayer()
        {
            // Act
            var profile = AgentToolProfile.GetPredefinedProfile("nonexistent");

            // Assert - default profile is now Player
            Assert.That(profile.ProfileName, Is.EqualTo("player"));
        }
    }
}

