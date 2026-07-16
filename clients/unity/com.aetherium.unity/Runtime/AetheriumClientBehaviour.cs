using System;
using Aetherium.Client;
using Aetherium.Client.Contracts;
using UnityEngine;

namespace Aetherium.Unity
{
    /// <summary>
    /// The one component a game drops into a scene (docs/design/unity-sample/unity-client-library.md):
    /// owns an <see cref="AetheriumClient"/> (connection + tools + lobby + perception store),
    /// pumps every core event through the <see cref="MainThreadDispatcher"/> — SignalR
    /// callbacks never touch Unity APIs directly — and re-raises them as C# events game
    /// code and the bundled views (GridMapView, EntityViewRegistry) subscribe to.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AetheriumClientBehaviour : MonoBehaviour
    {
        [Header("Server")]
        [Tooltip("Base URL of the Aetherium server; /gamehub is appended.")]
        [SerializeField] private string serverUrl = "http://localhost:5000";

        [Tooltip("Optional world to auto-join on connect (empty = server default session).")]
        [SerializeField] private string worldId = "";

        [Tooltip("Optional map within the world (empty = the world's first map).")]
        [SerializeField] private string mapId = "";

        [SerializeField] private bool autoConnect = true;

        private MainThreadDispatcher _dispatcher;

        /// <summary>The underlying client. Tool/lobby calls are async and thread-safe;
        /// results complete on worker threads, so await them from game code freely but only
        /// touch Unity objects afterward via the events below (already main-threaded).</summary>
        public AetheriumClient Client { get; private set; }

        public PerceptionStore Store => Client?.Store;

        // Main-thread-marshalled event surface.
        public event Action<PerceptionDto> FrameReceived;
        public event Action<TrackedEntity> EntityAppeared;
        public event Action<TrackedEntity, GridPoint, GridPoint> EntityMoved;
        public event Action<TrackedEntity> EntityVanished;
        public event Action<RememberedCell> CellRevealed;
        public event Action<ReanchorReason> Reanchored;
        public event Action<PlayerVitalsDto> Downed;
        public event Action<PlayerVitalsDto> Respawned;
        public event Action<PlayerVitalsDto> Died;
        public event Action<AetheriumConnectionState> ConnectionStateChanged;

        private void Awake()
        {
            _dispatcher = gameObject.AddComponent<MainThreadDispatcher>();

            Client = new AetheriumClient(
                serverUrl,
                string.IsNullOrEmpty(worldId) ? null : worldId,
                string.IsNullOrEmpty(mapId) ? null : mapId);

            // Worker-thread core events → main-thread game events.
            Client.Connection.PerceptionReceived += f => _dispatcher.Enqueue(() => FrameReceived?.Invoke(f));
            Client.Store.EntityAppeared += e => _dispatcher.Enqueue(() => EntityAppeared?.Invoke(e));
            Client.Store.EntityMoved += (e, from, to) => _dispatcher.Enqueue(() => EntityMoved?.Invoke(e, from, to));
            Client.Store.EntityVanished += e => _dispatcher.Enqueue(() => EntityVanished?.Invoke(e));
            Client.Store.CellRevealed += c => _dispatcher.Enqueue(() => CellRevealed?.Invoke(c));
            Client.Store.Reanchored += r => _dispatcher.Enqueue(() => Reanchored?.Invoke(r));
            Client.Connection.Downed += v => _dispatcher.Enqueue(() => Downed?.Invoke(v));
            Client.Connection.Respawned += v => _dispatcher.Enqueue(() => Respawned?.Invoke(v));
            Client.Connection.Died += v => _dispatcher.Enqueue(() => Died?.Invoke(v));
            Client.Connection.StateChanged += s => _dispatcher.Enqueue(() => ConnectionStateChanged?.Invoke(s));
        }

        private async void Start()
        {
            if (!autoConnect)
                return;
            try
            {
                await Client.ConnectAsync();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Aetherium] Connect to {serverUrl} failed: {exception.Message}");
            }
        }

        private async void OnDestroy()
        {
            if (Client == null)
                return;
            try
            {
                await Client.DisposeAsync();
            }
            catch
            {
                // Shutdown teardown — nothing actionable.
            }
        }
    }
}
