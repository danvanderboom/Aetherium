using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.Narrative;
using Orleans;

namespace Aetherium.Server.Agents.Tools.Narrative
{
    /// <summary>
    /// Accepts (activates) a quest by ID in the character's current world's narrative.
    /// </summary>
    [AgentTool("accept_quest", "Accept (start) a quest by ID in this world",
        Categories = new[] { "quest" })]
    public class AcceptQuestTool : IAgentTool
    {
        public string ToolId => "accept_quest";
        public string Description => "Accept (start) a quest by its ID; fails if unknown, already active/completed, or prerequisites are unmet";
        public IEnumerable<string> Categories => new[] { "quest" };
        public IEnumerable<string> RequiredCapabilities => Array.Empty<string>();

        public ToolParameterSchema GetParameterSchema() => new()
        {
            Properties = new Dictionary<string, ParameterDefinition>
            {
                ["questId"] = new() { Type = "string", Description = "ID of the quest to accept" }
            },
            Required = new() { "questId" }
        };

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!args.TryGetValue("questId", out var questIdObj) || string.IsNullOrWhiteSpace(questIdObj?.ToString()))
                return ToolExecutionResult.Error("Missing required parameter: questId");

            var questId = questIdObj.ToString()!;

            if (context.ServiceProvider.GetService(typeof(IGrainFactory)) is not IGrainFactory grainFactory)
                return ToolExecutionResult.Error("Unable to access grain factory");

            var grain = await NarrativeStateResolver.ResolveForWorldAsync(grainFactory, context.Session?.WorldId);
            if (grain == null)
                return ToolExecutionResult.Error("No narrative context for the current world");

            var started = await grain.StartQuestAsync(questId);
            return started
                ? ToolExecutionResult.Ok($"Accepted quest '{questId}'",
                    new Dictionary<string, object> { ["questId"] = questId })
                : ToolExecutionResult.Error($"Could not accept quest '{questId}' (unknown, already active/completed, or prerequisites unmet)");
        }
    }
}
