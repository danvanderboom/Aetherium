using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Movement
{
    /// <summary>
    /// Tool for taking off from a landed cell back up to the flyer's cruise band through clear air.
    /// </summary>
    [AgentTool("takeoff", "Take off from a landed cell to the cruise band",
        Categories = new[] { "movement", "navigation" },
        RequiredCapabilities = new[] { "basic_movement" })]
    public class TakeoffTool : IAgentTool
    {
        public string ToolId => "takeoff";
        public string Description => "Take off from a landed cell to the flyer's cruise band";
        public IEnumerable<string> Categories => new[] { "movement", "navigation" };
        public IEnumerable<string> RequiredCapabilities => new[] { "basic_movement" };

        public ToolParameterSchema GetParameterSchema() => new ToolParameterSchema
        {
            Properties = new Dictionary<string, ParameterDefinition>(),
            Required = new List<string>()
        };

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("basic_movement"))
                return ToolExecutionResult.Error("Missing required capability: basic_movement");

            if (context.Session == null)
                return ToolExecutionResult.Error("No execution context available");

            return context.Session.Takeoff()
                ? ToolExecutionResult.Ok("Airborne")
                : ToolExecutionResult.Error("Cannot take off here (not landed, blocked air, or invalid terrain)");
        }
    }
}
