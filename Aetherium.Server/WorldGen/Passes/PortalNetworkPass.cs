using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;

namespace Aetherium.WorldGen.Passes
{
    /// <summary>
    /// Generation pass that places portal entities during the Interactions phase,
    /// connecting worlds within clusters via link metadata.
    /// </summary>
    public sealed class PortalNetworkPass : IWorldGenerationPass
    {
        // One procedural portal per this many passable tiles (min 1, max 3).
        private const int PortalTileRatio = 200;

        public string Name => "portal-network";
        public GenerationPhase Phase => GenerationPhase.Interactions;

        public bool SupportsTemplate(WorldGenerationTemplate template) => true;

        // Finds link points with rectangular bounding boxes and walks square 6-neighbour reachability;
        // both are square-grid assumptions. A single-shell H3 planet has no inter-level portals anyway.
        public bool SupportsTopology(string? topology)
            => !string.Equals(topology, "h3", System.StringComparison.OrdinalIgnoreCase);

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null)
            {
                context.AddError("Portal network pass requires world instance");
                return;
            }

            var world = context.World;
            var rng = context.GeneratorContext.GetRandom("portal-network");

            bool isHub = false;
            if (context.GeneratorContext.GeneratorParams?.TryGetValue("isHub", out var isHubStr) == true && isHubStr != null)
            {
                isHub = isHubStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            if (isHub && context.GeneratorContext.GeneratorParams?.TryGetValue("portalDefinitions", out var portalDefsBlob) == true)
            {
                PlaceHubPortals(world, context, rng, portalDefsBlob ?? string.Empty);
            }
            else
            {
                PlaceProceduralPortals(world, context, rng);
            }
        }

        private void PlaceHubPortals(World world, WorldGenerationContext context, Random rng, string portalDefinitionsBlob)
        {
            List<PortalDefinitionDto> portalDefs;
            try
            {
                portalDefs = ParsePortalDefinitions(portalDefinitionsBlob);
            }
            catch (Exception ex)
            {
                context.AddError($"Failed to parse hub portal definitions: {ex.Message}");
                return;
            }

            if (portalDefs.Count == 0)
            {
                PlaceProceduralPortals(world, context, rng);
                return;
            }

            var candidateLocations = FindPortalLocations(world, context);

            if (candidateLocations.Count == 0)
            {
                candidateLocations = world.EntitiesByLocation.Keys
                    .Where(loc => world.PassableTerrain(loc))
                    .OrderBy(loc => loc.Z).ThenBy(loc => loc.Y).ThenBy(loc => loc.X)
                    .ToList();
            }

            var placed = new List<(string PortalId, WorldLocation Location)>();
            for (int i = 0; i < portalDefs.Count && candidateLocations.Count > 0; i++)
            {
                var portalDef = portalDefs[i];
                var locationIndex = rng.Next(candidateLocations.Count);
                var location = candidateLocations[locationIndex];
                candidateLocations.RemoveAt(locationIndex);

                var portalId = string.IsNullOrEmpty(portalDef.Id) ? $"hub-portal-{i}" : portalDef.Id;
                var targetTag = portalDef.WorldTag ?? portalDef.WorldTemplate;

                var portal = new PortalEntity(
                    portalId: portalId,
                    targetWorldId: null,
                    targetMapId: null,
                    targetTag: targetTag,
                    activation: portalDef.Activation
                );

                portal.Set(location);
                world.AddEntity(portal);

                RecordPortalMetadata(context, portalId, location, targetTag, portalDef.Activation);
                placed.Add((portalId, location));
            }

            VerifyReachability(world, context, placed);
        }

        private void PlaceProceduralPortals(World world, WorldGenerationContext context, Random rng)
        {
            var candidateLocations = FindPortalLocations(world, context);

            if (candidateLocations.Count == 0)
            {
                candidateLocations = world.EntitiesByLocation.Keys
                    .Where(loc => world.PassableTerrain(loc))
                    .OrderBy(loc => loc.Z).ThenBy(loc => loc.Y).ThenBy(loc => loc.X)
                    .ToList();
            }

            int portalCount = Math.Min(3, Math.Max(1, candidateLocations.Count / PortalTileRatio));

            var placed = new List<(string PortalId, WorldLocation Location)>();
            for (int i = 0; i < portalCount; i++)
            {
                if (candidateLocations.Count == 0)
                    break;

                int idx = rng.Next(candidateLocations.Count);
                var location = candidateLocations[idx];
                candidateLocations.RemoveAt(idx);

                var portalId = $"portal-{context.GeneratorContext.Seed}-{i}";
                var targetTag = SelectTargetTag(context, rng);

                var portal = new PortalEntity(
                    portalId: portalId,
                    targetWorldId: null,
                    targetMapId: null,
                    targetTag: targetTag,
                    activation: null
                );

                portal.Set(location);
                world.AddEntity(portal);

                RecordPortalMetadata(context, portalId, location, targetTag, null);
                placed.Add((portalId, location));
            }

            VerifyReachability(world, context, placed);
        }

        private static void RecordPortalMetadata(WorldGenerationContext context, string portalId, WorldLocation location, string? targetTag, string? activation)
        {
            if (!context.SharedData.ContainsKey("portals"))
            {
                context.SharedData["portals"] = new List<Dictionary<string, object>>();
            }

            if (context.SharedData["portals"] is not List<Dictionary<string, object>> portalList)
            {
                context.AddError("SharedData[\"portals\"] is not the expected list type — concurrent pass collision?");
                return;
            }

            portalList.Add(new Dictionary<string, object>
            {
                { "portalId", portalId },
                { "location", location },
                { "targetTag", targetTag ?? string.Empty },
                { "activation", activation ?? string.Empty }
            });
        }

        private void VerifyReachability(World world, WorldGenerationContext context, List<(string PortalId, WorldLocation Location)> placed)
        {
            if (placed.Count == 0)
                return;

            var start = context.StartLocation;
            if (start is null || start.IsNone)
                return;

            var reachable = BfsReachable(world, start);
            var unreachable = new List<string>();
            foreach (var (portalId, location) in placed)
            {
                if (!reachable.Contains(location))
                {
                    unreachable.Add($"{portalId}@{location.X},{location.Y},{location.Z}");
                }
            }

            // Record reachability for validation/diagnostics. The validation service can
            // promote this to a hard error per-template if portals must be reachable.
            if (unreachable.Count > 0)
            {
                context.GeneratorContext.PhaseArtifacts["portal-network:unreachable"] = unreachable;
            }
            context.GeneratorContext.PhaseArtifacts["portal-network:reachable-count"] = placed.Count - unreachable.Count;
            context.GeneratorContext.PhaseArtifacts["portal-network:placed-count"] = placed.Count;
        }

        private static HashSet<WorldLocation> BfsReachable(World world, WorldLocation start)
        {
            var visited = new HashSet<WorldLocation> { start };
            var queue = new Queue<WorldLocation>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var n in WorldLocationNeighbors.Cardinal6(current))
                {
                    if (visited.Contains(n))
                        continue;
                    if (!world.EntitiesByLocation.ContainsKey(n))
                        continue;
                    if (!world.PassableTerrain(n))
                        continue;
                    visited.Add(n);
                    queue.Enqueue(n);
                }
            }

            return visited;
        }

        // GetNeighbors removed — callers now use WorldLocationNeighbors.Cardinal6 directly.

        /// <summary>
        /// Parses portal definitions. Preferred format is JSON
        /// (<c>[{"id":"p1","worldTag":"dungeon","activation":"unlocked"}, ...]</c>).
        /// Falls back to the legacy <c>id:p1|worldTag:dungeon;...</c> format if the blob is not JSON,
        /// for compatibility with older data; the legacy format is fragile (no escaping) and should be migrated.
        /// </summary>
        internal static List<PortalDefinitionDto> ParsePortalDefinitions(string blob)
        {
            var result = new List<PortalDefinitionDto>();
            if (string.IsNullOrWhiteSpace(blob))
                return result;

            var trimmed = blob.TrimStart();
            if (trimmed.StartsWith('[') || trimmed.StartsWith('{'))
            {
                var parsed = JsonSerializer.Deserialize<List<PortalDefinitionDto>>(blob, JsonOptions);
                return parsed ?? result;
            }

            // Legacy pipe/semicolon format
            foreach (var portalStr in blob.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var def = new PortalDefinitionDto();
                bool hasAny = false;
                foreach (var part in portalStr.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = part.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (kv.Length != 2)
                        continue;
                    var key = kv[0].Trim();
                    var value = kv[1].Trim();
                    switch (key.ToLowerInvariant())
                    {
                        case "id": def.Id = value; hasAny = true; break;
                        case "worldtag": def.WorldTag = value; hasAny = true; break;
                        case "worldtemplate": def.WorldTemplate = value; hasAny = true; break;
                        case "maptag": def.MapTag = value; hasAny = true; break;
                        case "mapname": def.MapName = value; hasAny = true; break;
                        case "activation": def.Activation = value; hasAny = true; break;
                    }
                }
                if (hasAny)
                    result.Add(def);
            }

            return result;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private List<WorldLocation> FindPortalLocations(World world, WorldGenerationContext context)
        {
            var locations = new List<WorldLocation>();

            try
            {
                var allLocations = world.EntitiesByLocation?.Keys?.ToList() ?? new List<WorldLocation>();
                var objective = context.ObjectiveLocation;
                var start = context.StartLocation;

                if (objective != null && allLocations.Count > 0)
                {
                    foreach (var loc in allLocations)
                    {
                        if (loc == null)
                            continue;
                        if (!world.PassableTerrain(loc))
                            continue;
                        if (Math.Abs(loc.X - objective.X) < 10 && Math.Abs(loc.Y - objective.Y) < 10)
                        {
                            locations.Add(loc);
                        }
                    }
                }

                if (start != null && allLocations.Count > 0)
                {
                    foreach (var loc in allLocations)
                    {
                        if (loc == null)
                            continue;
                        if (!world.PassableTerrain(loc))
                            continue;
                        if (Math.Abs(loc.X - start.X) < 10 && Math.Abs(loc.Y - start.Y) < 10)
                        {
                            locations.Add(loc);
                        }
                    }
                }

                if (context.PrimaryPath.Count > 0)
                {
                    var pathLocations = new List<WorldLocation>();
                    foreach (var loc in context.PrimaryPath)
                    {
                        if (loc != null && world.PassableTerrain(loc))
                        {
                            pathLocations.Add(loc);
                        }
                    }

                    if (pathLocations.Count >= 3)
                    {
                        locations.Add(pathLocations[0]);
                        locations.Add(pathLocations[pathLocations.Count / 2]);
                        locations.Add(pathLocations[pathLocations.Count - 1]);
                    }
                    else if (pathLocations.Count > 0)
                    {
                        locations.Add(pathLocations[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                // Candidate finding is best-effort — failures here are recoverable via the
                // "any passable terrain" fallback in the callers. Record under PhaseArtifacts
                // for diagnostics rather than escalating to context.AddError (which would abort).
                context.GeneratorContext.PhaseArtifacts["portal-network:find-locations-error"] = ex.ToString();
            }

            // Sort to remove any dependency on Dictionary.Keys enumeration order.
            return locations.Distinct()
                .OrderBy(loc => loc.Z).ThenBy(loc => loc.Y).ThenBy(loc => loc.X)
                .ToList();
        }

        private static string? SelectTargetTag(WorldGenerationContext context, Random rng)
        {
            var template = context.Request.Template;
            if (template == WorldGenerationTemplate.Outdoor)
            {
                var tags = new[] { "hub", "city", "outdoor" };
                return tags[rng.Next(tags.Length)];
            }
            else
            {
                var tags = new[] { "hub", "dungeon" };
                return tags[rng.Next(tags.Length)];
            }
        }

        internal sealed class PortalDefinitionDto
        {
            public string? Id { get; set; }
            public string? WorldTag { get; set; }
            public string? WorldTemplate { get; set; }
            public string? MapTag { get; set; }
            public string? MapName { get; set; }
            public string? Activation { get; set; }
        }
    }
}
