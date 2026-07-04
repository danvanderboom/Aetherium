using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Aetherium.Server.Agents.Telemetry;
using Aetherium.Model.Telemetry;

namespace Aetherium.Server.Hubs
{
    /// <summary>
    /// SignalR hub for broadcasting agent telemetry updates in real-time.
    /// Dashboard connects to this hub to receive telemetry updates.
    /// </summary>
    public class AgentDashboardHub : Hub
    {
        public async Task JoinAgentGroup(string agentId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{agentId}");
        }

        public async Task LeaveAgentGroup(string agentId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"agent:{agentId}");
        }

        public async Task SubscribeToAgent(string agentId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{agentId}");
            // Note: Current telemetry would be sent by server-side code after subscription
            // For now, clients will receive updates as they happen
        }
    }
}

