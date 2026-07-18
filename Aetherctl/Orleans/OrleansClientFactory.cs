using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;
using Aetherium.Server.Agents;
using Aetherium.Server.Management;
using Aetherium.Server.Narrative;
using Aetherium.Model.Narrative;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Instances;
using Aetherium.Server.Groups;

namespace Aetherctl.Orleans
{
    /// <summary>
    /// Factory for creating and managing Orleans client connections.
    /// </summary>
    public class OrleansClientFactory : IAsyncDisposable
    {
        private IHost? _host;
        private IClusterClient? _client;

        public async Task<IClusterClient> ConnectAsync()
        {
            if (_client != null)
                return _client;

            _host = Host.CreateDefaultBuilder()
                .UseOrleansClient(clientBuilder =>
                {
                    clientBuilder.UseLocalhostClustering();
                })
                .Build();

            await _host.StartAsync();
            _client = _host.Services.GetRequiredService<IClusterClient>();

            return _client;
        }

        public IAgentGrain GetAgent(string agentId)
        {
            EnsureConnected();
            return _client!.GetGrain<IAgentGrain>(agentId);
        }

        public IPromptRegistryGrain GetPromptRegistry()
        {
            EnsureConnected();
            return _client!.GetGrain<IPromptRegistryGrain>("registry");
        }

        public IGameManagementGrain GetGameManagement()
        {
            EnsureConnected();
            return _client!.GetGrain<IGameManagementGrain>("GLOBAL");
        }

        public INarrativeGrain GetNarrative(string narrativeId)
        {
            EnsureConnected();
            return _client!.GetGrain<INarrativeGrain>(narrativeId);
        }

        public IWorldGrain GetWorld(string worldId)
        {
            EnsureConnected();
            return _client!.GetGrain<IWorldGrain>(worldId);
        }

        public IAgentRunnerGrain GetAgentRunner(string runnerId)
        {
            EnsureConnected();
            return _client!.GetGrain<IAgentRunnerGrain>(runnerId);
        }

        public IInstanceAllocatorGrain GetInstanceAllocator(string worldId)
        {
            EnsureConnected();
            return _client!.GetGrain<IInstanceAllocatorGrain>(worldId);
        }

        public IDungeonInstanceGrain GetDungeonInstance(string instanceId)
        {
            EnsureConnected();
            return _client!.GetGrain<IDungeonInstanceGrain>(instanceId);
        }

        public IPartyGrain GetParty(string partyId)
        {
            EnsureConnected();
            return _client!.GetGrain<IPartyGrain>(partyId);
        }

        public IGameMapGrain GetGameMap(string mapId)
        {
            EnsureConnected();
            return _client!.GetGrain<IGameMapGrain>(mapId);
        }

        public Aetherium.Model.Telemetry.IAgentTelemetryGrain GetAgentTelemetry(string agentId)
        {
            EnsureConnected();
            return _client!.GetGrain<Aetherium.Model.Telemetry.IAgentTelemetryGrain>(agentId);
        }

        private void EnsureConnected()
        {
            if (_client == null)
                throw new InvalidOperationException("Client not connected. Call ConnectAsync first.");
        }

        public async ValueTask DisposeAsync()
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
                _host = null;
                _client = null;
            }
        }
    }
}

