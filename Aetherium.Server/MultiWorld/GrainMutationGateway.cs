using System;
using System.Threading.Tasks;
using Aetherium.Model;
using Orleans;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Routes <see cref="IMapMutationGateway"/> calls to <see cref="IGameMapGrain"/>
    /// for sessions joined to an Orleans-hosted map. Every method is a thin
    /// delegation — the grain holds the canonical state and emits the resulting
    /// deltas to the map's SignalR group.
    ///
    /// <para>
    /// Phase 2b+c uses this gateway for sessions that called <c>GameHub.JoinWorld</c>.
    /// Sessions in legacy mode (no <c>?worldId=</c> query parameter) keep using
    /// <see cref="LocalMutationGateway"/> instead.
    /// </para>
    /// </summary>
    public sealed class GrainMutationGateway : IMapMutationGateway
    {
        private readonly IGrainFactory _grainFactory;
        private readonly string _mapId;
        private readonly string _sessionId;

        public GrainMutationGateway(IGrainFactory grainFactory, string mapId, string sessionId)
        {
            _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
            _mapId = string.IsNullOrEmpty(mapId) ? throw new ArgumentException("mapId required") : mapId;
            _sessionId = string.IsNullOrEmpty(sessionId) ? throw new ArgumentException("sessionId required") : sessionId;
        }

        private IGameMapGrain MapGrain => _grainFactory.GetGrain<IGameMapGrain>(_mapId);

        public Task<MoveResult> MoveAsync(Aetherium.Model.RelativeDirection direction, int distance)
            => MapGrain.MoveAsync(_sessionId, direction, distance);

        public Task<RotateResult> RotateAsync(int degrees)
            => MapGrain.RotateAsync(_sessionId, degrees);

        public Task<ChangeLevelResult> ChangeLevelAsync(int deltaZ)
            => MapGrain.ChangeLevelAsync(_sessionId, deltaZ);

        public Task<InteractionResultDto> PickupAsync(string targetEntityId)
            => MapGrain.PickupAsync(_sessionId, targetEntityId);

        public Task<InteractionResultDto> DropAsync(string itemEntityId)
            => MapGrain.DropAsync(_sessionId, itemEntityId);

        public Task<InteractionResultDto> UseAsync(string itemEntityId, string onEntityId, string? usageId = null)
            => MapGrain.UseAsync(_sessionId, itemEntityId, onEntityId, usageId);

        public Task<InteractionResultDto> OpenAsync(string targetEntityId)
            => MapGrain.OpenAsync(_sessionId, targetEntityId);

        public Task<InteractionResultDto> CloseAsync(string targetEntityId)
            => MapGrain.CloseAsync(_sessionId, targetEntityId);

        public Task<AttackResultDto> AttackAsync(string targetEntityId)
            => MapGrain.AttackAsync(_sessionId, targetEntityId);

        public Task<TradeResultDto> TradeAsync(string side, string good, double quantity)
            => MapGrain.TradeAsync(_sessionId, side, good, quantity);
    }
}
