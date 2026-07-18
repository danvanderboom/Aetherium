using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Flight
{
    /// <summary>
    /// Summon/hail an air taxi: it generates an AdHoc flight plan to the caller and lands to offer boarding.
    /// Band-agnostic; gated by planar signal range.
    /// </summary>
    [AgentTool("summon", "Summon/hail an air taxi to your location for boarding",
        Categories = new[] { "interaction", "flight" },
        RequiredCapabilities = new[] { "interaction" })]
    public class SummonTool : IAgentTool
    {
        public string ToolId => "summon";
        public string Description => "Summon/hail an air taxi; it plans an AdHoc route to you and lands to board";
        public IEnumerable<string> Categories => new[] { "interaction", "flight" };
        public IEnumerable<string> RequiredCapabilities => new[] { "interaction" };

        public ToolParameterSchema GetParameterSchema() => new ToolParameterSchema
        {
            Properties = new Dictionary<string, ParameterDefinition>
            {
                ["targetEntityId"] = new() { Type = "string", Description = "Entity ID of the air taxi to summon" }
            },
            Required = new List<string> { "targetEntityId" }
        };

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("interaction"))
                return ToolExecutionResult.Error("Missing required capability: interaction");

            if (context.Session == null)
                return ToolExecutionResult.Error("No execution context available");

            if (!args.TryGetValue("targetEntityId", out var idObj) || string.IsNullOrWhiteSpace(idObj?.ToString()))
                return ToolExecutionResult.Error("Missing required parameter: targetEntityId");

            var outcome = context.Session.Summon(idObj.ToString()!);
            return outcome.Success ? ToolExecutionResult.Ok(outcome.Reason) : ToolExecutionResult.Error(outcome.Reason);
        }
    }
}
