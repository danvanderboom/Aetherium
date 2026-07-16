using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Client.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace Aetherium.Client
{
    public enum AetheriumConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
    }

    /// <summary>
    /// Owns the SignalR <see cref="HubConnection"/> to an Aetherium server's /gamehub:
    /// lifecycle, automatic reconnect, the inbound event handlers (ReceivePerceptionUpdate,
    /// ReceiveGameState, ReceiveDowned/Respawn/Died), connection-state events, and an
    /// optional access-token provider for deployed (JWT-gated) servers. Extracted from the
    /// console client's proven GameClient seed (docs/design/unity-sample/unity-client-library.md).
    ///
    /// Threading contract: all events may fire on SignalR worker threads. The core never
    /// touches a synchronization context — marshalling to a main thread is the adapter's job.
    /// </summary>
    public sealed class AetheriumConnection : IAsyncDisposable
    {
        private readonly HubConnection _connection;

        /// <summary>The perception store this connection feeds. One store per connection.</summary>
        public PerceptionStore Store { get; } = new PerceptionStore();

        public AetheriumConnectionState State { get; private set; } = AetheriumConnectionState.Disconnected;

        public event Action<AetheriumConnectionState>? StateChanged;
        public event Action<PerceptionDto>? PerceptionReceived;
        public event Action<GameStateDto>? GameStateReceived;
        public event Action<PlayerVitalsDto>? Downed;
        public event Action<PlayerVitalsDto>? Respawned;
        public event Action<PlayerVitalsDto>? Died;

        /// <summary>The session/player id the server assigned (from ReceiveGameState).</summary>
        public string? PlayerId { get; private set; }

        /// <param name="baseUrl">Server base URL, e.g. "http://localhost:5000". "/gamehub" is appended.</param>
        /// <param name="worldId">Optional world to auto-join on connect (rides the query string).</param>
        /// <param name="mapId">Optional map within the world.</param>
        /// <param name="accessTokenProvider">Optional JWT provider for deployed servers; absent in dev.</param>
        /// <param name="configureHttpConnection">Optional transport hook — used by in-proc
        /// integration tests to route through a TestServer handler; games never need it.</param>
        public AetheriumConnection(
            string baseUrl,
            string? worldId = null,
            string? mapId = null,
            Func<Task<string?>>? accessTokenProvider = null,
            Action<Microsoft.AspNetCore.Http.Connections.Client.HttpConnectionOptions>? configureHttpConnection = null)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("A server base URL is required.", nameof(baseUrl));

            var url = baseUrl.TrimEnd('/') + "/gamehub";
            var query = new List<string>();
            if (!string.IsNullOrEmpty(worldId)) query.Add("worldId=" + Uri.EscapeDataString(worldId));
            if (!string.IsNullOrEmpty(mapId)) query.Add("mapId=" + Uri.EscapeDataString(mapId));
            if (query.Count > 0)
                url += "?" + string.Join("&", query);

            _connection = new HubConnectionBuilder()
                .WithUrl(url, options =>
                {
                    if (accessTokenProvider != null)
                        options.AccessTokenProvider = () => accessTokenProvider()!;
                    configureHttpConnection?.Invoke(options);
                })
                .WithAutomaticReconnect()
                .Build();

            _connection.On<PerceptionDto>("ReceivePerceptionUpdate", frame =>
            {
                Store.ApplyFrame(frame);
                PerceptionReceived?.Invoke(frame);
            });

            _connection.On<GameStateDto>("ReceiveGameState", state =>
            {
                PlayerId = state.PlayerId;
                GameStateReceived?.Invoke(state);
            });

            _connection.On<PlayerVitalsDto>("ReceiveDowned", vitals => Downed?.Invoke(vitals));
            _connection.On<PlayerVitalsDto>("ReceiveRespawn", vitals =>
            {
                // A respawn is a positional discontinuity: the body is back at the dock.
                Store.NoteDiscontinuity(ReanchorReason.Respawned);
                Respawned?.Invoke(vitals);
            });
            _connection.On<PlayerVitalsDto>("ReceiveDied", vitals => Died?.Invoke(vitals));

            _connection.Reconnecting += _ =>
            {
                SetState(AetheriumConnectionState.Reconnecting);
                return Task.CompletedTask;
            };
            _connection.Reconnected += _ =>
            {
                SetState(AetheriumConnectionState.Connected);
                return Task.CompletedTask;
            };
            _connection.Closed += _ =>
            {
                SetState(AetheriumConnectionState.Disconnected);
                return Task.CompletedTask;
            };
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            SetState(AetheriumConnectionState.Connecting);
            try
            {
                await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                SetState(AetheriumConnectionState.Disconnected);
                throw;
            }
            Store.NoteDiscontinuity(ReanchorReason.Joined);
            SetState(AetheriumConnectionState.Connected);
        }

        public async Task DisconnectAsync()
        {
            await _connection.StopAsync().ConfigureAwait(false);
            SetState(AetheriumConnectionState.Disconnected);
        }

        /// <summary>Typed hub invocation — the single funnel ToolClient/LobbyClient go through.</summary>
        public Task<T> InvokeAsync<T>(string method, object?[] args, CancellationToken cancellationToken = default)
            => _connection.InvokeCoreAsync<T>(method, args, cancellationToken);

        private void SetState(AetheriumConnectionState state)
        {
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke(state);
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}
