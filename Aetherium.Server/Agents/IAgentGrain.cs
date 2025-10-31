using System.Threading.Tasks;

namespace Aetherium.Server.Agents
{
    /// <summary>
    /// Orleans grain interface for AI agents that can join games and make decisions.
    /// </summary>
    public interface IAgentGrain : Orleans.IGrainWithStringKey
    {
        /// <summary>
        /// Instructs the agent to join a game session.
        /// </summary>
        Task JoinGameAsync(string sessionId);

        /// <summary>
        /// Instructs the agent to leave the current game session.
        /// </summary>
        Task LeaveGameAsync();

        /// <summary>
        /// Gets the current status of the agent.
        /// </summary>
        Task<AgentStatus> GetStatusAsync();

        /// <summary>
        /// Updates the agent's prompt template.
        /// </summary>
        Task UpdatePromptAsync(string promptTemplate);
    }
}


