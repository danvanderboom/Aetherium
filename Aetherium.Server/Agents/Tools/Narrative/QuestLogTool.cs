using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Server.Narrative;
using Orleans;

namespace Aetherium.Server.Agents.Tools.Narrative
{
    /// <summary>
    /// Reports the character's active quests (with objective progress) and completed quest IDs.
    /// </summary>
    [AgentTool("quest_log", "Show your active quests and completed quests in this world",
        Categories = new[] { "quest" })]
    public class QuestLogTool : IAgentTool
    {
        public string ToolId => "quest_log";
        public string Description => "Show your active quests (with objective progress) and completed quests";
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

            var state = await grain.GetStateAsync();
            var active = await grain.GetActiveQuestsAsync();

            var activeList = active.Select(q =>
            {
                var completed = (state != null && state.CompletedObjectives.TryGetValue(q.QuestId, out var cs)) ? cs : null;
                var objectives = (q.Objectives ?? new List<QuestObjective>()).Select(o => (object)new Dictionary<string, object>
                {
                    ["objectiveId"] = o.ObjectiveId,
                    ["type"] = o.Type,
                    ["completed"] = completed != null && completed.Contains(o.ObjectiveId)
                }).ToList();

                return (object)new Dictionary<string, object>
                {
                    ["questId"] = q.QuestId,
                    ["title"] = q.Title,
                    ["objectives"] = objectives
                };
            }).ToList();

            var completedQuests = state?.CompletedQuestIds.ToList() ?? new List<string>();

            return ToolExecutionResult.Ok(
                $"{activeList.Count} active, {completedQuests.Count} completed",
                new Dictionary<string, object>
                {
                    ["active"] = activeList,
                    ["completed"] = completedQuests.Cast<object>().ToList()
                });
        }
    }
}
