using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Agents.Tools.Movement;
using Aetherium.Server.Agents.Tools.Interaction;
using Aetherium.Server.Agents.Tools.Vision;
using Aetherium.Server;
using Aetherium.WorldBuilders;
using Aetherium.Entities;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Model;

namespace Aetherium.Test.Agents.Tools.Integration
{
    [TestFixture]
    public class ToolSystemIntegrationTests
    {
        private ServiceCollection _services;
        private ServiceProvider _serviceProvider;
        private AgentToolRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _services = new ServiceCollection();
            _serviceProvider = _services.BuildServiceProvider();
            _registry = new AgentToolRegistry(_serviceProvider);
            _registry.DiscoverTools(typeof(MoveTool).Assembly);
        }

        [TearDown]
        public void TearDown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task FullWorkflow_AgentCanMoveAndPickupItems()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            TestWorldMovement.CarveOpenArea(session); // movement is validated (P0-1)
            var interactionSystem = new InteractionSystem();
            var profile = AgentToolProfile.FullAccess;
            
            var context = new ToolExecutionContext
            {
                SessionId = "test",
                AgentId = "agent1",
                Session = session,
                GrantedCapabilities = profile.GrantedCapabilities,
                ServiceProvider = _serviceProvider
            };

            // Act - Move forward
            var moveTool = _registry.GetTool("move");
            Assert.That(moveTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(moveTool), Is.True);
            
            var moveResult = await moveTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            });

            // Act - Toggle vision
            var visionTool = _registry.GetTool("toggledirectionalvision");
            Assert.That(visionTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(visionTool), Is.True);
            
            var visionResult = await visionTool.ExecuteAsync(context, new Dictionary<string, object>());

            // Assert
            Assert.That(moveResult.Success, Is.True);
            Assert.That(visionResult.Success, Is.True);
        }

        [Test]
        public void ProfileFiltering_ExplorerCannotAccessInventoryTools()
        {
            // Arrange
            var profile = AgentToolProfile.Explorer;
            var pickupTool = _registry.GetTool("pickup");

            // Act & Assert
            Assert.That(pickupTool, Is.Not.Null);
            Assert.That(profile.IsToolAllowed(pickupTool), Is.False, 
                "Explorer profile should not have access to inventory tools");
        }

        [Test]
        public void ProfileFiltering_FullAccessCanAccessAllPlayerTools()
        {
            // Arrange
            var profile = AgentToolProfile.FullAccess;
            var moveTool = _registry.GetTool("move");
            var pickupTool = _registry.GetTool("pickup");
            var visionTool = _registry.GetTool("toggledirectionalvision");

            // Act & Assert
            Assert.That(profile.IsToolAllowed(moveTool), Is.True);
            Assert.That(profile.IsToolAllowed(pickupTool), Is.True);
            Assert.That(profile.IsToolAllowed(visionTool), Is.True);
        }

        [Test]
        public void ProfileFiltering_WorldBuilderCanAccessWorldBuildingTools()
        {
            // Arrange
            var profile = AgentToolProfile.WorldBuilder;
            var spawnTool = _registry.GetTool("spawnentity");
            var modifyTool = _registry.GetTool("modifyentity");

            // Act & Assert
            if (spawnTool != null)
                Assert.That(profile.IsToolAllowed(spawnTool), Is.True);
            if (modifyTool != null)
                Assert.That(profile.IsToolAllowed(modifyTool), Is.True);
        }

        [Test]
        public async Task ErrorHandling_ToolReturnsErrorForInvalidArgs()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var context = new ToolExecutionContext
            {
                Session = session,
                ServiceProvider = _serviceProvider
            };
            var moveTool = _registry.GetTool("move");

            // Act - Invalid direction
            var result = await moveTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["direction"] = "invalid_direction",
                ["distance"] = 1
            });

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.Not.Empty);
        }

        [Test]
        public async Task ToolChaining_MultipleToolsCanBeExecutedSequentially()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            TestWorldMovement.CarveOpenArea(session); // movement is validated (P0-1)
            var context = new ToolExecutionContext
            {
                Session = session,
                ServiceProvider = _serviceProvider,
                GrantedCapabilities = new HashSet<string> { "basic_movement" } // Need capability for movement tools
            };

            // Act - Chain move -> rotate -> move
            var moveTool = _registry.GetTool("move");
            var rotateTool = _registry.GetTool("rotate");

            var result1 = await moveTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            });
            
            var result2 = await rotateTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["clockwise"] = true // Changed from direction to clockwise
            });
            
            var result3 = await moveTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            });

            // Assert
            Assert.That(result1.Success, Is.True);
            Assert.That(result2.Success, Is.True);
            Assert.That(result3.Success, Is.True);
        }

        [Test]
        public void ToolDiscovery_AllCategoriesAreRepresented()
        {
            // Arrange & Act
            var movementTools = _registry.GetToolsByCategory("movement").ToList();
            var inventoryTools = _registry.GetToolsByCategory("inventory").ToList();
            var visionTools = _registry.GetToolsByCategory("vision").ToList();
            var interactionTools = _registry.GetToolsByCategory("interaction").ToList();

            // Assert
            Assert.That(movementTools.Count, Is.GreaterThan(0), "Should have movement tools");
            Assert.That(inventoryTools.Count, Is.GreaterThan(0), "Should have inventory tools");
            Assert.That(visionTools.Count, Is.GreaterThan(0), "Should have vision tools");
            Assert.That(interactionTools.Count, Is.GreaterThan(0), "Should have interaction tools");
        }

        [Test]
        public void ToolRegistry_DoesNotCreateDuplicateInstances()
        {
            // Arrange & Act
            var tool1 = _registry.GetTool("move");
            var tool2 = _registry.GetTool("move");
            var tool3 = _registry.GetTool("move");

            // Assert
            Assert.That(tool1, Is.SameAs(tool2));
            Assert.That(tool2, Is.SameAs(tool3));
        }

        [Test]
        public async Task DualAPISupport_ToolsWorkWithBothSessionAndGrain()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            TestWorldMovement.CarveOpenArea(session); // movement is validated (P0-1)

            // Context 1: Direct session access (GameHub style)
            var hubContext = new ToolExecutionContext
            {
                SessionId = "test",
                ConnectionId = "conn1",
                Session = session,
                ServiceProvider = _serviceProvider,
                GrantedCapabilities = new HashSet<string> { "basic_movement" } // Need capability
            };
            
            // Context 2: Orleans grain style (would have ManagementGrain in real scenario)
            var grainContext = new ToolExecutionContext
            {
                SessionId = "test",
                AgentId = "agent1",
                Session = session,
                ServiceProvider = _serviceProvider,
                GrantedCapabilities = new HashSet<string> { "basic_movement" } // Need capability
            };

            var moveTool = _registry.GetTool("move");
            var args = new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            };

            // Act
            var hubResult = await moveTool.ExecuteAsync(hubContext, args);
            var grainResult = await moveTool.ExecuteAsync(grainContext, args);

            // Assert
            Assert.That(hubResult.Success, Is.True);
            Assert.That(grainResult.Success, Is.True);
        }

        // ========== Interaction Tool Tests ==========

        [Test]
        public async Task InteractionTool_OpenTool_CanOpenDoor()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var interactionSystem = new InteractionSystem();
            var door = new Door();
            door.Set(new WorldLocation(session.ViewLocation!.X, session.ViewLocation!.Y, session.ViewLocation!.Z));
            door.Get<OpensAndCloses>().IsOpen = false;
            door.Get<OpensAndCloses>().IsLocked = false;
            session.World.AddEntity(door);

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "interaction" },
                ServiceProvider = _serviceProvider
            };

            var openTool = _registry.GetTool("open");
            Assert.That(openTool, Is.Not.Null);

            // Act
            var result = await openTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["targetEntityId"] = door.EntityId
            });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(door.Get<OpensAndCloses>().IsOpen, Is.True);
        }

        [Test]
        public async Task InteractionTool_CloseTool_CanCloseDoor()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var interactionSystem = new InteractionSystem();
            var door = new Door();
            door.Set(new WorldLocation(session.ViewLocation!.X, session.ViewLocation!.Y, session.ViewLocation!.Z));
            door.Get<OpensAndCloses>().IsOpen = true;
            door.Get<OpensAndCloses>().IsLocked = false;
            door.Clear<ObstructsView>();
            session.World.AddEntity(door);

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "interaction" },
                ServiceProvider = _serviceProvider
            };

            var closeTool = _registry.GetTool("close");
            Assert.That(closeTool, Is.Not.Null);

            // Act
            var result = await closeTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["targetEntityId"] = door.EntityId
            });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(door.Get<OpensAndCloses>().IsOpen, Is.False);
        }

        [Test]
        public async Task InteractionTool_UseTool_CanUseKeyOnDoor()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var interactionSystem = new InteractionSystem();
            var door = new Door();
            var keyId = "test-key-123";
            door.Set(new WorldLocation(session.ViewLocation!.X, session.ViewLocation!.Y, session.ViewLocation!.Z));
            door.Get<OpensAndCloses>().IsLocked = true;
            door.Get<OpensAndCloses>().KeyShape = keyId;
            session.World.AddEntity(door);

            var key = new KeyItem(keyId);
            session.Player!.Get<Inventory>()!.TryAdd(key.EntityId, key);

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "inventory_access", "interaction" },
                ServiceProvider = _serviceProvider
            };

            var useTool = _registry.GetTool("use");
            Assert.That(useTool, Is.Not.Null);

            // Act
            var result = await useTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["itemEntityId"] = key.EntityId,
                ["onEntityId"] = door.EntityId
            });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(door.Get<OpensAndCloses>().IsLocked, Is.False);
        }

        [Test]
        public async Task InteractionTool_DropTool_CanDropItemFromInventory()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var interactionSystem = new InteractionSystem();
            var item = new KeyItem("test-key");
            session.Player!.Get<Inventory>()!.TryAdd(item.EntityId, item);

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "inventory_access" },
                ServiceProvider = _serviceProvider
            };

            var dropTool = _registry.GetTool("drop");
            Assert.That(dropTool, Is.Not.Null);
            Assert.That(session.Player.Get<Inventory>()!.Items.ContainsKey(item.EntityId), Is.True);

            // Act
            var result = await dropTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["itemEntityId"] = item.EntityId
            });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(session.Player.Get<Inventory>()!.Items.ContainsKey(item.EntityId), Is.False);
            Assert.That(session.World.Entities.ContainsKey(item.EntityId), Is.True);
        }

        // ========== Vision Tool Tests ==========

        [Test]
        public async Task VisionTool_SetFieldOfViewTool_CanChangeFOV()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var hasHeading = session.Player!.Get<HasHeading>();
            var initialFOV = hasHeading?.FieldOfViewDegrees ?? 90;

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "vision" },
                ServiceProvider = _serviceProvider
            };

            var fovTool = _registry.GetTool("setfieldofview");
            Assert.That(fovTool, Is.Not.Null);

            // Act
            var result = await fovTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["degrees"] = 120
            });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(hasHeading!.FieldOfViewDegrees, Is.EqualTo(120));
        }

        [Test]
        public async Task VisionTool_SetLightingModeTool_CanChangeLightingMode()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var initialMode = session.CurrentLightingMode;

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "vision" },
                ServiceProvider = _serviceProvider
            };

            var lightingTool = _registry.GetTool("setlightingmode");
            Assert.That(lightingTool, Is.Not.Null);

            // Act
            var result = await lightingTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["mode"] = "Sunlight"
            });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(session.CurrentLightingMode, Is.EqualTo(LightingMode.Sunlight));
            Assert.That(session.CurrentLightingMode, Is.Not.EqualTo(initialMode));
        }

        [Test]
        public async Task VisionTool_SetVisionModeTool_CanChangeVisionMode()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var initialMode = session.CurrentVisionMode;

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "vision" },
                ServiceProvider = _serviceProvider
            };

            var visionModeTool = _registry.GetTool("setvisionmode");
            Assert.That(visionModeTool, Is.Not.Null);

            // Act
            var result = await visionModeTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["mode"] = "Infrared"
            });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(session.CurrentVisionMode, Is.EqualTo(VisionMode.Infrared));
            Assert.That(session.CurrentVisionMode, Is.Not.EqualTo(initialMode));
        }

        // ========== Movement Tool Tests ==========

        [Test]
        public async Task MovementTool_ChangeLevelTool_CanChangeZLevel()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            // Level changes are validated (P0-1): the player must be on stairs
            // with an open landing above.
            TestWorldMovement.CarveStairsAtPlayer(session, deltaZ: 1);
            var initialZ = session.ViewLocation!.Z;

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "basic_movement" },
                ServiceProvider = _serviceProvider
            };

            var changeLevelTool = _registry.GetTool("changelevel");
            Assert.That(changeLevelTool, Is.Not.Null);

            // Act
            var result = await changeLevelTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["delta"] = 1
            });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(session.ViewLocation!.Z, Is.EqualTo(initialZ + 1));
        }

        [Test]
        public async Task MovementTool_RotateTool_ChangesHeading()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var initialHeading = session.HeadingDegrees;

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "basic_movement" },
                ServiceProvider = _serviceProvider
            };

            var rotateTool = _registry.GetTool("rotate");
            Assert.That(rotateTool, Is.Not.Null);

            // Act
            var result = await rotateTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["degrees"] = 90
            });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(session.HeadingDegrees, Is.Not.EqualTo(initialHeading));
            Assert.That((session.HeadingDegrees - initialHeading + 360) % 360, Is.EqualTo(90));
        }

        // ========== Complex Workflow Tests ==========

        [Test]
        public async Task ComplexWorkflow_PickupUseOpenCloseWorkflow()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var interactionSystem = new InteractionSystem();
            
            // Explicitly set ViewLocation to a known location (like other tests do)
            var testLocation = new WorldLocation(15, 15, 0);
            session.ViewLocation = testLocation;
            
            // Ensure player location matches ViewLocation
            session.Player!.Set(testLocation);
            
            var keyId = "test-key-456";
            var key = new KeyItem(keyId);
            // Set key at the same location as ViewLocation - use same coordinates
            key.Set(new WorldLocation(15, 15, 0));
            session.World.AddEntity(key);

            var door = new Door();
            door.Set(new WorldLocation(testLocation.X + 1, testLocation.Y, testLocation.Z));
            door.Get<OpensAndCloses>().IsLocked = true;
            door.Get<OpensAndCloses>().KeyShape = keyId;
            session.World.AddEntity(door);

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = AgentToolProfile.FullAccess.GrantedCapabilities,
                ServiceProvider = _serviceProvider
            };

            var pickupTool = _registry.GetTool("pickup");
            var useTool = _registry.GetTool("use");
            var openTool = _registry.GetTool("open");
            var closeTool = _registry.GetTool("close");

            // Act - Pickup key
            var pickupResult = await pickupTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["targetEntityId"] = key.EntityId
            });

            // Assert - Pickup should succeed
            Assert.That(pickupResult.Success, Is.True, $"Pickup failed: {pickupResult.Message}. Key location: {key.Get<WorldLocation>()}, ViewLocation: {session.ViewLocation}");

            // Act - Use key on door
            var useResult = await useTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["itemEntityId"] = key.EntityId,
                ["onEntityId"] = door.EntityId
            });

            // Act - Open door
            var openResult = await openTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["targetEntityId"] = door.EntityId
            });

            // Assert open before closing
            Assert.That(openResult.Success, Is.True, $"Open failed: {openResult.Message}");
            Assert.That(door.Get<OpensAndCloses>().IsOpen, Is.True, "Door should be open");

            // Act - Close door
            var closeResult = await closeTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["targetEntityId"] = door.EntityId
            });

            // Assert
            Assert.That(pickupResult.Success, Is.True, $"Pickup failed: {pickupResult.Message}");
            Assert.That(useResult.Success, Is.True, $"Use failed: {useResult.Message}");
            Assert.That(door.Get<OpensAndCloses>().IsLocked, Is.False, "Door should be unlocked after using key");
            Assert.That(closeResult.Success, Is.True, $"Close failed: {closeResult.Message}");
            Assert.That(door.Get<OpensAndCloses>().IsOpen, Is.False, "Door should be closed after close operation");
        }

        [Test]
        public async Task ComplexWorkflow_MoveRotateChangeLevelWorkflow()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            TestWorldMovement.CarveOpenArea(session); // movement is validated (P0-1)
            var startX = session.ViewLocation!.X;
            var startY = session.ViewLocation!.Y;
            var startZ = session.ViewLocation!.Z;
            var startHeading = session.HeadingDegrees;

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "basic_movement" },
                ServiceProvider = _serviceProvider
            };

            var moveTool = _registry.GetTool("move");
            var rotateTool = _registry.GetTool("rotate");
            var changeLevelTool = _registry.GetTool("changelevel");

            // Act - Move forward
            var moveResult = await moveTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 2
            });

            // Act - Rotate
            var rotateResult = await rotateTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["degrees"] = 90
            });

            // Act - Change level (stairs must be under the player's CURRENT
            // position, i.e. after the move above — level changes are validated)
            TestWorldMovement.CarveStairsAtPlayer(session, deltaZ: 1);
            var changeLevelResult = await changeLevelTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["delta"] = 1
            });

            // Assert
            Assert.That(moveResult.Success, Is.True);
            Assert.That(rotateResult.Success, Is.True);
            Assert.That(changeLevelResult.Success, Is.True);
            Assert.That(session.ViewLocation!.Z, Is.EqualTo(startZ + 1));
            Assert.That(session.HeadingDegrees, Is.Not.EqualTo(startHeading));
        }

        [Test]
        public async Task ComplexWorkflow_VisionModeChangesWorkflow()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var initialVisionMode = session.CurrentVisionMode;
            var initialLightingMode = session.CurrentLightingMode;

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "vision" },
                ServiceProvider = _serviceProvider
            };

            var setVisionModeTool = _registry.GetTool("setvisionmode");
            var setLightingModeTool = _registry.GetTool("setlightingmode");
            var setFOVTool = _registry.GetTool("setfieldofview");

            // Act - Change vision mode
            var visionResult = await setVisionModeTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["mode"] = "Infrared"
            });

            // Act - Change lighting mode
            var lightingResult = await setLightingModeTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["mode"] = "Torch"
            });

            // Act - Change FOV
            var fovResult = await setFOVTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["degrees"] = 60
            });

            // Assert
            Assert.That(visionResult.Success, Is.True);
            Assert.That(lightingResult.Success, Is.True);
            Assert.That(fovResult.Success, Is.True);
            Assert.That(session.CurrentVisionMode, Is.EqualTo(VisionMode.Infrared));
            Assert.That(session.CurrentLightingMode, Is.EqualTo(LightingMode.Torch));
            Assert.That(session.Player!.Get<HasHeading>()!.FieldOfViewDegrees, Is.EqualTo(60));
        }

        // ========== Session State Verification Tests ==========

        [Test]
        public async Task SessionState_MovementUpdatesPosition()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            TestWorldMovement.CarveOpenArea(session); // movement is validated (P0-1)
            var startX = session.ViewLocation!.X;
            var startY = session.ViewLocation!.Y;

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "basic_movement" },
                ServiceProvider = _serviceProvider
            };

            var moveTool = _registry.GetTool("move");

            // Act
            var result = await moveTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["direction"] = "forward",
                ["distance"] = 1
            });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(session.ViewLocation!.X != startX || session.ViewLocation!.Y != startY, Is.True);
            Assert.That(session.Player!.Get<WorldLocation>()!.X, Is.EqualTo(session.ViewLocation!.X));
            Assert.That(session.Player!.Get<WorldLocation>()!.Y, Is.EqualTo(session.ViewLocation!.Y));
        }

        [Test]
        public async Task SessionState_RotationUpdatesHeading()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var startHeading = session.HeadingDegrees;

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "basic_movement" },
                ServiceProvider = _serviceProvider
            };

            var rotateTool = _registry.GetTool("rotate");

            // Act
            var result = await rotateTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["clockwise"] = true
            });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(session.HeadingDegrees, Is.Not.EqualTo(startHeading));
        }

        [Test]
        public async Task SessionState_InventoryChangesAfterPickupAndDrop()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var interactionSystem = new InteractionSystem();
            var item = new KeyItem("test-key");
            item.Set(new WorldLocation(session.ViewLocation!.X, session.ViewLocation!.Y, session.ViewLocation!.Z));
            session.World.AddEntity(item);

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "inventory_access" },
                ServiceProvider = _serviceProvider
            };

            var pickupTool = _registry.GetTool("pickup");
            var dropTool = _registry.GetTool("drop");
            var inventory = session.Player!.Get<Inventory>()!;

            // Act - Pickup
            Assert.That(inventory.Items.ContainsKey(item.EntityId), Is.False);
            var pickupResult = await pickupTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["targetEntityId"] = item.EntityId
            });

            // Assert - Item in inventory
            Assert.That(pickupResult.Success, Is.True);
            Assert.That(inventory.Items.ContainsKey(item.EntityId), Is.True);
            Assert.That(session.World.Entities.ContainsKey(item.EntityId), Is.False);

            // Act - Drop
            var dropResult = await dropTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["itemEntityId"] = item.EntityId
            });

            // Assert - Item not in inventory
            Assert.That(dropResult.Success, Is.True);
            Assert.That(inventory.Items.ContainsKey(item.EntityId), Is.False);
            Assert.That(session.World.Entities.ContainsKey(item.EntityId), Is.True);
        }

        // ========== Error Handling Tests ==========

        [Test]
        public async Task ErrorHandling_OpenToolFailsForLockedDoor()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var interactionSystem = new InteractionSystem();
            var door = new Door();
            door.Set(new WorldLocation(session.ViewLocation!.X, session.ViewLocation!.Y, session.ViewLocation!.Z));
            door.Get<OpensAndCloses>().IsLocked = true;
            session.World.AddEntity(door);

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "interaction" },
                ServiceProvider = _serviceProvider
            };

            var openTool = _registry.GetTool("open");

            // Act
            var result = await openTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["targetEntityId"] = door.EntityId
            });

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Contains.Substring("Locked").IgnoreCase);
        }

        [Test]
        public async Task ErrorHandling_UseToolFailsForNonExistentEntity()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var interactionSystem = new InteractionSystem();
            var key = new KeyItem("test-key");
            session.Player!.Get<Inventory>()!.TryAdd(key.EntityId, key);

            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "inventory_access", "interaction" },
                ServiceProvider = _serviceProvider
            };

            var useTool = _registry.GetTool("use");

            // Act
            var result = await useTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["itemEntityId"] = key.EntityId,
                ["onEntityId"] = "non-existent-entity"
            });

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.Not.Empty);
        }

        [Test]
        public async Task ErrorHandling_MissingRequiredParameter()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "inventory_access" },
                ServiceProvider = _serviceProvider
            };

            var dropTool = _registry.GetTool("drop");

            // Act - Missing required parameter
            var result = await dropTool.ExecuteAsync(context, new Dictionary<string, object>());

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Contains.Substring("required parameter").IgnoreCase);
        }

        [Test]
        public async Task ErrorHandling_CapabilityRestriction()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string>(), // No capabilities
                ServiceProvider = _serviceProvider
            };

            var pickupTool = _registry.GetTool("pickup");

            // Act
            var result = await pickupTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["targetEntityId"] = "test-item"
            });

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Contains.Substring("capability").IgnoreCase);
        }

        [Test]
        public async Task ErrorHandling_InvalidParameterValue()
        {
            // Arrange
            var worldBuilder = new TorusWorldBuilder();
            var session = new GameSession("test", worldBuilder);
            var context = new ToolExecutionContext
            {
                SessionId = "test",
                Session = session,
                GrantedCapabilities = new HashSet<string> { "vision" },
                ServiceProvider = _serviceProvider
            };

            var fovTool = _registry.GetTool("setfieldofview");

            // Act - Invalid FOV value (too large)
            var result = await fovTool.ExecuteAsync(context, new Dictionary<string, object>
            {
                ["degrees"] = 500
            });

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.Not.Empty);
        }
    }
}

