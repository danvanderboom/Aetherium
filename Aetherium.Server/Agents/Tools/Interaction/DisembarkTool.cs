using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.Vehicles;
using Orleans;

namespace Aetherium.Server.Agents.Tools.Interaction
{
    /// <summary>
    /// Disembarks the caller from the vehicle interior they are currently aboard (add-boardable-vehicles
    /// Phase 2), re-pointing their session onto the vehicle's landed dock surface. The vehicle is
    /// resolved from the session's current world (a vehicle interior world encodes its vehicle id), so no
    /// target is needed. Auto-discovered; "interaction" category makes it available to the Player profile.
    /// </summary>
    [AgentTool("disembark", "Leave the vehicle you are aboard onto the surface",
        Categories = new[] { "interaction" }, RequiredCapabilities = new[] { "interaction" })]
    public class DisembarkTool : IAgentTool
    {
        public string ToolId => "disembark";
        public string Description => "Disembark from the vehicle you are currently inside onto the surface";
        public IEnumerable<string> Categories => new[] { "interaction" };
        public IEnumerable<string> RequiredCapabilities => new[] { "interaction" };

        public ToolParameterSchema GetParameterSchema() => new()
        {
            Properties = new Dictionary<string, ParameterDefinition>(),
            Required = new()
        };

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("interaction"))
                return ToolExecutionResult.Error("Missing capability: interaction");

            var vehicleId = VehicleGrain.VehicleIdFromWorldId(context.Session?.WorldId);
            if (string.IsNullOrEmpty(vehicleId))
                return ToolExecutionResult.Error("You are not aboard a vehicle");

            if (context.ServiceProvider.GetService(typeof(IGrainFactory)) is not IGrainFactory grainFactory)
                return ToolExecutionResult.Error("Unable to access grain factory");

            var vehicle = grainFactory.GetGrain<IVehicleGrain>(vehicleId);
            var result = await vehicle.DisembarkAsync(new List<string> { context.SessionId });
            if (!result.Success)
                return ToolExecutionResult.Error(result.Error ?? "Could not disembark");
            if (result.Moved == 0)
                return ToolExecutionResult.Error("Could not disembark (are you aboard, and is the vehicle landed?)");

            return ToolExecutionResult.Ok("Disembarked onto the surface.");
        }
    }
}
