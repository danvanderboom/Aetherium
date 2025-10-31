using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.WorldBuilders;
using Aetherium.Model;
using Aetherium.Server;
using Xunit;

namespace Aetherium.Test
{
    public class ClientServerCommunicationTests
    {
        [Fact]
        public void GameSession_InitializesWithWorld()
        {
            // Arrange & Act
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));

            // Assert
            Assert.NotNull(session.World);
            Assert.NotNull(session.ViewLocation);
            Assert.Equal(Aetherium.WorldDirection.North, session.Heading);
        }

        [Fact]
        public void GameSession_ComputesPerception()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));
            session.ViewLocation = new Aetherium.Components.WorldLocation(15, 15, 0);

            // Act
            var perception = session.GetPerception();

            // Assert
            Assert.NotNull(perception);
            // Player location is always (0,0,0) in relative coordinates - client should not know absolute position
            Assert.Equal(0, perception.PlayerLocation.X);
            Assert.Equal(0, perception.PlayerLocation.Y);
            Assert.Equal(0, perception.PlayerLocation.Z);
            Assert.NotEmpty(perception.Visuals);
        }

        [Fact]
        public void PerceptionService_IncludesOnlyVisibleTiles()
        {
            // Arrange
            var worldBuilder = new FovDiagnosticWorldBuilder("simple_wall");
            var world = worldBuilder.Build();
            var service = new PerceptionService();
            var playerLocation = new Aetherium.Components.WorldLocation(10, 10, 0);
            var viewportSize = new Size(42, 22);

            // Act
            var perception = service.ComputePerception(world, playerLocation, Aetherium.WorldDirection.North, viewportSize);

            // Assert
            Assert.NotNull(perception);
            Assert.NotEmpty(perception.Visuals);
            
            // Verify visuals use relative coordinates (offsets from player at 0,0,0)
            foreach (var visual in perception.Visuals.Values)
            {
                // Visual locations are now relative offsets from player
                var dx = Math.Abs(visual.Location.X); // Player is at 0, so this is the offset
                var dy = Math.Abs(visual.Location.Y);
                var distance = Math.Sqrt(dx * dx + dy * dy);
                
                // Should be within viewport bounds or FOV range (relative to player at center)
                Assert.True(distance < 30, $"Visual at relative distance {distance} is too far from player");
            }
        }

        [Fact]
        public void PerceptionDto_ContainsRequiredFields()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));

            // Act
            var perception = session.GetPerception();

            // Assert
            Assert.NotNull(perception.PlayerLocation);
            Assert.True(Enum.IsDefined(typeof(Aetherium.Model.WorldDirection), perception.PlayerHeading));
            Assert.NotNull(perception.Visuals);
            Assert.NotNull(perception.VisibleBounds);
            Assert.NotEqual(Guid.Empty, perception.UpdateTimestamp);
            Assert.NotNull(perception.TileTypes);
        }

        [Fact]
        public void VisualDto_IncludesLightLevel()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));

            // Act
            var perception = session.GetPerception();

            // Assert
            Assert.NotEmpty(perception.Visuals);
            var firstVisual = perception.Visuals.Values.First();
            Assert.InRange(firstVisual.LightLevel, 0.0, 1.0);
        }

        [Fact]
        public void GameSession_MoveUpdatesViewLocation()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));
            var initialLocation = session.ViewLocation;

            // Act
            session.MoveView(Aetherium.Model.RelativeDirection.Forward, 1);

            // Assert
            Assert.NotNull(session.ViewLocation);
            Assert.NotEqual(initialLocation, session.ViewLocation);
        }

        [Fact]
        public void GameSession_RotateUpdatesHeading()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));
            session.Heading = Aetherium.WorldDirection.North;

            // Act
            session.RotateView(true); // Clockwise

            // Assert
            Assert.Equal(Aetherium.WorldDirection.East, session.Heading);
        }

        [Fact]
        public void GameSession_RotateCounterclockwise()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));
            session.Heading = Aetherium.WorldDirection.North;

            // Act
            session.RotateView(false); // Counter-clockwise

            // Assert
            Assert.Equal(Aetherium.WorldDirection.West, session.Heading);
        }

        [Fact]
        public void GameSession_ChangeLevelUpdatesZ()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));
            var initialZ = session.ViewLocation?.Z ?? 0;

            // Act
            session.ChangeLevel(1);

            // Assert
            Assert.NotNull(session.ViewLocation);
            Assert.Equal(initialZ + 1, session.ViewLocation.Z);
        }

        [Fact]
        public void GameSessionManager_CreatesAndTracksSession()
        {
            // Arrange
            var manager = new GameSessionManager();

            // Act
            var session = manager.CreateSession("connection-1", new FovDiagnosticWorldBuilder("open_space"));

            // Assert
            Assert.NotNull(session);
            Assert.Equal("connection-1", session.ConnectionId);
            Assert.Equal(1, manager.ActiveSessionCount);
        }

        [Fact]
        public void GameSessionManager_RetrievesSession()
        {
            // Arrange
            var manager = new GameSessionManager();
            var created = manager.CreateSession("connection-1", new FovDiagnosticWorldBuilder("open_space"));

            // Act
            var retrieved = manager.GetSession("connection-1");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(created.SessionId, retrieved.SessionId);
        }

        [Fact]
        public void GameSessionManager_RemovesSession()
        {
            // Arrange
            var manager = new GameSessionManager();
            manager.CreateSession("connection-1", new FovDiagnosticWorldBuilder("open_space"));

            // Act
            var removed = manager.RemoveSession("connection-1");
            var retrieved = manager.GetSession("connection-1");

            // Assert
            Assert.True(removed);
            Assert.Null(retrieved);
            Assert.Equal(0, manager.ActiveSessionCount);
        }

        [Fact]
        public void MappingExtensions_ConvertWorldLocation()
        {
            // Arrange
            var engineLocation = new Aetherium.Components.WorldLocation(10, 20, 5);

            // Act
            var dto = engineLocation.ToDto();

            // Assert
            Assert.Equal(10, dto.X);
            Assert.Equal(20, dto.Y);
            Assert.Equal(5, dto.Z);
        }

        [Fact]
        public void MappingExtensions_ConvertWorldLocationBack()
        {
            // Arrange
            var dto = new WorldLocationDto(10, 20, 5);

            // Act
            var engineLocation = dto.ToWorldLocation();

            // Assert
            Assert.Equal(10, engineLocation.X);
            Assert.Equal(20, engineLocation.Y);
            Assert.Equal(5, engineLocation.Z);
        }

        [Fact]
        public void MappingExtensions_ConvertDirection()
        {
            // Arrange
            var engineDirection = Aetherium.WorldDirection.East;

            // Act
            var dto = engineDirection.ToDto();

            // Assert
            Assert.Equal(Aetherium.Model.WorldDirection.East, dto);
        }

        [Fact]
        public void PerceptionDto_SerializableRoundTrip()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));
            var perception = session.GetPerception();

            // Act - Simulate serialization by converting to/from JSON
            var json = System.Text.Json.JsonSerializer.Serialize(perception);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<PerceptionDto>(json);

            // Assert
            Assert.NotNull(deserialized);
            // Player location should always be (0,0,0) in relative coordinates
            Assert.Equal(0, deserialized.PlayerLocation.X);
            Assert.Equal(0, deserialized.PlayerLocation.Y);
            Assert.Equal(0, deserialized.PlayerLocation.Z);
            Assert.Equal(perception.Visuals.Count, deserialized.Visuals.Count);
        }

        [Fact]
        public void PerceptionService_RespectsFieldOfView()
        {
            // Arrange
            var worldBuilder = new FovDiagnosticWorldBuilder("open_space");
            var world = worldBuilder.Build();
            var service = new PerceptionService();
            var playerLocation = new Aetherium.Components.WorldLocation(15, 15, 0);
            var viewportSize = new Size(42, 22);

            // Act
            var perception = service.ComputePerception(world, playerLocation, Aetherium.WorldDirection.North, viewportSize);

            // Assert
            // Player location is always at (0,0,0) in relative coordinates and should be visible
            var playerKey = "0,0,0"; // Relative coordinates: player is always at center
            Assert.True(perception.Visuals.ContainsKey(playerKey), "Player location (0,0,0) should be visible");

            // Locations far outside FOV should not be included (using relative coordinates)
            var farAwayKey = "100,100,0"; // Relative offset from player
            Assert.False(perception.Visuals.ContainsKey(farAwayKey), "Far away locations should not be visible");
        }

        [Fact]
        public void PerceptionService_IncludesTileTypes()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));

            // Act
            var perception = session.GetPerception();

            // Assert
            Assert.NotEmpty(perception.TileTypes);
            Assert.True(perception.TileTypes.ContainsKey("Indoors") || 
                       perception.TileTypes.ContainsKey("None") || 
                       perception.TileTypes.Any(),
                       "Should include tile type definitions");
        }

        [Fact]
        public void GameSession_ToggleDirectionalVision()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));
            Assert.False(session.DirectionalVisionMode); // Default is off

            // Act
            session.DirectionalVisionMode = true;

            // Assert
            Assert.True(session.DirectionalVisionMode);
        }

        [Fact]
        public void GameSession_RotateByDegrees()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));
            session.HeadingDegrees = 0; // Start facing North

            // Act
            session.RotateView(15); // Rotate 15 degrees clockwise

            // Assert
            Assert.Equal(15, session.HeadingDegrees);
        }

        [Fact]
        public void GameSession_RotateByDegreesWrapsAround()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));
            session.HeadingDegrees = 350;

            // Act
            session.RotateView(20); // Rotate 20 degrees clockwise (should wrap to 10)

            // Assert
            Assert.Equal(10, session.HeadingDegrees);
        }

        [Fact]
        public void GameSession_RotateByNegativeDegreesCounterClockwise()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));
            session.HeadingDegrees = 0;

            // Act
            session.RotateView(-15); // Rotate 15 degrees counter-clockwise

            // Assert
            Assert.Equal(345, session.HeadingDegrees); // 0 - 15 + 360 = 345
        }

        [Fact]
        public void PerceptionDto_IncludesDirectionalVisionFields()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));
            session.DirectionalVisionMode = true;
            session.HeadingDegrees = 45;

            // Act
            var perception = session.GetPerception();

            // Assert
            Assert.Equal(45, perception.HeadingDegrees);
            Assert.True(perception.IsDirectionalVision);
            Assert.InRange(perception.FieldOfViewDegrees, 1, 360);
        }

        [Fact]
        public void PerceptionService_DirectionalVisionFiltersVisibility()
        {
            // Arrange
            var worldBuilder = new FovDiagnosticWorldBuilder("open_space");
            var world = worldBuilder.Build();
            var service = new PerceptionService();
            var playerLocation = new Aetherium.Components.WorldLocation(15, 15, 0);
            var viewportSize = new Size(42, 22);

            // Act - Compute perception with directional vision facing North (0°) with 90° FOV
            var perception = service.ComputePerception(
                world, 
                playerLocation, 
                Aetherium.WorldDirection.North, 
                viewportSize,
                LightingMode.Torch,
                VisionMode.Normal,
                null,
                DateTime.UtcNow,
                directionalVision: true,
                headingDegrees: 0,
                fovDegrees: 90);

            // Assert
            Assert.True(perception.IsDirectionalVision);
            Assert.Equal(0, perception.HeadingDegrees);
            Assert.Equal(90, perception.FieldOfViewDegrees);

            // Verify that some cells are visible (north) and some are filtered out
            // The exact counts depend on the world, but we should have fewer visuals than omnidirectional
            Assert.NotEmpty(perception.Visuals);
        }

        [Fact]
        public void PerceptionService_DirectionalVisionVsOmnidirectional()
        {
            // Arrange
            var worldBuilder = new FovDiagnosticWorldBuilder("open_space");
            var world = worldBuilder.Build();
            var service = new PerceptionService();
            var playerLocation = new Aetherium.Components.WorldLocation(15, 15, 0);
            var viewportSize = new Size(42, 22);

            // Act - Get both omnidirectional and directional perceptions
            var omniPerception = service.ComputePerception(
                world, playerLocation, Aetherium.WorldDirection.North, viewportSize,
                LightingMode.Torch, VisionMode.Normal, null, DateTime.UtcNow,
                directionalVision: false, headingDegrees: null, fovDegrees: null);

            var directionalPerception = service.ComputePerception(
                world, playerLocation, Aetherium.WorldDirection.North, viewportSize,
                LightingMode.Torch, VisionMode.Normal, null, DateTime.UtcNow,
                directionalVision: true, headingDegrees: 0, fovDegrees: 90);

            // Assert - Directional vision should see fewer cells than omnidirectional
            Assert.True(directionalPerception.Visuals.Count < omniPerception.Visuals.Count,
                $"Directional vision ({directionalPerception.Visuals.Count} cells) should see fewer cells than omnidirectional ({omniPerception.Visuals.Count} cells)");
        }

        [Fact]
        public void GameSession_HeadingPropertyUsesDegreesConversion()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));

            // Act - Set using enum property
            session.Heading = Aetherium.WorldDirection.East;

            // Assert - HeadingDegrees should be updated
            Assert.Equal(90, session.HeadingDegrees);
        }

        [Fact]
        public void GameSession_DegreesPropertyUpdatesHeading()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));

            // Act - Set degrees directly
            session.HeadingDegrees = 90;

            // Assert - Heading enum should return East
            Assert.Equal(Aetherium.WorldDirection.East, session.Heading);
        }

        [Fact]
        public void GameSession_DegreesToHeadingRounding()
        {
            // Arrange
            var session = new GameSession("test-connection", new FovDiagnosticWorldBuilder("open_space"));

            // Act & Assert - Test rounding to nearest cardinal direction
            session.HeadingDegrees = 30;
            Assert.Equal(Aetherium.WorldDirection.North, session.Heading); // 30° rounds to North (0-44)

            session.HeadingDegrees = 60;
            Assert.Equal(Aetherium.WorldDirection.East, session.Heading); // 60° rounds to East (45-134)

            session.HeadingDegrees = 150;
            Assert.Equal(Aetherium.WorldDirection.South, session.Heading); // 150° rounds to South (135-224)

            session.HeadingDegrees = 240;
            Assert.Equal(Aetherium.WorldDirection.West, session.Heading); // 240° rounds to West (225-314)

            session.HeadingDegrees = 330;
            Assert.Equal(Aetherium.WorldDirection.North, session.Heading); // 330° rounds to North (315-359)
        }
    }
}


