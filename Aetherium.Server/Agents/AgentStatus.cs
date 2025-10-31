using Orleans;

namespace Aetherium.Server.Agents
{
    /// <summary>
    /// Status information for an agent grain.
    /// </summary>
    [GenerateSerializer]
    public sealed class AgentStatus
    {
        /// <summary>
        /// Unique identifier for this agent instance.
        /// </summary>
        [Id(0)] public string AgentId { get; init; } = string.Empty;

        /// <summary>
        /// Type/name of the agent (e.g., "Explorer", "CombatAgent").
        /// </summary>
        [Id(1)] public string AgentType { get; init; } = string.Empty;

        /// <summary>
        /// Current game session ID, or null if not in a game.
        /// </summary>
        [Id(2)] public string? GameSessionId { get; init; }

        /// <summary>
        /// Whether the agent is currently active and connected.
        /// </summary>
        [Id(3)] public bool IsActive { get; init; }

        /// <summary>
        /// Name of the prompt template currently being used.
        /// </summary>
        [Id(4)] public string? PromptTemplateName { get; init; }
    }
}


