using System;
using System.Collections.Generic;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Server.Agents.Tools
{
    /// <summary>
    /// Provides context and resources for tool execution.
    /// </summary>
    public class ToolExecutionContext
    {
        /// <summary>
        /// The game session ID for this execution.
        /// </summary>
        public string SessionId { get; init; } = string.Empty;
        
        /// <summary>
        /// The agent ID executing this tool (if applicable).
        /// </summary>
        public string? AgentId { get; init; }
        
        /// <summary>
        /// SignalR connection ID (for human players).
        /// </summary>
        public string? ConnectionId { get; init; }
        
        /// <summary>
        /// The game management grain for executing actions.
        /// </summary>
        public IGameManagementGrain? ManagementGrain { get; init; }
        
        /// <summary>
        /// Direct reference to the game session (optional, for efficiency).
        /// </summary>
        public GameSession? Session { get; init; }
        
        /// <summary>
        /// The gateway that tools invoke to apply gameplay mutations. Tools SHALL
        /// route mutation calls (move, rotate, pickup, drop, use, open, close,
        /// change-level) through this gateway rather than reaching into
        /// <see cref="Session"/> directly.
        ///
        /// <para>
        /// When not explicitly set, this auto-falls-back to a
        /// <see cref="LocalMutationGateway"/> bound to <see cref="Session"/>.
        /// Phase 2c overrides the fallback for sessions joined to an Orleans
        /// world (<c>GameHub.JoinWorld</c> sets <c>session.Gateway</c> to a
        /// <c>GrainMutationGateway</c>). Phase 2d removed the field for an
        /// explicitly-passed <c>InteractionSystem</c> — the gateway is the
        /// only mutation entry point now.
        /// </para>
        /// </summary>
        public IMapMutationGateway? MutationGateway
        {
            get
            {
                if (_mutationGateway is not null)
                    return _mutationGateway;
                if (Session is not null)
                {
                    // Cache the auto-fallback so successive reads return the same instance.
                    _mutationGateway = new LocalMutationGateway(Session);
                    return _mutationGateway;
                }
                return null;
            }
            init => _mutationGateway = value;
        }
        private IMapMutationGateway? _mutationGateway;
        
        /// <summary>
        /// Capabilities granted to this execution context.
        /// Tools can check this to enforce authorization.
        /// </summary>
        public HashSet<string> GrantedCapabilities { get; init; } = new();
        
        /// <summary>
        /// Service provider for dependency injection.
        /// </summary>
        public IServiceProvider ServiceProvider { get; init; } = null!;
        
        /// <summary>
        /// Optional parent agent ID for hierarchical delegation.
        /// </summary>
        public string? ParentAgentId { get; init; }
        
        /// <summary>
        /// Current delegation depth (0 for root agents).
        /// </summary>
        public int DelegationDepth { get; init; }
        
        /// <summary>
        /// Maximum allowed delegation depth.
        /// </summary>
        public int MaxDelegationDepth { get; init; } = 2;
        
        /// <summary>
        /// Checks if a specific capability is granted.
        /// </summary>
        public bool HasCapability(string capability)
        {
            return GrantedCapabilities.Contains(capability);
        }
        
        /// <summary>
        /// Checks if all required capabilities are granted.
        /// </summary>
        public bool HasAllCapabilities(IEnumerable<string> requiredCapabilities)
        {
            foreach (var capability in requiredCapabilities)
            {
                if (!GrantedCapabilities.Contains(capability))
                    return false;
            }
            return true;
        }
    }
}

