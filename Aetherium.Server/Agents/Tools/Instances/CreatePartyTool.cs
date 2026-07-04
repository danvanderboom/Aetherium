using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.Groups;
using Aetherium.Model.Groups;
using Aetherium.Model.Worlds;
using Orleans;

namespace Aetherium.Server.Agents.Tools.Instances
{
    /// <summary>
    /// Creates a new party led by the calling character and returns its id.
    /// </summary>
    [AgentTool("create_party", "Create a new party that you lead",
        Categories = new[] { "instance" })]
    public class CreatePartyTool : IAgentTool
    {
        public string ToolId => "create_party";
        public string Description => "Create a new party led by you; returns the party id others can join and that enter_dungeon accepts";
        public IEnumerable<string> Categories => new[] { "instance" };
        public IEnumerable<string> RequiredCapabilities => Array.Empty<string>();

        public ToolParameterSchema GetParameterSchema() => new();

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (context.ServiceProvider.GetService(typeof(IGrainFactory)) is not IGrainFactory grainFactory)
                return ToolExecutionResult.Error("Unable to access grain factory");

            // A GUID-suffixed key keeps party ids unique without varying by wall-clock (which would
            // be non-deterministic); the calling character becomes the leader.
            var partyId = $"party-{Guid.NewGuid():N}";
            var party = grainFactory.GetGrain<IPartyGrain>(partyId);
            await party.CreateAsync(new PlayerId(context.SessionId), context.SessionId);

            return ToolExecutionResult.Ok(
                $"Created party '{partyId}'",
                new Dictionary<string, object> { ["partyId"] = partyId });
        }
    }
}
