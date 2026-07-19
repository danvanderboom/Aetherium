using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Flight
{
    /// <summary>
    /// Attack/shoot a flyer such as a low-air drone. Gated by planar weapon range and a small band delta, so a
    /// grounded attacker can reach a low drone but not a flyer in a high band.
    /// </summary>
    [AgentTool("attack-flyer", "Attack/shoot a low-air flyer within range",
        Categories = new[] { "interaction", "flight", "combat" },
        RequiredCapabilities = new[] { "interaction" })]
    public class AttackFlyerTool : IAgentTool
    {
        public string ToolId => "attack-flyer";
        public string Description => "Attack/shoot a flyer (e.g. a low-air drone) within weapon range and band reach";
        public IEnumerable<string> Categories => new[] { "interaction", "flight", "combat" };
        public IEnumerable<string> RequiredCapabilities => new[] { "interaction" };

        public ToolParameterSchema GetParameterSchema() => new ToolParameterSchema
        {
            Properties = new Dictionary<string, ParameterDefinition>
            {
                ["targetEntityId"] = new() { Type = "string", Description = "Entity ID of the flyer to attack" }
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

            var outcome = context.Session.Attack(idObj.ToString()!);
            return outcome.Success ? ToolExecutionResult.Ok(outcome.Reason) : ToolExecutionResult.Error(outcome.Reason);
        }
    }
}
