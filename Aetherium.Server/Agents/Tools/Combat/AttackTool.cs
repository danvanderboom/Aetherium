using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Combat
{
    /// <summary>
    /// Attacks an adjacent target, routing through the session's mutation gateway so it applies to
    /// canonical (grain) state and fans out to all joined sessions.
    /// </summary>
    [AgentTool("attack", "Attack an adjacent entity by its ID",
        Categories = new[] { "combat" })]
    public class AttackTool : IAgentTool
    {
        public string ToolId => "attack";
        public string Description => "Attack an adjacent entity by its ID, dealing damage; defeats it when its health reaches zero";
        public IEnumerable<string> Categories => new[] { "combat" };
        public IEnumerable<string> RequiredCapabilities => Array.Empty<string>();

        public ToolParameterSchema GetParameterSchema() => new()
        {
            Properties = new Dictionary<string, ParameterDefinition>
            {
                ["targetEntityId"] = new() { Type = "string", Description = "Entity ID of the target to attack" }
            },
            Required = new() { "targetEntityId" }
        };

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!args.TryGetValue("targetEntityId", out var targetObj) || string.IsNullOrWhiteSpace(targetObj?.ToString()))
                return ToolExecutionResult.Error("Missing required parameter: targetEntityId");

            var targetId = targetObj.ToString()!;

            if (context.MutationGateway == null)
                return ToolExecutionResult.Error("No execution context available");

            var result = await context.MutationGateway.AttackAsync(targetId);
            if (!result.Success)
                return ToolExecutionResult.Error(result.Reason ?? "Attack failed");

            // The data payload lets GameHub emit the enemy_defeated narrative event that completes
            // kill objectives, and lets callers report the outcome without re-querying state.
            var message = result.TargetDefeated
                ? $"Defeated {targetId} ({result.TargetType})"
                : $"Hit {targetId} for {result.Damage} ({result.RemainingHealth} HP left)";

            return ToolExecutionResult.Ok(message, new Dictionary<string, object>
            {
                ["targetId"] = targetId,
                ["targetType"] = result.TargetType,
                ["damage"] = result.Damage,
                ["remainingHealth"] = result.RemainingHealth,
                ["defeated"] = result.TargetDefeated
            });
        }
    }
}
