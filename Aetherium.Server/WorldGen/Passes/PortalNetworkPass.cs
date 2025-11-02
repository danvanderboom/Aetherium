using System;
using System.Collections.Generic;
using System.Linq;
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
        public string Name => "portal-network";
        public GenerationPhase Phase => GenerationPhase.Interactions;

        public bool SupportsTemplate(WorldGenerationTemplate template) => true; // Works with all templates

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null)
            {
                context.AddError("Portal network pass requires world instance");
                return;
            }

            var world = context.World;
            var rng = context.GeneratorContext.GetRandom("portal-network");

            // Check if this is a hub world with portal definitions
            bool isHub = false;
            if (context.GeneratorContext.GeneratorParams?.TryGetValue("isHub", out var isHubStr) == true && isHubStr != null)
            {
                isHub = isHubStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            if (isHub && context.GeneratorContext.GeneratorParams?.TryGetValue("portalDefinitions", out var portalDefsObj) == true)
            {
                // Hub world: place portals from definition
                PlaceHubPortals(world, context, rng, portalDefsObj.ToString() ?? "");
            }
            else
            {
                // Procedural world: place portals randomly
                PlaceProceduralPortals(world, context, rng);
            }
        }

        /// <summary>
        /// Places portals for a hub world based on portal definitions from the hub JSON.
        /// </summary>
        private void PlaceHubPortals(World world, WorldGenerationContext context, Random rng, string portalDefinitions)
        {
            // Parse portal definitions (format: "id:portal1|worldTag:dungeon|activation:unlocked;id:portal2|worldTag:city")
            var portalDefs = ParsePortalDefinitions(portalDefinitions);
            
            if (portalDefs.Count == 0)
            {
                // Fallback to procedural placement if no valid definitions
                PlaceProceduralPortals(world, context, rng);
                return;
            }

            var candidateLocations = FindPortalLocations(world, context, rng);
            
            if (candidateLocations.Count == 0)
            {
                // Fallback: use any passable location
                candidateLocations = world.EntitiesByLocation.Keys
                    .Where(loc => world.PassableTerrain(loc))
                    .ToList();
            }

            // Place one portal per definition (up to available locations)
            int portalsPlaced = 0;
            for (int i = 0; i < portalDefs.Count && candidateLocations.Count > 0; i++)
            {
                var portalDef = portalDefs[i];
                var locationIndex = portalsPlaced % candidateLocations.Count;
                var location = candidateLocations[locationIndex];
                candidateLocations.RemoveAt(locationIndex); // Remove used location

                // Determine target tag from portal definition
                var targetTag = portalDef.TryGetValue("worldTag", out var tag) ? tag?.ToString() :
                               portalDef.TryGetValue("worldTemplate", out var template) ? template?.ToString() : null;

                var portalId = portalDef.TryGetValue("id", out var id) ? id?.ToString() ?? $"hub-portal-{i}" : $"hub-portal-{i}";
                var activation = portalDef.TryGetValue("activation", out var act) ? act?.ToString() : null;

                var portal = new PortalEntity(
                    portalId: portalId,
                    targetWorldId: null,
                    targetMapId: null,
                    targetTag: targetTag,
                    activation: activation
                );

                portal.Set(location);
                world.AddEntity(portal);

                // Store portal metadata
                if (!context.SharedData.ContainsKey("portals"))
                {
                    context.SharedData["portals"] = new List<Dictionary<string, object>>();
                }

                var portalList = context.SharedData["portals"] as List<Dictionary<string, object>>;
                portalList?.Add(new Dictionary<string, object>
                {
                    { "portalId", portalId },
                    { "location", location },
                    { "targetTag", targetTag ?? "" },
                    { "activation", activation ?? "" }
                });

                portalsPlaced++;
            }
        }

        /// <summary>
        /// Places portals for a procedural world (original behavior).
        /// </summary>
        private void PlaceProceduralPortals(World world, WorldGenerationContext context, Random rng)
        {
            // Find strategic locations for portals:
            // - Near exits/boundaries
            // - At major landmarks (if available)
            // - Distributed across key areas
            var candidateLocations = FindPortalLocations(world, context, rng);

            if (candidateLocations.Count == 0)
            {
                // Fallback: use any passable location
                candidateLocations = world.EntitiesByLocation.Keys
                    .Where(loc => world.PassableTerrain(loc))
                    .ToList();
            }

            // Place 1-3 portals depending on world size
            int portalCount = Math.Min(3, Math.Max(1, candidateLocations.Count / 200));
            
            for (int i = 0; i < portalCount; i++)
            {
                if (candidateLocations.Count == 0)
                    break;

                var location = candidateLocations[rng.Next(candidateLocations.Count)];
                candidateLocations.Remove(location); // Avoid placing multiple portals at same location

                // Generate portal with link hints
                var portalId = $"portal-{context.GeneratorContext.Seed}-{i}";
                var targetTag = SelectTargetTag(world, context, rng);
                
                var portal = new PortalEntity(
                    portalId: portalId,
                    targetWorldId: null, // Will be resolved at runtime via cluster grain
                    targetMapId: null,
                    targetTag: targetTag,
                    activation: null // No activation requirement by default
                );

                portal.Set(location);
                world.AddEntity(portal);

                // Store portal metadata in context for cluster registration
                if (!context.SharedData.ContainsKey("portals"))
                {
                    context.SharedData["portals"] = new List<Dictionary<string, object>>();
                }

                var portalList = context.SharedData["portals"] as List<Dictionary<string, object>>;
                portalList?.Add(new Dictionary<string, object>
                {
                    { "portalId", portalId },
                    { "location", location },
                    { "targetTag", targetTag ?? "" }
                });
            }
        }

        /// <summary>
        /// Parses portal definitions from serialized string format.
        /// Format: "id:portal1|worldTag:dungeon|activation:unlocked;id:portal2|worldTag:city"
        /// </summary>
        private List<Dictionary<string, string>> ParsePortalDefinitions(string portalDefinitions)
        {
            var result = new List<Dictionary<string, string>>();
            
            if (string.IsNullOrWhiteSpace(portalDefinitions))
                return result;

            var portalStrings = portalDefinitions.Split(';', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var portalStr in portalStrings)
            {
                var portalDef = new Dictionary<string, string>();
                var parts = portalStr.Split('|', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var part in parts)
                {
                    var keyValue = part.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (keyValue.Length == 2)
                    {
                        portalDef[keyValue[0].Trim()] = keyValue[1].Trim();
                    }
                }
                
                if (portalDef.Count > 0)
                {
                    result.Add(portalDef);
                }
            }
            
            return result;
        }

        private List<WorldLocation> FindPortalLocations(World world, WorldGenerationContext context, Random rng)
        {
            var locations = new List<WorldLocation>();

            try
            {
                var allLocations = world.EntitiesByLocation?.Keys?.ToList() ?? new List<WorldLocation>();

                // Snapshot potentially nullable context values to avoid racey reads in predicates
                var objective = context.ObjectiveLocation;
                var start = context.StartLocation;

                // Prefer locations near objective
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

                // Prefer start location vicinity
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

                // Prefer locations along primary path
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

                    // Sample points along path (beginning, middle, end)
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
            catch
            {
                // Be conservative: if anything goes wrong while selecting candidates,
                // return what we have so far and let fallback logic handle placement.
            }

            return locations.Distinct().ToList();
        }

        private string? SelectTargetTag(World world, WorldGenerationContext context, Random rng)
        {
            // Determine target tag based on world template or constraints
            var template = context.Request.Template;

            // Could be enhanced with narrative constraints
            // For now, simple logic:
            if (template == WorldGenerationTemplate.Outdoor)
            {
                // Outdoor worlds might link to hubs, cities, or other outdoor areas
                var tags = new[] { "hub", "city", "outdoor" };
                return tags[rng.Next(tags.Length)];
            }
            else
            {
                // Dungeon worlds might link to other dungeons or hubs
                var tags = new[] { "hub", "dungeon" };
                return tags[rng.Next(tags.Length)];
            }
        }
    }
}

