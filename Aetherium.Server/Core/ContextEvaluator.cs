using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;

namespace Aetherium.Server
{
    /// <summary>
    /// Evaluates game context for a session and optional target entity.
    /// Produces a set of context tags (e.g., "near-door", "in-forest", "in-combat", "indoors").
    /// </summary>
    public class ContextEvaluator
    {
        /// <summary>
        /// Evaluates context tags for a game session and optional target entity.
        /// </summary>
        /// <param name="session">The game session to evaluate context for.</param>
        /// <param name="targetEntityId">Optional target entity ID to include in context evaluation.</param>
        /// <returns>A set of context tag strings (e.g., "near-door", "indoors", "in-forest", "in-combat").</returns>
        public static HashSet<string> EvaluateContext(GameSession session, string? targetEntityId = null)
        {
            if (session == null || session.ViewLocation == null || session.World == null)
                return new HashSet<string>();
            return EvaluateContext(session.World, session.ViewLocation, targetEntityId);
        }

        /// <summary>
        /// Session-free overload for grain-routed callers that don't have a
        /// <see cref="GameSession"/>. Same semantics as the session overload.
        /// </summary>
        public static HashSet<string> EvaluateContext(World world, WorldLocation playerLocation, string? targetEntityId = null)
        {
            var contextTags = new HashSet<string>();

            if (world == null || playerLocation == null)
                return contextTags;

            // Check for doors nearby (adjacent or same location)
            var deltas = new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) };
            foreach (var (dx, dy) in deltas)
            {
                var loc = playerLocation.FromDelta(dx, dy, 0);
                if (world.EntitiesByLocation.TryGetValue(loc, out var entities))
                {
                    foreach (var entity in entities.Values)
                    {
                        var door = entity.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
                        if (door != null)
                        {
                            contextTags.Add("near-door");
                            break;
                        }
                    }
                }
                if (contextTags.Contains("near-door"))
                    break;
            }

            // Check if target entity is a door
            if (!string.IsNullOrEmpty(targetEntityId) && world.Entities.TryGetValue(targetEntityId, out var target))
            {
                var door = target.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
                if (door != null)
                {
                    contextTags.Add("target-is-door");
                }
            }

            // Check terrain/tile types for indoor/outdoor, forest, etc.
            var terrain = world.GetTerrain(playerLocation);
            if (terrain != null)
            {
                var terrainTypeName = terrain.Type?.Name?.ToLowerInvariant() ?? string.Empty;
                if (terrainTypeName.Contains("forest"))
                {
                    contextTags.Add("in-forest");
                }
                if (terrainTypeName.Contains("indoor") || terrainTypeName.Contains("cave"))
                {
                    contextTags.Add("indoors");
                }
                else
                {
                    contextTags.Add("outdoors");
                }
            }

            // Check tile types
            if (world.EntitiesByLocation.TryGetValue(playerLocation, out var entitiesAtLocation))
            {
                foreach (var entity in entitiesAtLocation.Values)
                {
                    var tile = entity.Get<Tile>();
                    if (tile != null && tile.Type != null)
                    {
                        var tileTypeName = tile.Type.Name?.ToLowerInvariant() ?? string.Empty;
                        if (tileTypeName.Contains("indoor") || tileTypeName == "indoors")
                        {
                            contextTags.Add("indoors");
                            contextTags.Remove("outdoors");
                        }
                    }
                }
            }

            // Check if target is adjacent to player
            if (!string.IsNullOrEmpty(targetEntityId) && world.Entities.TryGetValue(targetEntityId, out var targetEntity))
            {
                var targetLoc = targetEntity.Get<WorldLocation>();
                if (targetLoc != null)
                {
                    var distance = Math.Abs(targetLoc.X - playerLocation.X) + 
                                  Math.Abs(targetLoc.Y - playerLocation.Y) + 
                                  Math.Abs(targetLoc.Z - playerLocation.Z);
                    if (distance <= 1)
                    {
                        contextTags.Add("adjacent-target");
                    }
                }
            }

            // Combat detection: in-combat when a living hostile (a Monster with health) is adjacent.
            // Replaces the former `if (false)` placeholder now that combat (P3-7) exists.
            foreach (var entity in world.Entities.Values)
            {
                if (entity is not Monster)
                    continue;

                // Entity.Get<T>() throws when absent, so gate on Has<T>() first.
                if (!entity.Has<Health>() || !entity.Has<WorldLocation>())
                    continue;
                if (entity.Get<Health>().Level <= 0)
                    continue;

                var loc = entity.Get<WorldLocation>();

                var dist = Math.Abs(loc.X - playerLocation.X)
                         + Math.Abs(loc.Y - playerLocation.Y)
                         + Math.Abs(loc.Z - playerLocation.Z);
                if (dist <= 1)
                {
                    contextTags.Add("in-combat");
                    break;
                }
            }

            return contextTags;
        }

        /// <summary>
        /// Checks if all required context tags are present.
        /// </summary>
        /// <param name="contextTags">The current context tags.</param>
        /// <param name="requiredTags">The required context tags to check.</param>
        /// <returns>True if all required tags are present, false otherwise.</returns>
        public static bool MeetsRequirements(HashSet<string> contextTags, IEnumerable<string> requiredTags)
        {
            if (requiredTags == null || !requiredTags.Any())
                return true;

            return requiredTags.All(tag => contextTags.Contains(tag));
        }
    }
}

