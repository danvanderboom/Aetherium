using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Movement
{
    /// <summary>
    /// Tool for landing an airborne flyer onto valid, unoccupied terrain directly below it.
    /// </summary>
    [AgentTool("land", "Land an airborne flyer on valid terrain below",
        Categories = new[] { "movement", "navigation" },
        RequiredCapabilities = new[] { "basic_movement" })]
    public class LandTool : IAgentTool
    {
        public string ToolId => "land";
        public string Description => "Land an airborne flyer onto valid, unoccupied terrain directly below";
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

            return context.Session.Land()
                ? ToolExecutionResult.Ok("Landed")
                : ToolExecutionResult.Error("Cannot land here (not an airborne lander, or no valid landing surface)");
        }
    }
}
