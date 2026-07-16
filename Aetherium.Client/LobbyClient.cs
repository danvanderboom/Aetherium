using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Client.Contracts;

namespace Aetherium.Client
{
    /// <summary>
    /// World discovery and membership over the game hub: ListWorlds / GetWorldInfo /
    /// JoinWorld, plus UsePortal (the one non-tool interaction verb). Leaving is implicit
    /// on disconnect — the server has no explicit LeaveWorld. Creating new instances is
    /// operator-side today (aetherctl / management REST); a player-facing "host a station"
    /// is engine gap G6 (M1).
    /// </summary>
    public sealed class LobbyClient
    {
        private readonly AetheriumConnection _connection;

        public LobbyClient(AetheriumConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public Task<List<WorldInfo>> ListWorldsAsync()
            => _connection.InvokeAsync<List<WorldInfo>>("ListWorlds", Array.Empty<object?>());

        public Task<WorldInfo?> GetWorldInfoAsync(string worldId)
            => _connection.InvokeAsync<WorldInfo?>("GetWorldInfo", new object?[] { worldId });

        /// <summary>Joins a world (first map when <paramref name="mapId"/> is null). On success
        /// the store re-anchors — a join is a positional discontinuity by definition.</summary>
        public async Task<JoinWorldResult> JoinWorldAsync(string worldId, string? mapId = null)
        {
            var result = await _connection
                .InvokeAsync<JoinWorldResult>("JoinWorld", new object?[] { worldId, mapId })
                .ConfigureAwait(false);
            if (result.Success)
                _connection.Store.NoteDiscontinuity(ReanchorReason.Joined);
            return result;
        }

        /// <summary>Steps through a portal. On success the store re-anchors and wipes memory —
        /// the far side is a different place, possibly a different map.</summary>
        public async Task<InteractionResultDto> UsePortalAsync(string portalEntityId)
        {
            var result = await _connection
                .InvokeAsync<InteractionResultDto>("UsePortal", new object?[] { portalEntityId })
                .ConfigureAwait(false);
            if (result.Success)
                _connection.Store.NoteDiscontinuity(ReanchorReason.Portal);
            return result;
        }

        // ---- quests / parties / instances (dedicated hub methods) ----

        public Task<List<QuestSummaryDto>> ListAvailableQuestsAsync()
            => _connection.InvokeAsync<List<QuestSummaryDto>>("ListAvailableQuests", Array.Empty<object?>());

        public Task<bool> AcceptQuestAsync(string questId)
            => _connection.InvokeAsync<bool>("AcceptQuest", new object?[] { questId });

        public Task<QuestLogDto> GetQuestLogAsync()
            => _connection.InvokeAsync<QuestLogDto>("GetQuestLog", Array.Empty<object?>());

        public Task<EnterDungeonResultDto> EnterDungeonAsync(string dungeonId, string? partyId = null)
            => _connection.InvokeAsync<EnterDungeonResultDto>("EnterDungeon", new object?[] { dungeonId, partyId });

        public Task<string?> CreatePartyAsync()
            => _connection.InvokeAsync<string?>("CreateParty", Array.Empty<object?>());

        public Task<bool> JoinPartyAsync(string partyId)
            => _connection.InvokeAsync<bool>("JoinParty", new object?[] { partyId });

        public Task<bool> LeavePartyAsync(string partyId)
            => _connection.InvokeAsync<bool>("LeaveParty", new object?[] { partyId });

        public Task<PartyInfoDto?> GetPartyAsync(string partyId)
            => _connection.InvokeAsync<PartyInfoDto?>("GetParty", new object?[] { partyId });
    }

    /// <summary>
    /// The one-object entry point most games want: a connection plus its typed clients.
    /// <code>
    /// await using var client = new AetheriumClient("http://localhost:5000");
    /// await client.ConnectAsync();
    /// await client.Lobby.JoinWorldAsync(worldId);
    /// await client.Tools.MoveAsync(WorldDirection.North);
    /// </code>
    /// </summary>
    public sealed class AetheriumClient : IAsyncDisposable
    {
        public AetheriumConnection Connection { get; }
        public ToolClient Tools { get; }
        public LobbyClient Lobby { get; }
        public PerceptionStore Store => Connection.Store;

        public AetheriumClient(
            string baseUrl,
            string? worldId = null,
            string? mapId = null,
            Func<Task<string?>>? accessTokenProvider = null)
        {
            Connection = new AetheriumConnection(baseUrl, worldId, mapId, accessTokenProvider);
            Tools = new ToolClient(Connection);
            Lobby = new LobbyClient(Connection);
        }

        public Task ConnectAsync(System.Threading.CancellationToken cancellationToken = default)
            => Connection.ConnectAsync(cancellationToken);

        public Task DisconnectAsync() => Connection.DisconnectAsync();

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
}
