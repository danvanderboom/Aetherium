using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;
using ConsoleGameServer.Agents;
using ConsoleGameServer.Management;
using ConsoleGameServer.Narrative;
using ConsoleGameServer.MultiWorld;

namespace AgentCLI
{
    /// <summary>
    /// Orleans client for connecting to the agent service from the CLI.
    /// </summary>
    public class AgentClient : IAsyncDisposable
    {
        private IHost? _host;
        private IClusterClient? _client;

        public async Task ConnectAsync()
        {
            _host = Host.CreateDefaultBuilder()
                .UseOrleansClient(clientBuilder =>
                {
                    clientBuilder.UseLocalhostClustering();
                })
                .Build();

            await _host.StartAsync();
            _client = _host.Services.GetRequiredService<IClusterClient>();
            
            Console.WriteLine("Connected to Orleans cluster");
        }

        public IAgentGrain GetAgent(string agentId)
        {
            if (_client == null)
                throw new InvalidOperationException("Client not connected. Call ConnectAsync first.");

            return _client.GetGrain<IAgentGrain>(agentId);
        }

        public ConsoleGameServer.Agents.IPromptRegistryGrain GetPromptRegistry()
        {
            if (_client == null)
                throw new InvalidOperationException("Client not connected. Call ConnectAsync first.");

            // Use a singleton key for the registry
            return _client.GetGrain<ConsoleGameServer.Agents.IPromptRegistryGrain>("registry");
        }

        public IGameManagementGrain GetGameManagement()
        {
            if (_client == null)
                throw new InvalidOperationException("Client not connected. Call ConnectAsync first.");

            // Use singleton key "GLOBAL" for the management grain
            return _client.GetGrain<IGameManagementGrain>("GLOBAL");
        }

        public INarrativeGrain GetNarrative(string narrativeId)
        {
            if (_client == null)
                throw new InvalidOperationException("Client not connected. Call ConnectAsync first.");

            return _client.GetGrain<INarrativeGrain>(narrativeId);
        }

        public IWorldGrain GetWorld(string worldId)
        {
            if (_client == null)
                throw new InvalidOperationException("Client not connected. Call ConnectAsync first.");

            return _client.GetGrain<IWorldGrain>(worldId);
        }

        public IAgentRunnerGrain GetAgentRunner(string runnerId)
        {
            if (_client == null)
                throw new InvalidOperationException("Client not connected. Call ConnectAsync first.");

            return _client.GetGrain<IAgentRunnerGrain>(runnerId);
        }

        public async ValueTask DisposeAsync()
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }
    }
}
