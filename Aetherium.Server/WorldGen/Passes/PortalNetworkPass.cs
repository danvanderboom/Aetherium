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

