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
    /// Session resume: the server issues a resume token with the initial game state. On any
    /// reconnect — SignalR's automatic reconnect or a manual ConnectAsync after a full drop —
    /// the connection presents (PlayerId, ResumeToken) to GameHub.ResumeSession. Success
    /// rebinds to the prior server session (position, inventory, world mirror intact) and is
    /// NOT a discontinuity: the store keeps its anchor and memory, and the fresh-spawn
    /// session the server created during the handshake is discarded on both ends. Failure
    /// (grace window lapsed, unknown session) falls back to the fresh join: the buffered
    /// fresh-session state is adopted and the store re-anchors via
    /// <see cref="ReanchorReason.Joined"/>.
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

        /// <summary>The resume secret issued with the game state; presented back to the
        /// server after a reconnect to rebind to the same session.</summary>
        public string? ResumeToken { get; private set; }

        // Resume gating. While a reconnect's resume attempt is unresolved, the handshake's
        // fresh-session pushes (ReceiveGameState + ReceivePerceptionUpdate for a brand-new
        // spawn) must not reach the store or the app — applying a fresh-spawn frame against
        // the surviving anchor would smear alien geometry into remembered cells. They are
        // buffered here: discarded when the resume succeeds, adopted when it fails.
        private readonly object _resumeGate = new object();
        private bool _resuming;
        private GameStateDto? _pendingGameState;
        private PerceptionDto? _pendingPerception;

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
                lock (_resumeGate)
                {
                    if (_resuming)
                    {
                        _pendingPerception = frame; // latest wins; adopted only on failed resume
                        return;
                    }
                }
                Store.ApplyFrame(frame);
                PerceptionReceived?.Invoke(frame);
            });

            _connection.On<GameStateDto>("ReceiveGameState", state =>
            {
                lock (_resumeGate)
                {
                    if (_resuming)
                    {
                        _pendingGameState = state;
                        return;
                    }
                }
                PlayerId = state.PlayerId;
                ResumeToken = string.IsNullOrEmpty(state.ResumeToken) ? null : state.ResumeToken;
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
                BeginResumeWindow();
                SetState(AetheriumConnectionState.Reconnecting);
                return Task.CompletedTask;
            };
            _connection.Reconnected += async _ =>
            {
                await ResolveResumeAsync().ConfigureAwait(false);
                SetState(AetheriumConnectionState.Connected);
            };
            _connection.Closed += _ =>
            {
                // The connection is gone for good (auto-reconnect gave up or Stop was
                // called); nothing to gate anymore. Credentials survive so a later
                // ConnectAsync can still try to resume within the server's grace window.
                lock (_resumeGate)
                {
                    _resuming = false;
                    _pendingGameState = null;
                    _pendingPerception = null;
                }
                SetState(AetheriumConnectionState.Disconnected);
                return Task.CompletedTask;
            };
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            SetState(AetheriumConnectionState.Connecting);
            var attemptResume = BeginResumeWindow();
            try
            {
                await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                lock (_resumeGate)
                {
                    _resuming = false;
                    _pendingGameState = null;
                    _pendingPerception = null;
                }
                SetState(AetheriumConnectionState.Disconnected);
                throw;
            }

            if (attemptResume)
                await ResolveResumeAsync(cancellationToken).ConfigureAwait(false);
            else
                Store.NoteDiscontinuity(ReanchorReason.Joined);
            SetState(AetheriumConnectionState.Connected);
        }

        public async Task DisconnectAsync()
        {
            await _connection.StopAsync().ConfigureAwait(false);
            SetState(AetheriumConnectionState.Disconnected);
        }

        /// <summary>
        /// Opens the resume gate when we hold credentials from a prior session. Returns
        /// whether a resume will be attempted; when false (first-ever connect) inbound
        /// pushes flow straight through as before.
        /// </summary>
        private bool BeginResumeWindow()
        {
            lock (_resumeGate)
            {
                _resuming = PlayerId != null && ResumeToken != null;
                _pendingGameState = null;
                _pendingPerception = null;
                return _resuming;
            }
        }

        /// <summary>
        /// Resolves an open resume window: asks the server to rebind to the prior session.
        /// Success — no discontinuity; the buffered fresh-spawn pushes are dropped and the
        /// resumed session's frame (returned by the hub call) is applied against the
        /// surviving anchor. Failure — fresh-join fallback: adopt the buffered fresh game
        /// state, re-anchor (wiping memory), then apply the fresh frame.
        /// </summary>
        private async Task ResolveResumeAsync(CancellationToken cancellationToken = default)
        {
            bool resuming;
            lock (_resumeGate)
                resuming = _resuming;
            if (!resuming)
                return;

            ResumeSessionResultDto? result = null;
            try
            {
                result = await _connection.InvokeCoreAsync<ResumeSessionResultDto>(
                    "ResumeSession", new object?[] { PlayerId, ResumeToken }, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Older server without ResumeSession, or transient invoke failure —
                // treat as a failed resume and fall back to the fresh join.
            }

            if (result?.Success == true)
            {
                lock (_resumeGate)
                {
                    _resuming = false;
                    _pendingGameState = null;
                    _pendingPerception = null;
                }
                if (result.Perception != null)
                {
                    Store.ApplyFrame(result.Perception);
                    PerceptionReceived?.Invoke(result.Perception);
                }
                return;
            }

            GameStateDto? freshState;
            PerceptionDto? freshFrame;
            lock (_resumeGate)
            {
                _resuming = false;
                freshState = _pendingGameState;
                freshFrame = _pendingPerception;
                _pendingGameState = null;
                _pendingPerception = null;
            }

            Store.NoteDiscontinuity(ReanchorReason.Joined);
            if (freshState != null)
            {
                PlayerId = freshState.PlayerId;
                ResumeToken = string.IsNullOrEmpty(freshState.ResumeToken) ? null : freshState.ResumeToken;
                GameStateReceived?.Invoke(freshState);
            }
            if (freshFrame != null)
            {
                Store.ApplyFrame(freshFrame);
                PerceptionReceived?.Invoke(freshFrame);
            }
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
