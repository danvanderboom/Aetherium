using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGame.Lighting;
using ConsoleGame.Systems;
using ConsoleGameModel;

namespace ConsoleGameServer
{
    public class PerceptionService
    {
        private readonly VisionSystem visionSystem = new VisionSystem();
        private readonly LightingSystem lightingSystem = new LightingSystem();

        public PerceptionDto ComputePerception(World world, WorldLocation playerLocation, ConsoleGame.WorldDirection playerHeading, Size viewportSize)
        {
            // Calculate visible bounds based on viewport
            var worldWidth = viewportSize.Width / 2; // symbolWidth = 2
            var worldHeight = viewportSize.Height;
            var bounds = new Rectangle(
                playerLocation.X - worldWidth / 2,
                playerLocation.Y - worldHeight / 2,
                worldWidth,
                worldHeight);

            var maxRange = Math.Max(bounds.Width, bounds.Height) / 2 + 1;

            // Compute lighting
            var lightFrame = lightingSystem.ComputeLighting(world, bounds, playerLocation.Z);

            // Compute vision with lighting
            var visionFrame = visionSystem.ComputeVision(world, playerLocation, bounds, maxRange, lightFrame);

            // Convert to DTO with RELATIVE coordinates only (player is always at 0,0,0)
            var perception = new PerceptionDto
            {
                // PlayerLocation is always (0,0,0) - client should not know absolute world coordinates
                PlayerLocation = new WorldLocationDto(0, 0, 0),
                PlayerHeading = playerHeading.ToDto(),
                VisibleBounds = bounds.ToDto(),
                UpdateTimestamp = Guid.NewGuid()
            };

            // Convert all visuals to DTOs with RELATIVE coordinates (offsets from player)
            foreach (var visualList in visionFrame.Visuals)
            {
                var location = visualList.Key;
                var lightLevel = lightFrame.GetLightLevel(location);

                // Calculate relative offset from player position
                var relativeX = location.X - playerLocation.X;
                var relativeY = location.Y - playerLocation.Y;
                var relativeZ = location.Z - playerLocation.Z;

                // Combine all visuals at this location into one DTO
                var firstVisual = visualList.Value.FirstOrDefault();
                if (firstVisual != null)
                {
                    var visualDto = firstVisual.ToDto(lightLevel);
                    // Update location to be relative
                    visualDto.Location = new WorldLocationDto(relativeX, relativeY, relativeZ);
                    // Key uses relative coordinates
                    var key = $"{relativeX},{relativeY},{relativeZ}";
                    perception.Visuals[key] = visualDto;
                }
            }

            // Include tile types dictionary
            perception.TileTypes = world.TileTypes.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToDto());

            return perception;
        }
    }
}

