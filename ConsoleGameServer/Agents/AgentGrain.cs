using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace ConsoleGameServer.Agents
{
    /// <summary>
    /// Orleans grain implementation for AI agents that can join games and make decisions.
    /// </summary>
    public class AgentGrain : Grain, IAgentGrain
    {
        private string? _gameSessionId;
        private string? _agentType;
        private string? _promptTemplate;
        private bool _isActive;

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            _isActive = true;
            var agentId = this.GetPrimaryKeyString();
            Console.WriteLine($"[AgentGrain] Activated: {agentId}");
            return base.OnActivateAsync(cancellationToken);
        }

        public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _isActive = false;
            var agentId = this.GetPrimaryKeyString();
            Console.WriteLine($"[AgentGrain] Deactivated: {agentId}");
            return base.OnDeactivateAsync(reason, cancellationToken);
        }

        public Task JoinGameAsync(string sessionId)
        {
            _gameSessionId = sessionId;
            var agentId = this.GetPrimaryKeyString();
            Console.WriteLine($"[AgentGrain] {agentId} joining game session: {sessionId}");
            
            // TODO: Connect to GameHub via SignalR client
            // TODO: Use Microsoft Agent Framework to process perceptions and make decisions
            
            return Task.CompletedTask;
        }

        public Task LeaveGameAsync()
        {
            var agentId = this.GetPrimaryKeyString();
            var previousSession = _gameSessionId;
            _gameSessionId = null;
            
            Console.WriteLine($"[AgentGrain] {agentId} leaving game session: {previousSession}");
            
            // TODO: Disconnect from GameHub
            
            return Task.CompletedTask;
        }

        public Task<AgentStatus> GetStatusAsync()
        {
            var agentId = this.GetPrimaryKeyString();
            return Task.FromResult(new AgentStatus
            {
                AgentId = agentId,
                AgentType = _agentType ?? "Unknown",
                GameSessionId = _gameSessionId,
                IsActive = _isActive,
                PromptTemplateName = _promptTemplate
            });
        }

        public Task SetAgentTypeAsync(string agentType)
        {
            _agentType = agentType;
            return Task.CompletedTask;
        }

        public Task UpdatePromptAsync(string promptTemplate)
        {
            _promptTemplate = promptTemplate;
            var agentId = this.GetPrimaryKeyString();
            Console.WriteLine($"[AgentGrain] {agentId} prompt template updated");
            
            // TODO: Update agent behavior based on new prompt
            
            return Task.CompletedTask;
        }
    }
}

