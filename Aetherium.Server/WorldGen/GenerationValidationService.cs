using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.WorldGen
{
    public sealed class GenerationValidationService
    {
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
            var isAdvancedDungeon = string.Equals(context.Request.LayoutGenerator, "AdvancedDungeon", StringComparison.OrdinalIgnoreCase);

            if (start is null || start.IsNone)
            {
                result.AddError("Start location missing");
                return;
            }

            if (!isAdvancedDungeon && (objective is null || objective.IsNone))
            {
                return;
            }

            if (objective is null || objective.IsNone)
            {
                result.AddError("Objective location missing");
                return;
            }

            var lockedDoor = FindLockedDoor(world);
            var keyLocation = lockedDoor.HasValue
                ? FindMatchingKey(world, lockedDoor.Value.KeyShape)
                : null;

            var startToKeyPath = lockedDoor.HasValue && keyLocation != null
                ? FindPath(world, start, keyLocation!, loc => lockedDoor.Value.Location.Equals(loc))
                : new List<WorldLocation>();

            if (lockedDoor.HasValue && keyLocation != null && startToKeyPath.Count == 0)
            {
                result.AddError("No access path from start to key before encountering locked door");
            }
            else if (lockedDoor.HasValue && keyLocation != null)
            {
                result.ProofArtifacts["start-to-key"] = startToKeyPath;
            }

            var blockedDoorLocations = new HashSet<WorldLocation>();
            if (lockedDoor.HasValue)
            {
                blockedDoorLocations.Add(lockedDoor.Value.Location);
            }

            var pathAfterUnlock = FindPath(world, start!, objective!, loc => false);
            if (pathAfterUnlock.Count == 0)
            {
                result.AddError("Objective unreachable after unlocking gating");
            }
            else
            {
                result.ProofArtifacts["start-to-objective"] = pathAfterUnlock;
            }

            if (lockedDoor.HasValue && keyLocation != null)
            {
                var doorToObjective = FindPath(world, lockedDoor.Value.Location, objective!, loc => false);
                if (doorToObjective.Count == 0)
                {
                    result.AddError("No path from locked door to objective after unlock");
                }
            }
        }

        private void ValidateMetrics(WorldGenerationContext context, GenerationValidationResult result)
        {
            var metrics = context.GeneratorContext.Metrics;
            var isAdvancedDungeon = string.Equals(context.Request.LayoutGenerator, "AdvancedDungeon", StringComparison.OrdinalIgnoreCase);
            var parameters = context.GeneratorContext.GeneratorParams ?? new Dictionary<string, string>();
            var objective = context.ObjectiveLocation;

            double minLoopRatio = parameters.TryGetValue("minLoopRatio", out var loopParam) && double.TryParse(loopParam, out var parsedLoop)
                ? parsedLoop
                : 0.10;

            if (isAdvancedDungeon && metrics.LoopRatio < minLoopRatio)
            {
                result.AddError($"Loop ratio {metrics.LoopRatio:F2} below required minimum {minLoopRatio:F2}");
            }

            if (isAdvancedDungeon && metrics.BranchingFactor < 1.2)
            {
                result.AddError("Branching factor too low for meaningful exploration");
            }

            if (isAdvancedDungeon && metrics.TrapsPlaced < 1)
            {
                result.AddError("No traps placed in dungeon");
            }

            if (isAdvancedDungeon && metrics.SecretsPlaced < 1)
            {
                result.AddError("No secret content placed");
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

                foreach (var neighbor in GetNeighbors(current))
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

        private static IEnumerable<WorldLocation> GetNeighbors(WorldLocation location)
        {
            yield return location.FromDelta(1, 0, 0);
            yield return location.FromDelta(-1, 0, 0);
            yield return location.FromDelta(0, 1, 0);
            yield return location.FromDelta(0, -1, 0);
            yield return location.FromDelta(0, 0, 1);
            yield return location.FromDelta(0, 0, -1);
        }
    }
}



