using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGame.Entities;
using ConsoleGame.Lighting;
using ConsoleGame.Systems;
using ConsoleGameModel;
using ConsoleGameServer.Perception;

namespace ConsoleGameServer
{
    public class PerceptionService
    {
        private readonly VisionSystem visionSystem = new VisionSystem();
        private readonly LightingSystem lightingSystem = new LightingSystem();
        private readonly InfraredVisionSystem infraredVisionSystem = new InfraredVisionSystem();

        public PerceptionDto ComputePerception(
            World world,
            WorldLocation playerLocation,
            ConsoleGame.WorldDirection playerHeading,
            Size viewportSize)
        {
            // Use default modes for backward compatibility
            return ComputePerception(
                world,
                playerLocation,
                playerHeading,
                viewportSize,
                LightingMode.Torch,
                VisionMode.Normal,
                null,
                DateTime.UtcNow,
                false,  // directional vision disabled by default
                null,   // no heading degrees
                null);  // no FOV degrees
        }

        public PerceptionDto ComputePerception(
            World world,
            WorldLocation playerLocation,
            ConsoleGame.WorldDirection playerHeading,
            Size viewportSize,
            LightingMode lightingMode,
            VisionMode visionMode,
            HeatTrailTracker? heatTracker,
            DateTime currentTime)
        {
            return ComputePerception(
                world,
                playerLocation,
                playerHeading,
                viewportSize,
                lightingMode,
                visionMode,
                heatTracker,
                currentTime,
                false,  // directional vision disabled by default
                null,   // no heading degrees
                null);  // no FOV degrees
        }

        /// <summary>
        /// Computes perception with full control over vision modes.
        /// </summary>
        public PerceptionDto ComputePerception(
            World world,
            WorldLocation playerLocation,
            ConsoleGame.WorldDirection playerHeading,
            Size viewportSize,
            LightingMode lightingMode,
            VisionMode visionMode,
            HeatTrailTracker? heatTracker,
            DateTime currentTime,
            bool directionalVision,
            int? headingDegrees,
            int? fovDegrees)
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

            // Check if we're in UI self-test mode (for all diagnostic code in this method)
            var testMode = Environment.GetEnvironmentVariable("UI_SELFTEST_MODE") == "1";

            // DIAGNOSTIC: Log light sources before computing lighting (only in UI self-test mode)
            if (testMode)
            {
                try
                {
                    var lightSourcesPath = Path.Combine(Environment.CurrentDirectory, ".ui-test", "light_sources_diagnostics.txt");
                    var dir = Path.GetDirectoryName(lightSourcesPath);
                    if (dir != null)
                    {
                        Directory.CreateDirectory(dir);
                        File.WriteAllText(lightSourcesPath, $@"World bounds: {bounds}
Player Z level: {playerLocation.Z}
Total entities in world: {world.Entities.Count}

Light sources found:
");
                        
                        foreach (var entity in world.Entities.Values)
                        {
                            if (entity.Has<WorldLocation>() && entity.Has<LightSource>())
                            {
                                var loc = entity.Get<WorldLocation>();
                                var light = entity.Get<LightSource>();
                                File.AppendAllText(lightSourcesPath, $"Entity {entity.GetHashCode()}: Location {loc}, Intensity {light.Intensity}, Range {light.Range}, Enabled {light.IsEnabled}{Environment.NewLine}");
                            }
                        }
                    }
                }
                catch { /* ignore file write errors */                 }
            }

            // Compute lighting or heat-based vision depending on mode
            VisionFrame visionFrame;
            LightFrame lightFrame;

            if (visionMode == VisionMode.Infrared)
            {
                // Infrared mode: use heat-based vision
                lightFrame = new LightFrame(); // Empty light frame
                visionFrame = infraredVisionSystem.ComputeHeatVision(
                    world,
                    playerLocation,
                    bounds,
                    heatTracker ?? new HeatTrailTracker(),
                    currentTime);
            }
            else
            {
                // Normal vision mode: use lighting + FOV
                lightFrame = lightingSystem.ComputeLightingWithMode(
                    world,
                    bounds,
                    playerLocation.Z,
                    lightingMode,
                    playerLocation,
                    currentTime.TimeOfDay.TotalHours);

                // Compute vision with lighting - use directional FOV if enabled
                if (directionalVision && headingDegrees.HasValue && fovDegrees.HasValue)
                {
                    visionFrame = visionSystem.ComputeVision(world, playerLocation, bounds, maxRange, lightFrame,
                        headingDegrees.Value, fovDegrees.Value);
                }
                else
                {
                    visionFrame = visionSystem.ComputeVision(world, playerLocation, bounds, maxRange, lightFrame);
                }
            }

            // Get sunlight color for ambient tint (if in sunlight mode)
            double ambientR = 1.0, ambientG = 1.0, ambientB = 1.0;
            if (lightingMode == LightingMode.Sunlight && visionMode == VisionMode.Normal)
            {
                var sunlightCalc = new SunlightCalculator();
                var (_, elevation) = sunlightCalc.CalculateSunPosition(currentTime.TimeOfDay.TotalHours);
                var (r, g, b, _) = sunlightCalc.GetSunlightColor(elevation);
                ambientR = r;
                ambientG = g;
                ambientB = b;
            }

            // DIAGNOSTIC: Only write diagnostics in UI self-test mode to avoid interfering with unit tests
            if (testMode)
            {
                try 
                {
                    var diagFile = Path.Combine(Environment.CurrentDirectory, "..", ".ui-test", "server_diagnostics.txt");
                    var lightLevel = lightFrame.GetLightLevel(playerLocation);
                    var diagText = $"Player WORLD location: {playerLocation.X},{playerLocation.Y},{playerLocation.Z}\n" +
                                  $"Light level at player: {lightLevel:F3}\n" +
                                  $"Visuals count: {visionFrame.Visuals.Count}\n" +
                                  $"Light sources in world: {lightFrame.LightLevels.Count}\n";
                    var dir = Path.GetDirectoryName(diagFile);
                    if (dir != null)
                    {
                        Directory.CreateDirectory(dir);
                        File.AppendAllText(diagFile, diagText + "\n");
                    }
                } 
                catch { /* ignore file write errors */ }
            }

            // Convert to DTO with RELATIVE coordinates only (player is always at 0,0,0)
            var perception = new PerceptionDto
            {
                // PlayerLocation is always (0,0,0) - client should not know absolute world coordinates
                PlayerLocation = new WorldLocationDto(0, 0, 0),
                PlayerHeading = playerHeading.ToDto(),
                HeadingDegrees = headingDegrees ?? ConvertDirectionToDegrees(playerHeading),
                IsDirectionalVision = directionalVision,
                FieldOfViewDegrees = fovDegrees ?? 360,  // 360 means omnidirectional
                VisibleBounds = bounds.ToDto(),
                UpdateTimestamp = Guid.NewGuid(),
                
                // Add mode information
                CurrentLightingMode = lightingMode,
                CurrentVisionMode = visionMode,
                GameTimeOfDay = currentTime.TimeOfDay.TotalHours,
                AmbientTint = (ambientR, ambientG, ambientB)
            };
            
            // Diagnostic: Log vision stats for debugging (reusing testMode from above)
            if (testMode)
            {
                var originLightLevel2 = lightFrame.GetLightLevel(playerLocation);
                // Find project root (go up from ConsoleGameServer to root, then .ui-test)
                var serverDir = System.IO.Directory.GetCurrentDirectory();
                var projectRoot = System.IO.Directory.GetParent(serverDir)?.Parent?.FullName ?? serverDir;
                var diagPath = System.IO.Path.Combine(projectRoot, ".ui-test", "server_perception_diagnostics.txt");
                try
                {
                    var dir = System.IO.Path.GetDirectoryName(diagPath);
                    if (dir != null)
                    {
                        System.IO.Directory.CreateDirectory(dir);
                        var diagContent = $"Player world location: {playerLocation.X},{playerLocation.Y},{playerLocation.Z}\n" +
                            $"Bounds: {bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}\n" +
                            $"maxRange: {maxRange}\n" +
                            $"Origin light level: {originLightLevel2}\n" +
                            $"VisionFrame.Visuals count: {visionFrame.Visuals.Count}\n" +
                            $"Sample visual keys (first 10): {string.Join(", ", visionFrame.Visuals.Keys.Take(10).Select(loc => $"{loc.X},{loc.Y},{loc.Z}"))}\n";
                        System.IO.File.WriteAllText(diagPath, diagContent);
                    }
                }
                catch { /* ignore write errors */ }
            }

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

