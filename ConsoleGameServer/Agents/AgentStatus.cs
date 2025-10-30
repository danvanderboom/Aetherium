namespace ConsoleGameServer.Agents
{
    /// <summary>
    /// Status information for an agent grain.
    /// </summary>
    public sealed class AgentStatus
    {
        /// <summary>
        /// Unique identifier for this agent instance.
        /// </summary>
        public string AgentId { get; init; } = string.Empty;

        /// <summary>
        /// Type/name of the agent (e.g., "Explorer", "CombatAgent").
        /// </summary>
        public string AgentType { get; init; } = string.Empty;

        /// <summary>
        /// Current game session ID, or null if not in a game.
        /// </summary>
        public string? GameSessionId { get; init; }

        /// <summary>
        /// Whether the agent is currently active and connected.
        /// </summary>
        public bool IsActive { get; init; }

        /// <summary>
        /// Name of the prompt template currently being used.
        /// </summary>
        public string? PromptTemplateName { get; init; }
    }
}

