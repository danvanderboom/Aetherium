using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.WorldGen
{
    public sealed class GenerationValidationService
    {
        // Default thresholds for AdvancedDungeon validation. All are overridable via
        // GeneratorParameters under the matching key.
        private const double DefaultMinLoopRatio = 0.10;
        private const double DefaultMinBranchingFactor = 1.2;
        private const int DefaultMinTraps = 1;
        private const int DefaultMinSecrets = 1;

        public GenerationValidationResult Validate(WorldGenerationContext context)
        {
            var result = new GenerationValidationResult();

            if (context.World == null)
            {
                result.AddError("World is null");
                return result;
            }

            ValidateConnectivity(context, result);
            ValidateMetrics(context, result);
            ValidateTokens(context, result);
            ValidateBudgets(context, result);

            return result;
        }

        private void ValidateConnectivity(WorldGenerationContext context, GenerationValidationResult result)
        {
            var world = context.World!;
            var start = context.StartLocation;
            var objective = context.ObjectiveLocation;

            if (start is null || start.IsNone)
            {
                result.AddError("Start location missing");
                return;
            }

            // Some templates (Maze, simple Outdoor, hub worlds) legitimately do not produce an
            // objective. Connectivity validation only runs end-to-end for templates that declare
            // they require an objective (currently AdvancedDungeon). The previous "silent success
            // for any non-AdvancedDungeon" path is preserved here, but with an explicit policy
            // check so future objective-producing templates can opt in.
            if (objective is null || objective.IsNone)
            {
                if (RequiresObjective(context))
                {
                    result.AddError("Objective location missing");
                }
                return;
            }

            // Enumerate all locked doors (was: only the first). Each door must satisfy:
            //   (a) a matching key exists in the world,
            //   (b) the key is reachable from start without traversing any locked door
            //       (the player obtains every key before encountering its door).
            //
            // After all doors are "unlocked", a path from start to objective must exist —
            // modeled by running BFS with the door tiles treated as passable (their cell
            // already has Indoors terrain, so they're naturally walkable; the previous
            // "isBlocked => false" was hollow, doing no actual unlock-modeling).
            var lockedDoors = FindAllLockedDoors(world);
            var lockedDoorLocations = lockedDoors.Select(d => d.Location).ToHashSet();

            foreach (var door in lockedDoors)
            {
                var keyLocation = FindMatchingKey(world, door.KeyShape);
                if (keyLocation == null)
                {
                    result.AddError($"Locked door (key '{door.KeyShape}') has no matching key in world");
                    continue;
                }

                // The key must be reachable from start without traversing *any* locked door.
                var startToKey = FindPath(world, start, keyLocation, lockedDoorLocations.Contains);
                if (startToKey.Count == 0)
                {
                    result.AddError($"Key '{door.KeyShape}' at {keyLocation.X},{keyLocation.Y} unreachable from start without traversing a locked door");
                }
                else
                {
                    result.ProofArtifacts[$"start-to-key:{door.KeyShape}"] = startToKey;
                }
            }

            // Objective must be reachable from start with all locked doors *unlocked* (i.e.,
            // door tiles treated as passable — same as the validator's existing "Indoors" terrain).
            var startToObjective = FindPath(world, start, objective, _ => false);
            if (startToObjective.Count == 0)
            {
                result.AddError("Objective unreachable from start after unlocking all gating");
            }
            else
            {
                result.ProofArtifacts["start-to-objective"] = startToObjective;
            }

            // Also confirm: with *every* locked door blocked, start is NOT connected to objective.
            // Otherwise the doors don't actually gate the objective (no enforcement of progression).
            if (lockedDoors.Count > 0)
            {
                var startToObjectiveWithDoorsBlocked = FindPath(world, start, objective, lockedDoorLocations.Contains);
                if (startToObjectiveWithDoorsBlocked.Count > 0)
                {
                    result.ProofArtifacts["start-to-objective:doors-bypassable"] = startToObjectiveWithDoorsBlocked;
                    // Note: not a hard error — the gating contract still permits alternative routes
                    // (e.g., secret passages). Recorded so callers can audit gating strength.
                }
            }
        }

        private static List<(WorldLocation Location, string KeyShape)> FindAllLockedDoors(World world)
        {
            var doors = new List<(WorldLocation, string)>();
            // Sort entities by Id for deterministic iteration (Dictionary.Values order is
            // implementation-defined).
            foreach (var entity in world.Entities.Values.OrderBy(e => e.EntityId))
            {
                if (!entity.Has<OpensAndCloses>())
                    continue;
                var opens = entity.Get<OpensAndCloses>();
                if (opens == null || !opens.IsLocked || string.IsNullOrEmpty(opens.KeyShape))
                    continue;
                var loc = entity.Get<WorldLocation>();
                if (loc == null)
                    continue;
                doors.Add((loc, opens.KeyShape));
            }
            return doors;
        }

        private void ValidateMetrics(WorldGenerationContext context, GenerationValidationResult result)
        {
            var metrics = context.GeneratorContext.Metrics;
            var parameters = context.GeneratorContext.GeneratorParams ?? new Dictionary<string, string>();
            var resolvedLayout = !string.IsNullOrWhiteSpace(context.Request.LayoutGenerator)
                ? context.Request.LayoutGenerator
                : (parameters.TryGetValue("layoutGenerator", out var l) ? l : string.Empty);
            var isAdvancedDungeon = string.Equals(resolvedLayout, "AdvancedDungeon", StringComparison.OrdinalIgnoreCase);
            var objective = context.ObjectiveLocation;

            double minLoopRatio = TryGetDouble(parameters, "minLoopRatio", DefaultMinLoopRatio);
            double minBranchingFactor = TryGetDouble(parameters, "minBranchingFactor", DefaultMinBranchingFactor);
            int minTraps = TryGetInt(parameters, "minTraps", DefaultMinTraps);
            int minSecrets = TryGetInt(parameters, "minSecrets", DefaultMinSecrets);

            if (isAdvancedDungeon && metrics.LoopRatio < minLoopRatio)
            {
                result.AddError($"Loop ratio {metrics.LoopRatio:F2} below required minimum {minLoopRatio:F2}");
            }

            if (isAdvancedDungeon && metrics.BranchingFactor < minBranchingFactor)
            {
                result.AddError($"Branching factor {metrics.BranchingFactor:F2} below required minimum {minBranchingFactor:F2}");
            }

            if (isAdvancedDungeon && metrics.TrapsPlaced < minTraps)
            {
                result.AddError($"Trap count {metrics.TrapsPlaced} below required minimum {minTraps}");
            }

            if (isAdvancedDungeon && metrics.SecretsPlaced < minSecrets)
            {
                result.AddError($"Secret count {metrics.SecretsPlaced} below required minimum {minSecrets}");
            }

            if (metrics.LockedDoors > 0 && metrics.KeysPlaced == 0)
            {
                result.AddError("Locked doors present without matching keys");
            }

            if (context.GeneratorContext.Levels > 1 && objective != null && !objective.IsNone)
            {
                var start = context.StartLocation;
                if (start != null && !start.IsNone)
                {
                    var verticalPath = FindPath(context.World!, start!, objective!, _ => false);
                    if (verticalPath.Count == 0)
                    {
                        result.AddError("Multi-level dungeon lacks cross-level connectivity");
                    }
                }
            }
        }

        private void ValidateTokens(WorldGenerationContext context, GenerationValidationResult result)
        {
            foreach (var token in context.Request.Narrative.Tokens)
            {
                var key = $"narrative-token:{token.TokenId}";
                if (!context.SharedData.ContainsKey(key))
                {
                    result.AddError($"Narrative token '{token.TokenId}' not satisfied");
                }
            }
        }

        private void ValidateBudgets(WorldGenerationContext context, GenerationValidationResult result)
        {
            var timeoutMs = context.Request.PhaseTimeout.TotalMilliseconds;
            foreach (var kvp in context.GeneratorContext.Metrics.PhaseDurationsMs)
            {
                if (kvp.Value > timeoutMs)
                {
                    result.AddError($"Phase '{kvp.Key}' exceeded budget ({kvp.Value:F1} ms > {timeoutMs:F1} ms)");
                }
            }
        }

        private static double TryGetDouble(IReadOnlyDictionary<string, string> parameters, string key, double defaultValue)
        {
            return parameters.TryGetValue(key, out var v) && double.TryParse(v, out var parsed)
                ? parsed
                : defaultValue;
        }

        private static int TryGetInt(IReadOnlyDictionary<string, string> parameters, string key, int defaultValue)
        {
            return parameters.TryGetValue(key, out var v) && int.TryParse(v, out var parsed)
                ? parsed
                : defaultValue;
        }

        private static bool RequiresObjective(WorldGenerationContext context)
        {
            var resolved = !string.IsNullOrWhiteSpace(context.Request.LayoutGenerator)
                ? context.Request.LayoutGenerator
                : (context.GeneratorContext.GeneratorParams?.TryGetValue("layoutGenerator", out var l) == true ? l : string.Empty);

            // Only generators that produce a critical-path objective (start -> goal) require validation.
            return string.Equals(resolved, "AdvancedDungeon", StringComparison.OrdinalIgnoreCase);
        }

        private static (WorldLocation Location, string KeyShape)? FindLockedDoor(World world)
        {
            foreach (var entity in world.Entities.Values)
            {
                if (!entity.Has<OpensAndCloses>())
                    continue;
                var opens = entity.Get<OpensAndCloses>();
                if (opens == null || !opens.IsLocked || string.IsNullOrEmpty(opens.KeyShape))
                    continue;
                var loc = entity.Get<WorldLocation>();
                if (loc == null)
                    continue;
                return (loc, opens.KeyShape);
            }
            return null;
        }

        private static WorldLocation? FindMatchingKey(World world, string keyShape)
        {
            foreach (var entity in world.Entities.Values)
            {
                if (!entity.Has<Key>())
                    continue;
                var key = entity.Get<Key>();
                if (key == null || !string.Equals(key.KeyId, keyShape, StringComparison.OrdinalIgnoreCase))
                    continue;
                var loc = entity.Get<WorldLocation>();
                if (loc == null)
                    continue;
                return loc;
            }
            return null;
        }

        private static List<WorldLocation> FindPath(World world, WorldLocation start, WorldLocation goal, Func<WorldLocation, bool> isBlocked)
        {
            var visited = new HashSet<WorldLocation>();
            var queue = new Queue<WorldLocation>();
            var prev = new Dictionary<WorldLocation, WorldLocation>();

            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == goal)
                    break;

                foreach (var neighbor in WorldLocationNeighbors.Cardinal6(current))
                {
                    if (visited.Contains(neighbor))
                        continue;
                    if (isBlocked(neighbor))
                        continue;
                    if (!world.EntitiesByLocation.ContainsKey(neighbor))
                        continue;
                    if (!world.PassableTerrain(neighbor))
                        continue;

                    visited.Add(neighbor);
                    prev[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }

            if (!visited.Contains(goal))
                return new List<WorldLocation>();

            var path = new List<WorldLocation> { goal };
            var node = goal;
            while (prev.TryGetValue(node, out var parent))
            {
                path.Add(parent);
                node = parent;
            }

            path.Reverse();
            return path;
        }

        // GetNeighbors removed — callers now use WorldLocationNeighbors.Cardinal6 directly.
    }
}



