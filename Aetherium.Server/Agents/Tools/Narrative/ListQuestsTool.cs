using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Server.Narrative;
using Aetherium.Model.Narrative;
using Orleans;

namespace Aetherium.Server.Agents.Tools.Narrative
{
    /// <summary>
    /// Lists the quests the character can currently start in their world's narrative.
    /// </summary>
    [AgentTool("list_quests", "List quests you can currently start in this world",
        Categories = new[] { "quest" })]
    public class ListQuestsTool : IAgentTool
    {
        public string ToolId => "list_quests";
        public string Description => "List quests you can currently start (prerequisites met) in your current world";
        public IEnumerable<string> Categories => new[] { "quest" };
        public IEnumerable<string> RequiredCapabilities => Array.Empty<string>();

        public ToolParameterSchema GetParameterSchema() => new();

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (context.ServiceProvider.GetService(typeof(IGrainFactory)) is not IGrainFactory grainFactory)
                return ToolExecutionResult.Error("Unable to access grain factory");

            var grain = await NarrativeStateResolver.ResolveForWorldAsync(grainFactory, context.Session?.WorldId);
            if (grain == null)
                return ToolExecutionResult.Error("No narrative context for the current world");

            var available = await grain.GetAvailableQuestsAsync();
            var quests = available.Select(q => (object)new Dictionary<string, object>
            {
                ["questId"] = q.QuestId,
                ["title"] = q.Title,
                ["description"] = q.Description,
                ["objectiveCount"] = q.Objectives?.Count ?? 0
            }).ToList();

            return ToolExecutionResult.Ok(
                $"{quests.Count} quest(s) available",
                new Dictionary<string, object> { ["quests"] = quests });
        }
    }
}
