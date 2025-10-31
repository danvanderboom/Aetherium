using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Server.Agents.Analysis;

namespace Aetherium.Server.WorldGen.Adaptation
{
    /// <summary>
    /// Injects dynamic content into the world based on agent behavior in real-time.
    /// </summary>
    public static class DynamicContentInjector
    {
        /// <summary>
        /// Injects helpful items when agent struggles.
        /// </summary>
        public static void InjectHelpfulItems(
            World world,
            List<ContentNeed> contentNeeds,
            WorldLocation agentLocation,
            int radius = 5)
        {
            if (world == null || contentNeeds == null || contentNeeds.Count == 0)
                return;

            // Find suitable locations near agent
            var candidateLocations = FindNearbyLocations(world, agentLocation, radius);

            if (candidateLocations.Count == 0)
                return;

            // Inject items based on highest priority needs
            var highPriorityNeeds = contentNeeds
                .Where(n => n.Priority >= 0.7)
                .OrderByDescending(n => n.Priority)
                .Take(2)
                .ToList();

            foreach (var need in highPriorityNeeds)
            {
                var location = candidateLocations.FirstOrDefault();
                if (location == null)
                    continue;

                candidateLocations.Remove(location);

                var item = CreateItemForNeed(need);
                if (item != null)
                {
                    item.Set(location);
                    world.AddEntity(item);
                }
            }
        }

        /// <summary>
        /// Adjusts monster density in an area based on agent combat performance.
        /// </summary>
        public static void AdjustMonsterDensity(
            World world,
            BehaviorAnalysis behaviorAnalysis,
            WorldLocation areaCenter,
            int radius = 10)
        {
            if (world == null || behaviorAnalysis == null)
                return;

            // Check if agent struggles with combat
            var combatStruggles = behaviorAnalysis.StrugglePatterns
                .Where(s => s.ContextType.Contains("combat"))
                .ToList();

            if (combatStruggles.Count > 0)
            {
                // Reduce monster density
                var monsters = new List<Entity>();
                foreach (var kvp in world.EntitiesByLocation.Where(kvp => IsInRadius(kvp.Key, areaCenter, radius)))
                {
                    foreach (var entityKvp in kvp.Value)
                    {
                        if (entityKvp.Value.Get<Health>() != null) // Monsters have health
                        {
                            monsters.Add(entityKvp.Value);
                        }
                    }
                }

                // Remove some monsters (reduce density)
                var toRemove = monsters.Take(Math.Max(1, monsters.Count / 3)).ToList();
                foreach (var monster in toRemove)
                {
                    world.RemoveEntity(monster.EntityId);
                }
            }
        }

        /// <summary>
        /// Adds hints when agent is stuck.
        /// </summary>
        public static void InjectHints(
            World world,
            List<StrugglePattern> strugglePatterns,
            WorldLocation agentLocation,
            int hintRadius = 3)
        {
            if (world == null || strugglePatterns == null || strugglePatterns.Count == 0)
                return;

            // Find locations for hints
            var hintLocations = FindNearbyLocations(world, agentLocation, hintRadius);

            foreach (var struggle in strugglePatterns.Take(2))
            {
                if (hintLocations.Count == 0)
                    break;

                var location = hintLocations.First();
                hintLocations.Remove(location);

                // Create a hint entity (could be a sign, note, etc.)
                var hint = CreateHintEntity(struggle);
                if (hint != null)
                {
                    hint.Set(location);
                    world.AddEntity(hint);
                }
            }
        }

        /// <summary>
        /// Adjusts puzzle difficulty based on agent progress.
        /// </summary>
        public static void AdjustPuzzleDifficulty(
            World world,
            BehaviorAnalysis behaviorAnalysis,
            WorldLocation puzzleLocation)
        {
            if (world == null || behaviorAnalysis == null)
                return;

            // Find entities at puzzle location
            var entities = new List<Entity>();
            if (world.EntitiesByLocation.TryGetValue(puzzleLocation, out var locationEntities))
            {
                foreach (var entityKvp in locationEntities)
                {
                    entities.Add(entityKvp.Value);
                }
            }

            // Check if agent struggles with puzzles
            var puzzleStruggles = behaviorAnalysis.StrugglePatterns
                .Where(s => s.ContextType.Contains("puzzle"))
                .ToList();

            if (puzzleStruggles.Count > 0)
            {
                // Simplify puzzles (remove complexity)
                foreach (var entity in entities)
                {
                    // Remove some complexity components if they exist
                    // This is a placeholder - actual implementation would depend on puzzle structure
                    Console.WriteLine($"[DynamicContentInjector] Simplifying puzzle at {puzzleLocation}");
                }
            }
        }

        private static List<WorldLocation> FindNearbyLocations(World world, WorldLocation center, int radius)
        {
            var locations = new List<WorldLocation>();

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx * dx + dy * dy > radius * radius)
                        continue;

                    var location = new WorldLocation(center.X + dx, center.Y + dy, center.Z);

                    // Check if location is passable and not occupied
                    if (world.PassableTerrain(location) && !world.EntitiesByLocation.ContainsKey(location))
                    {
                        locations.Add(location);
                    }
                }
            }

            return locations.OrderBy(l => Math.Abs(l.X - center.X) + Math.Abs(l.Y - center.Y)).ToList();
        }

        private static bool IsInRadius(WorldLocation location, WorldLocation center, int radius)
        {
            var dx = location.X - center.X;
            var dy = location.Y - center.Y;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static Entity? CreateItemForNeed(ContentNeed need)
        {
            // Create appropriate item entity based on need
            // This is a placeholder - actual implementation would create real item entities
            var itemType = need.SuggestedContent.FirstOrDefault();
            if (string.IsNullOrEmpty(itemType))
                return null;

            // Placeholder: create a basic item entity
            // In production, would use proper entity creation from entity registry
            Console.WriteLine($"[DynamicContentInjector] Would create item '{itemType}' for need '{need.NeedType}'");
            return null; // Return null for now - actual implementation needed
        }

        private static Entity? CreateHintEntity(StrugglePattern struggle)
        {
            // Create a hint entity based on struggle pattern
            // This is a placeholder - actual implementation would create real hint entities
            var hintText = GetHintText(struggle);
            Console.WriteLine($"[DynamicContentInjector] Would create hint entity with text: '{hintText}'");
            return null; // Return null for now - actual implementation needed
        }

        private static string GetHintText(StrugglePattern struggle)
        {
            return struggle.ContextType switch
            {
                "navigation_failure" => "Follow the markers to find your way",
                "key_lock_failure" => "Look for keys near locked doors",
                "combat_failure" => "Use tools to help in combat",
                "puzzle_failure" => "Try different interactions to solve puzzles",
                _ => "Take your time and explore carefully"
            };
        }
    }
}

