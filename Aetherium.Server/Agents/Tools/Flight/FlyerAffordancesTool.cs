using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Flight
{
    /// <summary>
    /// List the altitude-aware affordances a flyer offers the player (hack/summon/attack/inspect), each with
    /// whether it is currently reachable and, if not, why — so callers see that a satellite in orbit can be
    /// hacked but not attacked.
    /// </summary>
    [AgentTool("flyer-affordances", "List the altitude-aware interactions available for a flyer",
        Categories = new[] { "interaction", "flight", "perception" },
        RequiredCapabilities = new[] { "interaction" })]
    public class FlyerAffordancesTool : IAgentTool
    {
        public string ToolId => "flyer-affordances";
        public string Description => "List the interactions available for a flyer (hack/summon/attack/inspect) with reachability and reasons";
        public IEnumerable<string> Categories => new[] { "interaction", "flight", "perception" };
        public IEnumerable<string> RequiredCapabilities => new[] { "interaction" };

        public ToolParameterSchema GetParameterSchema() => new ToolParameterSchema
        {
            Properties = new Dictionary<string, ParameterDefinition>
            {
                ["targetEntityId"] = new() { Type = "string", Description = "Entity ID of the flyer to inspect" }
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

            var affordances = context.Session.FlyerAffordances(idObj.ToString()!);
            var data = new Dictionary<string, object>
            {
                ["affordances"] = affordances.Select(a => new Dictionary<string, object>
                {
                    ["id"] = a.Id,
                    ["reach"] = a.Reach.ToString(),
                    ["available"] = a.Available,
                    ["reason"] = a.Unavailable ?? string.Empty
                }).ToList()
            };
            return ToolExecutionResult.Ok($"{affordances.Count(a => a.Available)} affordance(s) available", data);
        }
    }
}
