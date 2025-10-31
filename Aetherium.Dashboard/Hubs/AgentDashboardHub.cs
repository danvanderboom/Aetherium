using Microsoft.AspNetCore.SignalR.Client;

namespace Aetherium.Dashboard.Hubs
{
    /// <summary>
    /// Client-side SignalR connection helper for connecting to Server's AgentDashboardHub.
    /// </summary>
    public static class AgentDashboardHubClient
    {
        // Note: Dashboard connects to Server's AgentDashboardHub at http://localhost:5000/agentDashboardHub
        // This is handled by the Blazor pages that use HubConnectionBuilder
    }
}

