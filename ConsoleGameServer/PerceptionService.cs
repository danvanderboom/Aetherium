using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGame.Entities;
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

            // Derive visible items and simple affordances
            var visibleItems = new List<ItemDto>();
            var affordances = new List<AffordanceDto>();

            // Detect items in bounds
            foreach (var loc in visionFrame.Visuals.Keys)
            {
                if (world.EntitiesByLocation.TryGetValue(loc, out var atLoc))
                {
                    foreach (var entity in atLoc.Values)
                    {
                        // Items are carriable non-character, non-terrain entities
                        if (!(entity is ConsoleGame.Character) && !(entity is ConsoleGame.Entities.Terrain)
                            && entity.AllComponents.OfType<Carriable>().Any())
                        {
                            var itemDto = entity.ToDto();
                            // Include relative location
                            itemDto.Location = new WorldLocationDto(
                                loc.X - playerLocation.X,
                                loc.Y - playerLocation.Y,
                                loc.Z - playerLocation.Z);
                            visibleItems.Add(itemDto);
                        }
                    }
                }
            }

            perception.VisibleItems = visibleItems;

            // If we can find the player entity at playerLocation, include inventory and affordances
            if (world.EntitiesByLocation.TryGetValue(playerLocation, out var here))
            {
                var player = here.Values.OfType<ConsoleGame.Character>().FirstOrDefault();
                if (player != null)
                {
                    var inv = player.Get<Inventory>();
                    if (inv != null)
                        perception.Inventory = inv.ToDto();

                    // Pickup affordances for items at current tile
                    foreach (var e in here.Values)
                    {
                        if (!(e is ConsoleGame.Character) && !(e is ConsoleGame.Entities.Terrain) && e.AllComponents.OfType<Carriable>().Any())
                        {
                            affordances.Add(new AffordanceDto { Action = "pickup", ActorId = player.EntityId, TargetId = e.EntityId });
                        }
                    }

                    // Drop affordances for each inventory item
                    if (inv != null)
                    {
                        foreach (var id in inv.ItemEntityIds)
                            affordances.Add(new AffordanceDto { Action = "drop", ActorId = player.EntityId, TargetId = id });
                    }

                    // Door affordances for same/adjacent tiles
                    var deltas = new[] { (0,0), (1,0), (-1,0), (0,1), (0,-1) };
                    foreach (var (dx, dy) in deltas)
                    {
                        var loc = playerLocation.FromDelta(dx, dy, 0);
                        if (world.EntitiesByLocation.TryGetValue(loc, out var ents))
                        {
                            foreach (var e in ents.Values)
                            {
                                var door = e.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
                                if (door != null)
                                {
                                    if (door.IsLocked)
                                    {
                                        var aff = new AffordanceDto { Action = "use", ActorId = player.EntityId, TargetId = e.EntityId, RequiresKeyId = door.KeyShape };
                                        affordances.Add(aff);
                                    }
                                    else
                                    {
                                        affordances.Add(new AffordanceDto { Action = door.IsOpen ? "close" : "open", ActorId = player.EntityId, TargetId = e.EntityId });
                                    }
                                }
                            }
                        }
                    }
                }
            }

            perception.Affordances = affordances;

            // Add navigation data if player has compass or map
            perception.NavigationData = ComputeNavigationData(world, playerLocation, playerHeading);

            return perception;
        }

        private NavigationDataDto? ComputeNavigationData(World world, WorldLocation playerLocation, ConsoleGame.WorldDirection playerHeading)
        {
            // Check if player has a compass or navigation tool
            if (world.EntitiesByLocation.TryGetValue(playerLocation, out var here))
            {
                var player = here.Values.OfType<ConsoleGame.Character>().FirstOrDefault();
                if (player != null)
                {
                    var inv = player.Get<Inventory>();
                    if (inv != null)
                    {
                        // Check inventory for items with ProvidesNavigation component
                        foreach (var itemId in inv.ItemEntityIds)
                        {
                            if (inv.Items.TryGetValue(itemId, out var item))
                            {
                                var navComponent = item.AllComponents.OfType<ProvidesNavigation>().FirstOrDefault();
                                if (navComponent != null)
                                {
                                    // Player has a compass!
                                    return new NavigationDataDto
                                    {
                                        HasCompass = true,
                                        CardinalDirection = playerHeading.ToDto(),
                                        HeadingDegrees = ConvertDirectionToDegrees(playerHeading)
                                    };
                                }
                            }
                        }
                    }
                }
            }

            return null; // No compass available
        }

        private int ConvertDirectionToDegrees(ConsoleGame.WorldDirection direction)
        {
            return direction switch
            {
                ConsoleGame.WorldDirection.North => 0,
                ConsoleGame.WorldDirection.East => 90,
                ConsoleGame.WorldDirection.South => 180,
                ConsoleGame.WorldDirection.West => 270,
                _ => 0
            };
        }
    }
}

