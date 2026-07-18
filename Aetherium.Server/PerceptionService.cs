using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Lighting;
using Aetherium.Systems;
using Aetherium.Model;
using Aetherium.Server.Perception;
using Aetherium.Server.Simulation;
using Microsoft.Extensions.Options;

namespace Aetherium.Server
{
    public class PerceptionService
    {
        private readonly VisionSystem visionSystem = new VisionSystem();
        private readonly LightingSystem lightingSystem = new LightingSystem();
        private readonly InfraredVisionSystem infraredVisionSystem = new InfraredVisionSystem();
        private readonly SunlightCalculator sunlightCalculator = new SunlightCalculator();
        private readonly WorldClock? worldClock;
        private readonly WeatherSystem? weatherSystem;
        private readonly SeasonManager? seasonManager;

        public PerceptionService(
            WorldClock? worldClock = null,
            WeatherSystem? weatherSystem = null,
            SeasonManager? seasonManager = null)
        {
            this.worldClock = worldClock;
            this.weatherSystem = weatherSystem;
            this.seasonManager = seasonManager;
        }

        public PerceptionDto ComputePerception(
            World world,
            WorldLocation playerLocation,
            Aetherium.WorldDirection playerHeading,
            Size viewportSize,
            Entity? self = null)
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
                null,   // no FOV degrees
                self: self);
        }

        public PerceptionDto ComputePerception(
            World world,
            WorldLocation playerLocation,
            Aetherium.WorldDirection playerHeading,
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
            Aetherium.WorldDirection playerHeading,
            Size viewportSize,
            LightingMode lightingMode,
            VisionMode visionMode,
            HeatTrailTracker? heatTracker,
            DateTime currentTime,
            bool directionalVision,
            int? headingDegrees,
            int? fovDegrees,
            InteractionSystem? interactionSystem = null,
            GameSession? session = null,
            Entity? self = null,
            bool absoluteCoordinates = false)
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
                // Infrared mode: heat-based vision. The light frame doubles as the
                // heat channel — ComputeHeatVision records each heated location's
                // intensity into it, and the DTO conversion below reads it into
                // VisualDto.LightLevel, which infrared clients color by. (It was
                // previously left empty, so infrared always rendered black.)
                lightFrame = new LightFrame();
                visionFrame = infraredVisionSystem.ComputeHeatVision(
                    world,
                    playerLocation,
                    bounds,
                    heatTracker ?? new HeatTrailTracker(),
                    currentTime,
                    lightFrame);
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

            // Convert to DTO. By default coordinates are RELATIVE (player is always at 0,0,0) so
            // clients never learn absolute world coordinates. Operators may opt into absolute
            // coordinates for debugging via absoluteCoordinates=true (gated on the management path).
            var perception = new PerceptionDto
            {
                PlayerLocation = absoluteCoordinates
                    ? new WorldLocationDto(playerLocation.X, playerLocation.Y, playerLocation.Z)
                    : new WorldLocationDto(0, 0, 0),
                PlayerHeading = playerHeading.ToDto(),
                HeadingDegrees = headingDegrees ?? ConvertDirectionToDegrees(playerHeading),
                IsDirectionalVision = directionalVision,
                FieldOfViewDegrees = fovDegrees ?? 360,  // 360 means omnidirectional
                VisibleBounds = bounds.ToDto(),
                UpdateTimestamp = Guid.NewGuid(),

                // Tiling (docs/grid-topologies.md): the client reads Topology to pick its cell
                // layout; SelfCellParity surfaces the one bit relative deltas can't convey on a
                // triangular world (which way the perceiver's own triangle points).
                Topology = world.Topology.Name,
                SelfCellParity = world.Topology.Name == "tri"
                    ? (playerLocation.X + playerLocation.Y) & 1
                    : (int?)null,

                // Interoception (add-interoception-channel): the perceiver's own body state,
                // populated only when the caller supplied the perceiving entity. Self-only —
                // it reads this entity's components and nothing else.
                Interoception = self is null ? null : BuildInteroception(self),
                
                // Add mode information
                CurrentLightingMode = lightingMode,
                CurrentVisionMode = visionMode,
                GameTimeOfDay = worldClock?.GetTimeOfDay() ?? currentTime.TimeOfDay.TotalHours,
                AmbientTint = (ambientR, ambientG, ambientB),
                Weather = weatherSystem != null ? weatherSystem.GetWeather(GetRegionIdForLocation(playerLocation)).ToString() : "Clear",
                Season = seasonManager?.GetSeason((int)(worldClock?.GetDay() ?? 0)) ?? "spring"
            };
            
            // Diagnostic: Log vision stats for debugging (reusing testMode from above)
            if (testMode)
            {
                var originLightLevel2 = lightFrame.GetLightLevel(playerLocation);
                // Find project root (go up from Aetherium.Server to root, then .ui-test)
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
                        if (!(entity is Aetherium.Character) && !(entity is Aetherium.Entities.Terrain)
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

            // Derive visible characters (monsters/NPCs and co-located players).
            // Mirrors the item loop above. The perceiving player's own cell is
            // skipped — they are always the center marker, never a "seen" character.
            var visibleCharacters = new List<CharacterDto>();
            foreach (var loc in visionFrame.Visuals.Keys)
            {
                if (loc == playerLocation)
                    continue;

                if (world.EntitiesByLocation.TryGetValue(loc, out var charLoc))
                {
                    foreach (var entity in charLoc.Values)
                    {
                        if (entity is Aetherium.Character ch)
                        {
                            var charDto = ch.ToCharacterDto();
                            charDto.Location = new WorldLocationDto(
                                loc.X - playerLocation.X,
                                loc.Y - playerLocation.Y,
                                loc.Z - playerLocation.Z);
                            visibleCharacters.Add(charDto);
                        }
                    }
                }
            }

            perception.VisibleCharacters = visibleCharacters;

            // If we can find the player entity at playerLocation, include inventory and affordances
            Inventory? inv = null;
            if (world.EntitiesByLocation.TryGetValue(playerLocation, out var here))
            {
                var player = here.Values.OfType<Aetherium.Character>().FirstOrDefault();
                if (player != null)
                {
                    // Null-safe lookup: Component.Get<T>() throws when the component is
                    // absent, so a character standing here without an Inventory would
                    // crash the whole perception (the Get<T>-throws hazard the audit
                    // flagged). Mirror the OfType().FirstOrDefault() pattern used just
                    // below for Carriable.
                    inv = player.AllComponents.OfType<Inventory>().FirstOrDefault();
                    if (inv != null)
                        perception.Inventory = inv.ToDto();

                    // Pickup affordances for items at current tile
                    foreach (var e in here.Values)
                    {
                        if (!(e is Aetherium.Character) && !(e is Aetherium.Entities.Terrain) && e.AllComponents.OfType<Carriable>().Any())
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

                    // Door affordances for same/adjacent tiles and use affordances for items in inventory
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
                                        // Create "use" affordances for each item in inventory that can be used on this door
                                        if (inv != null)
                                        {
                                            foreach (var itemId in inv.ItemEntityIds)
                                            {
                                                var aff = new AffordanceDto 
                                                { 
                                                    Action = "use", 
                                                    ActorId = player.EntityId, 
                                                    ItemId = itemId,
                                                    TargetId = e.EntityId, 
                                                    RequiresKeyId = door.KeyShape 
                                                };
                                                affordances.Add(aff);
                                            }
                                        }
                                        else
                                        {
                                            // No inventory, but still create a generic use affordance for the door
                                            var aff = new AffordanceDto { Action = "use", ActorId = player.EntityId, TargetId = e.EntityId, RequiresKeyId = door.KeyShape };
                                            affordances.Add(aff);
                                        }
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

            // Populate UsageOptions for "use" affordances if InteractionSystem is available
            if (interactionSystem != null && session != null && inv != null)
            {
                foreach (var aff in affordances.Where(a => a.Action == "use" && !string.IsNullOrEmpty(a.ItemId) && !string.IsNullOrEmpty(a.TargetId)))
                {
                    var useOptions = interactionSystem.GetUseOptions(session, aff.ItemId!, aff.TargetId);
                    aff.UsageOptions = useOptions.Select(opt => new AffordanceUsageDto
                    {
                        UsageId = opt.UsageId,
                        Label = opt.Label,
                        TargetId = aff.TargetId
                    }).ToList();
                }
            }

            perception.Affordances = affordances;

            // Add navigation data if player has compass or map
            perception.NavigationData = ComputeNavigationData(world, playerLocation, playerHeading);

            // Add audio perception
            perception.Audio = ComputeAudioPerception(world, playerLocation, heatTracker, currentTime);

            // Apply sunlight calculation if using sunlight mode
            if (lightingMode == LightingMode.Sunlight && worldClock != null)
            {
                var timeOfDay = worldClock.GetTimeOfDay();
                sunlightCalculator.ComputeSunlight(
                    world,
                    bounds,
                    playerLocation.Z,
                    timeOfDay,
                    lightFrame);
            }

            return perception;
        }

        private string GetRegionIdForLocation(WorldLocation location)
        {
            // Generate a region ID from location coordinates (64×64 regions)
            var regionX = location.X / 64;
            var regionY = location.Y / 64;
            return $"region:{regionX},{regionY},{location.Z}";
        }

        /// <summary>
        /// Projects the perceiving character's own components into the interoception block
        /// (add-interoception-channel). Every read is guarded — Entity.Get&lt;T&gt;() throws on a
        /// missing component — so a body without pools/statuses/cooldowns degrades to empty
        /// lists (and 0/0 health), never a throw. Reads ONLY <paramref name="self"/>.
        /// </summary>
        private static InteroceptionDto BuildInteroception(Entity self)
        {
            var interoception = new InteroceptionDto();

            if (self.Has<Health>())
            {
                var health = self.Get<Health>();
                interoception.Health = health.Level;
                interoception.MaxHealth = health.MaxLevel;
            }

            if (self.Has<Combat.StatusEffects>())
            {
                foreach (var effect in self.Get<Combat.StatusEffects>().Active)
                    interoception.Statuses.Add(new SelfStatusDto
                    {
                        Id = effect.Id,
                        RemainingTicks = effect.RemainingTicks,
                    });
            }

            if (self.Has<Abilities.ResourcePools>())
            {
                foreach (var pool in self.Get<Abilities.ResourcePools>().All)
                    interoception.Pools.Add(new ResourcePoolStateDto
                    {
                        Tag = pool.Tag,
                        Current = pool.Current,
                        Max = pool.Max,
                        IsInverse = pool.IsInverse,
                    });
            }

            if (self.Has<Abilities.AbilityCooldowns>())
            {
                // Snapshot holds only abilities with ticks remaining (entries drop at 0),
                // so this is directly "what isn't ready yet".
                foreach (var (abilityId, remaining) in self.Get<Abilities.AbilityCooldowns>().Snapshot)
                    interoception.Cooldowns.Add(new AbilityReadinessDto
                    {
                        AbilityId = abilityId,
                        RemainingTicks = remaining,
                    });
            }

            return interoception;
        }

        private AudioPerceptionDto ComputeAudioPerception(
            World world,
            WorldLocation playerLocation,
            HeatTrailTracker? heatTracker,
            DateTime currentTime)
        {
            var audio = new AudioPerceptionDto();

            // Determine biome from terrain at player location
            var terrain = world.GetTerrain(playerLocation);
            if (terrain != null)
            {
                audio.Biome = MapTerrainToBiome(terrain.Type.Name);
                audio.FootstepMaterial = DetermineFootstepMaterial(terrain.Type.Name);
            }

            // Compute danger level from heat tracking
            if (heatTracker != null)
            {
                var heatLevel = (float)heatTracker.GetHeatAtLocation(playerLocation, currentTime);
                audio.DangerLevel = Math.Min(heatLevel, 1.0f);

                // Also check nearby locations for danger
                var nearbyHeat = 0.0;
                var deltas = new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1) };
                foreach (var (dx, dy) in deltas)
                {
                    var loc = playerLocation.FromDelta(dx, dy, 0);
                    nearbyHeat += heatTracker.GetHeatAtLocation(loc, currentTime);
                }
                audio.DangerLevel = Math.Min((float)(nearbyHeat / deltas.Length), 1.0f);
            }

            // Compute reverb and occlusion heuristics
            audio.ReverbPreset = DetermineReverbPreset(world, playerLocation, audio.Biome);
            audio.Occlusion = ComputeOcclusionHeuristic(world, playerLocation);

            // Determine suggested music track based on biome and danger
            audio.SuggestedMusicTrack = DetermineMusicTrack(audio.Biome, audio.DangerLevel > 0.3f);

            return audio;
        }

        private string MapTerrainToBiome(string terrainName)
        {
            return terrainName.ToLowerInvariant() switch
            {
                "forest" => "forest",
                "plains" => "plains",
                "water" => "water",
                "cave" => "cave",
                "indoors" => "indoors",
                "mountain" => "mountain",
                _ => "dungeon"
            };
        }

        private string DetermineFootstepMaterial(string terrainName)
        {
            return terrainName.ToLowerInvariant() switch
            {
                "forest" => "grass",
                "plains" => "grass",
                "water" => "water",
                "cave" => "stone",
                "indoors" => "stone",
                "mountain" => "stone",
                _ => "stone"
            };
        }

        private string DetermineReverbPreset(World world, WorldLocation location, string? biome)
        {
            // Simple heuristic: count connected open spaces for reverb hint
            var openNeighbors = 0;
            var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };

            foreach (var (dx, dy) in directions)
            {
                var neighbor = location.FromDelta(dx, dy, 0);
                var neighborTerrain = world.GetTerrain(neighbor);
                if (neighborTerrain != null && IsPassableTerrain(neighborTerrain.Type.Name))
                {
                    openNeighbors++;
                }
            }

            // If many open neighbors, suggest hall; otherwise use biome default
            if (openNeighbors >= 3)
                return "hall";
            if (openNeighbors >= 2)
                return "room";

            // Default by biome
            return biome?.ToLowerInvariant() switch
            {
                "cave" => "cave",
                "dungeon" => "hall",
                "indoors" => "room",
                _ => "outdoor"
            };
        }

        private float ComputeOcclusionHeuristic(World world, WorldLocation location)
        {
            var blockingNeighbors = 0;
            var directions = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };

            foreach (var (dx, dy) in directions)
            {
                var neighbor = location.FromDelta(dx, dy, 0);
                var neighborTerrain = world.GetTerrain(neighbor);
                if (neighborTerrain != null && IsBlockingTerrain(neighborTerrain.Type.Name))
                {
                    blockingNeighbors++;
                }
            }

            return Math.Min(blockingNeighbors * 0.15f, 0.6f);
        }

        private string? DetermineMusicTrack(string? biome, bool isDanger)
        {
            if (string.IsNullOrEmpty(biome))
                return null;

            // Simple mapping: danger music vs exploration music
            // This will be enhanced by AudioDirector with actual profiles
            return isDanger ? "techno-synth-loop" : "mellow-guitar-loop";
        }

        private bool IsPassableTerrain(string terrainName)
        {
            var lower = terrainName.ToLowerInvariant();
            return lower != "wall" && lower != "none" && lower != "mountain";
        }

        private bool IsBlockingTerrain(string terrainName)
        {
            var lower = terrainName.ToLowerInvariant();
            return lower == "wall" || lower == "none" || lower == "mountain";
        }

        private NavigationDataDto? ComputeNavigationData(World world, WorldLocation playerLocation, Aetherium.WorldDirection playerHeading)
        {
            // Check if player has a compass or navigation tool
            if (world.EntitiesByLocation.TryGetValue(playerLocation, out var here))
            {
                var player = here.Values.OfType<Aetherium.Character>().FirstOrDefault();
                if (player != null)
                {
                    // Null-safe: Component.Get<T>() throws when absent, so a character
                    // here without an Inventory would crash navigation computation.
                    var inv = player.AllComponents.OfType<Inventory>().FirstOrDefault();
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

        private int ConvertDirectionToDegrees(Aetherium.WorldDirection direction)
        {
            return direction switch
            {
                Aetherium.WorldDirection.North => 0,
                Aetherium.WorldDirection.East => 90,
                Aetherium.WorldDirection.South => 180,
                Aetherium.WorldDirection.West => 270,
                _ => 0
            };
        }
    }
}


