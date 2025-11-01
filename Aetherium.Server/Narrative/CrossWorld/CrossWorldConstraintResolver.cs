using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Orleans;

namespace Aetherium.Server.Narrative.CrossWorld
{
    /// <summary>
    /// Resolves cross-world constraints to concrete world/map targets using cluster grain.
    /// </summary>
    public static class CrossWorldConstraintResolver
    {
        /// <summary>
        /// Resolves a cross-world constraint to a target world/map pair.
        /// </summary>
        public static async Task<(string? worldId, string? mapId)> ResolveTargetAsync(
            CrossWorldConstraint constraint,
            string clusterId,
            IGrainFactory grainFactory)
        {
            if (constraint == null)
                return (null, null);

            if (string.IsNullOrEmpty(clusterId))
                return (null, null);

            var clusterGrain = grainFactory.GetGrain<IClusterGrain>(clusterId);
            var clusterInfo = await clusterGrain.GetClusterInfoAsync();

            if (clusterInfo == null)
                return (null, null);

            // Resolve world using selector
            string? targetWorldId = null;
            if (constraint.WorldSelector != null)
            {
                targetWorldId = await ResolveWorldAsync(constraint.WorldSelector, clusterInfo, grainFactory);
            }
            else
            {
                // No world selector - use first available world
                targetWorldId = clusterInfo.WorldIds.FirstOrDefault();
            }

            if (string.IsNullOrEmpty(targetWorldId))
                return (null, null);

            // Resolve map using selector
            string? targetMapId = null;
            if (constraint.MapSelector != null)
            {
                targetMapId = await ResolveMapAsync(constraint.MapSelector, targetWorldId, grainFactory);
            }
            else
            {
                // No map selector - use first available map
                var worldGrain = grainFactory.GetGrain<IWorldGrain>(targetWorldId);
                var worldInfo = await worldGrain.GetInfoAsync();
                targetMapId = worldInfo?.MapIds?.FirstOrDefault();
            }

            return (targetWorldId, targetMapId);
        }

        /// <summary>
        /// Resolves a world selector to a world ID.
        /// </summary>
        private static async Task<string?> ResolveWorldAsync(
            WorldSelector selector,
            ClusterInfo clusterInfo,
            IGrainFactory grainFactory)
        {
            // Exact world ID match
            if (!string.IsNullOrEmpty(selector.WorldId))
            {
                if (clusterInfo.WorldIds.Contains(selector.WorldId) && 
                    !selector.ExcludeWorldIds.Contains(selector.WorldId))
                {
                    return selector.WorldId;
                }
            }

            // Tag-based matching
            if (!string.IsNullOrEmpty(selector.WorldTag))
            {
                foreach (var worldId in clusterInfo.WorldIds)
                {
                    if (selector.ExcludeWorldIds.Contains(worldId))
                        continue;

                    var worldGrain = grainFactory.GetGrain<IWorldGrain>(worldId);
                    var worldInfo = await worldGrain.GetInfoAsync();

                    if (worldInfo?.Metadata != null)
                    {
                        // Check metadata for tags
                        if (worldInfo.Metadata.TryGetValue("tags", out var tagsObj) && 
                            tagsObj is List<string> tags)
                        {
                            if (tags.Any(t => t.Equals(selector.WorldTag, StringComparison.OrdinalIgnoreCase)))
                            {
                                return worldId;
                            }
                        }

                        // Check generator type for tag match
                        if (worldInfo.Metadata.TryGetValue("generatorType", out var genTypeObj))
                        {
                            var genType = genTypeObj?.ToString() ?? "";
                            if (genType.Contains(selector.WorldTag, StringComparison.OrdinalIgnoreCase))
                            {
                                return worldId;
                            }
                        }
                    }
                }
            }

            // Template-based matching
            if (!string.IsNullOrEmpty(selector.WorldTemplate))
            {
                foreach (var worldId in clusterInfo.WorldIds)
                {
                    if (selector.ExcludeWorldIds.Contains(worldId))
                        continue;

                    var worldGrain = grainFactory.GetGrain<IWorldGrain>(worldId);
                    var worldInfo = await worldGrain.GetInfoAsync();

                    if (worldInfo?.Metadata != null)
                    {
                        if (worldInfo.Metadata.TryGetValue("template", out var templateObj))
                        {
                            var template = templateObj?.ToString() ?? "";
                            if (template.Equals(selector.WorldTemplate, StringComparison.OrdinalIgnoreCase))
                            {
                                return worldId;
                            }
                        }
                    }
                }
            }

            // Fallback: return first non-excluded world
            return clusterInfo.WorldIds.FirstOrDefault(w => !selector.ExcludeWorldIds.Contains(w));
        }

        /// <summary>
        /// Resolves a map selector to a map ID within a world.
        /// </summary>
        private static async Task<string?> ResolveMapAsync(
            MapSelector selector,
            string worldId,
            IGrainFactory grainFactory)
        {
            var worldGrain = grainFactory.GetGrain<IWorldGrain>(worldId);
            var worldInfo = await worldGrain.GetInfoAsync();

            if (worldInfo?.MapIds == null || worldInfo.MapIds.Count == 0)
                return null;

            // Exact map ID match
            if (!string.IsNullOrEmpty(selector.MapId))
            {
                if (worldInfo.MapIds.Contains(selector.MapId))
                {
                    return selector.MapId;
                }
            }

            // Tag or name-based matching
            if (!string.IsNullOrEmpty(selector.MapTag) || !string.IsNullOrEmpty(selector.MapName))
            {
                foreach (var mapId in worldInfo.MapIds)
                {
                    var mapGrain = grainFactory.GetGrain<IGameMapGrain>(mapId);
                    var mapMetadata = await mapGrain.GetMetadataAsync();

                    if (mapMetadata == null)
                        continue;

                    // Check tag match (could be in generator type or map name)
                    if (!string.IsNullOrEmpty(selector.MapTag))
                    {
                        if (mapMetadata.GeneratorType?.Contains(selector.MapTag, StringComparison.OrdinalIgnoreCase) == true ||
                            mapMetadata.MapName?.Contains(selector.MapTag, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return mapId;
                        }
                    }

                    // Check name match
                    if (!string.IsNullOrEmpty(selector.MapName))
                    {
                        if (mapMetadata.MapName?.Equals(selector.MapName, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return mapId;
                        }
                    }
                }
            }

            // Fallback: return first map
            return worldInfo.MapIds.FirstOrDefault();
        }
    }
}

