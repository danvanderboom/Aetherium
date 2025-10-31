using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Server.Audio;

namespace Aetherium.WorldGen.Passes
{
    /// <summary>
    /// Generates audio zones and acoustic data for the world based on biomes and room shapes
    /// </summary>
    public sealed class AudioGenerationPass : IWorldGenerationPass
    {
        private readonly IAudioProfileRepository _profileRepository;
        private Dictionary<string, BiomeAudioProfile> _profilesCache = new Dictionary<string, BiomeAudioProfile>();

        public AudioGenerationPass(IAudioProfileRepository? profileRepository = null)
        {
            _profileRepository = profileRepository ?? new JsonAudioProfileRepository();
        }

        public string Name => "audio-generation";
        public GenerationPhase Phase => GenerationPhase.Adaptation;

        public bool SupportsTemplate(WorldGenerationTemplate template) => true;

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null)
            {
                context.AddError("Audio generation requires world instance");
                return;
            }

            // Initialize profile repository
            _profileRepository.InitializeAsync().Wait();

            // Build audio zones by analyzing terrain
            var audioZones = BuildAudioZones(context.World);
            
            // Store in shared data for perception system
            context.SharedData["audio:zones"] = audioZones;

            // Store biome mappings for quick lookup
            var biomeMapping = BuildBiomeMapping(context.World);
            context.SharedData["audio:biomeMapping"] = biomeMapping;

            // Note: Metrics are tracked through standard GenerationMetrics properties
            // Audio-specific metrics could be added as properties if needed
        }

        private Dictionary<WorldLocation, AudioZone> BuildAudioZones(World world)
        {
            var zones = new Dictionary<WorldLocation, AudioZone>();
            var processed = new HashSet<WorldLocation>();

            // For each terrain location, determine biome and create/update audio zone
            foreach (var locationEntry in world.EntitiesByLocation)
            {
                var location = locationEntry.Key;
                if (processed.Contains(location))
                    continue;

                var terrain = world.GetTerrain(location);
                if (terrain == null)
                    continue;

                var biomeId = MapTerrainToBiome(terrain.Type.Name);
                if (string.IsNullOrEmpty(biomeId))
                    continue;

                // Get or create audio profile
                var profile = GetProfile(biomeId);
                if (profile == null)
                    continue;

                // Compute reverb based on room connectivity (simple heuristic)
                var reverb = ComputeReverbHeuristic(world, location);

                // Compute occlusion based on surrounding walls
                var occlusion = ComputeOcclusion(world, location);

                // Create zone entry
                var zone = new AudioZone
                {
                    BiomeId = biomeId,
                    ReverbPreset = profile.ReverbPreset,
                    BaseOcclusion = profile.BaseOcclusion + occlusion,
                    FootstepMaterial = profile.FootstepMaterial,
                    AmbientLoop = profile.AmbientLoop,
                    ExplorationMusic = profile.ExplorationMusic,
                    DangerMusic = profile.DangerMusic
                };

                zones[location] = zone;
                processed.Add(location);
            }

            return zones;
        }

        private Dictionary<WorldLocation, string> BuildBiomeMapping(World world)
        {
            var mapping = new Dictionary<WorldLocation, string>();

            foreach (var locationEntry in world.EntitiesByLocation)
            {
                var location = locationEntry.Key;
                var terrain = world.GetTerrain(location);
                if (terrain == null)
                    continue;

                var biomeId = MapTerrainToBiome(terrain.Type.Name);
                if (!string.IsNullOrEmpty(biomeId))
                {
                    mapping[location] = biomeId;
                }
            }

            return mapping;
        }

        private string MapTerrainToBiome(string terrainName)
        {
            // Map terrain type names to biome IDs
            return terrainName.ToLowerInvariant() switch
            {
                "forest" => "forest",
                "plains" => "plains",
                "water" => "water",
                "cave" => "cave",
                "indoors" => "indoors",
                "mountain" => "mountain",
                "wall" => null, // Walls don't have audio zones
                _ => "dungeon" // Default for dungeons and unknown types
            };
        }

        private BiomeAudioProfile? GetProfile(string biomeId)
        {
            if (_profilesCache.TryGetValue(biomeId, out var cached))
                return cached;

            var profile = _profileRepository.GetProfileAsync(biomeId).Result;
            if (profile != null)
                _profilesCache[biomeId] = profile;

            return profile;
        }

        private float ComputeReverbHeuristic(World world, WorldLocation location)
        {
            // Simple heuristic: count connected open spaces
            // More connections = larger space = more reverb
            var openNeighbors = 0;
            var directions = new[]
            {
                new WorldLocation(0, -1, 0), // North
                new WorldLocation(0, 1, 0),  // South
                new WorldLocation(-1, 0, 0), // West
                new WorldLocation(1, 0, 0)   // East
            };

            foreach (var delta in directions)
            {
                var neighbor = location.FromDelta(delta.X, delta.Y, delta.Z);
                var neighborTerrain = world.GetTerrain(neighbor);
                if (neighborTerrain != null && IsPassableTerrain(neighborTerrain.Type.Name))
                {
                    openNeighbors++;
                }
            }

            // Simple scale: 0-4 neighbors -> 0.0-0.4 additional reverb hint
            return Math.Min(openNeighbors * 0.1f, 0.4f);
        }

        private float ComputeOcclusion(World world, WorldLocation location)
        {
            // Simple heuristic: count walls/blocking neighbors
            var blockingNeighbors = 0;
            var directions = new[]
            {
                new WorldLocation(0, -1, 0), // North
                new WorldLocation(0, 1, 0),  // South
                new WorldLocation(-1, 0, 0), // West
                new WorldLocation(1, 0, 0)   // East
            };

            foreach (var delta in directions)
            {
                var neighbor = location.FromDelta(delta.X, delta.Y, delta.Z);
                var neighborTerrain = world.GetTerrain(neighbor);
                if (neighborTerrain != null && IsBlockingTerrain(neighborTerrain.Type.Name))
                {
                    blockingNeighbors++;
                }
            }

            // Simple scale: each blocking neighbor adds occlusion
            return Math.Min(blockingNeighbors * 0.1f, 0.3f);
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
    }

    /// <summary>
    /// Audio zone data for a location
    /// </summary>
    public class AudioZone
    {
        public string BiomeId { get; set; } = string.Empty;
        public string ReverbPreset { get; set; } = "outdoor";
        public float BaseOcclusion { get; set; } = 0.0f;
        public string FootstepMaterial { get; set; } = "stone";
        public string? AmbientLoop { get; set; }
        public string? ExplorationMusic { get; set; }
        public string? DangerMusic { get; set; }
    }
}

