using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Flight
{
    /// <summary>
    /// Hack a flyer over an uplink at range, without physical adjacency — the showcase interaction for reaching an
    /// out-of-touch flyer such as an orbital satellite. Band-agnostic; gated by planar uplink range only.
    /// </summary>
    [AgentTool("hack", "Hack a flyer (e.g. an orbital satellite) over an uplink at range, without adjacency",
        Categories = new[] { "interaction", "flight" },
        RequiredCapabilities = new[] { "interaction" })]
    public class HackTool : IAgentTool
    {
        public string ToolId => "hack";
        public string Description => "Hack a flyer over an uplink at range (any altitude), e.g. to retask or read an orbital satellite";
        public IEnumerable<string> Categories => new[] { "interaction", "flight" };
        public IEnumerable<string> RequiredCapabilities => new[] { "interaction" };

        public ToolParameterSchema GetParameterSchema() => new ToolParameterSchema
        {
            Properties = new Dictionary<string, ParameterDefinition>
            {
                ["targetEntityId"] = new() { Type = "string", Description = "Entity ID of the flyer to hack" }
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

            var outcome = context.Session.Hack(idObj.ToString()!);
            return outcome.Success ? ToolExecutionResult.Ok(outcome.Reason) : ToolExecutionResult.Error(outcome.Reason);
        }
    }
}
