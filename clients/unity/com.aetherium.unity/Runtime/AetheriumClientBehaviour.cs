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

        [Tooltip("Optional world to auto-join on connect (empty = server default session, or resolve by Join Game Definition Id below).")]
        [SerializeField] private string worldId = "";

        [Tooltip("Optional map within the world (empty = the world's first map).")]
        [SerializeField] private string mapId = "";

        [Tooltip("When Server World Id is empty, join the newest running world with this game bundle id " +
                 "(e.g. \"aphelion-h3\"). Lets a player reach a live instance without pasting an ephemeral " +
                 "world GUID. Empty = leave the connection on the server's default session.")]
        [SerializeField] private string joinGameDefinitionId = "";

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

                // If no explicit world was configured but a game bundle id was, resolve a live
                // instance of that bundle from the lobby and join it. The query-string auto-join
                // (worldId field) already fired inside ConnectAsync; this is the GUID-free path.
                if (string.IsNullOrEmpty(worldId) && !string.IsNullOrEmpty(joinGameDefinitionId))
                    await JoinNewestWorldOfGameAsync(joinGameDefinitionId);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Aetherium] Connect to {serverUrl} failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Discovers running worlds via the lobby and joins the newest one whose game bundle id
        /// matches <paramref name="gameDefinitionId"/>. Returns the joined world id, or null when
        /// no matching world is advertised. Safe to call from game code (e.g. a connect menu).
        /// </summary>
        public async System.Threading.Tasks.Task<string> JoinNewestWorldOfGameAsync(string gameDefinitionId)
        {
            var worlds = await Client.Lobby.ListWorldsAsync();
            string best = null;
            DateTime bestWhen = DateTime.MinValue;
            foreach (var world in worlds)
            {
                if (!string.Equals(world.GameDefinitionId, gameDefinitionId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (world.CreatedAt >= bestWhen)
                {
                    bestWhen = world.CreatedAt;
                    best = world.WorldId;
                }
            }

            if (best == null)
            {
                Debug.LogWarning($"[Aetherium] No running world found for game '{gameDefinitionId}'. " +
                                 "Create one (aetherctl game create " + gameDefinitionId + ") and reconnect.");
                return null;
            }

            var result = await Client.Lobby.JoinWorldAsync(best);
            if (!result.Success)
            {
                Debug.LogError($"[Aetherium] Join of world '{best}' failed: {result.Reason}");
                return null;
            }

            Debug.Log($"[Aetherium] Joined '{gameDefinitionId}' world {best}.");
            return best;
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
