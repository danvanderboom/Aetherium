using System;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Model;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// In-process implementation of <see cref="IMapMutationGateway"/> that mutates
    /// the bound <see cref="GameSession"/>'s local <c>World</c> directly via the
    /// existing <see cref="GameSession"/> movement methods and
    /// <see cref="InteractionSystem"/>.
    ///
    /// <para>
    /// Phase 2a: this is the only implementation. Behavior is identical to the
    /// pre-phase-2a code path — the gateway is a thin shim that lets tools call
    /// through one interface rather than reaching directly into session state.
    /// Phase 2b+c will introduce a <c>GrainMutationGateway</c> that routes to
    /// <c>IGameMapGrain</c>; this local gateway will remain for legacy sessions
    /// (no <c>?worldId=</c>) and for unit tests.
    /// </para>
    /// </summary>
    public sealed class LocalMutationGateway : IMapMutationGateway
    {
        private readonly GameSession _session;
        private readonly InteractionSystem _interactionSystem;
        private readonly CombatSystem _combatSystem = new CombatSystem();

        public LocalMutationGateway(GameSession session, InteractionSystem? interactionSystem = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            // The interaction system is stateless today; sharing one instance is fine,
            // and accepting an injected one supports tests that want to spy on it.
            _interactionSystem = interactionSystem ?? new InteractionSystem();
        }

        public Task<MoveResult> MoveAsync(Aetherium.Model.RelativeDirection direction, int distance)
        {
            if (_session.ViewLocation is null)
                return Task.FromResult(MoveResult.Fail("No view location"));

            var outcome = _session.MoveView(direction, distance);
            return Task.FromResult(outcome.Success
                ? MoveResult.Ok()
                : MoveResult.Fail(outcome.BlockedReason ?? "Blocked"));
        }

        public Task<RotateResult> RotateAsync(int degrees)
        {
            _session.RotateView(degrees);
            return Task.FromResult(RotateResult.Ok(_session.HeadingDegrees));
        }

        public Task<ChangeLevelResult> ChangeLevelAsync(int deltaZ)
        {
            if (_session.ViewLocation is null)
                return Task.FromResult(ChangeLevelResult.Fail("No view location"));

            var outcome = _session.ChangeLevel(deltaZ);
            return Task.FromResult(outcome.Success
                ? ChangeLevelResult.Ok(_session.ViewLocation!.Z)
                : ChangeLevelResult.Fail(outcome.BlockedReason ?? "Blocked"));
        }

        // Interactions run under the session state lock so they serialize against
        // movement/rotation/level-change/perception on the same legacy session — a
        // hub call and a management-grain call can no longer interleave mutations of
        // the same World/Player (P0-12).
        public Task<InteractionResultDto> PickupAsync(string targetEntityId)
            => Task.FromResult(_session.WithStateLock(() => ToDto(_interactionSystem.TryPickup(_session, targetEntityId))));

        public Task<InteractionResultDto> DropAsync(string itemEntityId)
            => Task.FromResult(_session.WithStateLock(() => ToDto(_interactionSystem.TryDrop(_session, itemEntityId))));

        public Task<InteractionResultDto> UseAsync(string itemEntityId, string onEntityId, string? usageId = null)
            => Task.FromResult(_session.WithStateLock(() => ToDto(_interactionSystem.TryUse(_session, itemEntityId, onEntityId, usageId))));

        public Task<InteractionResultDto> OpenAsync(string targetEntityId)
            => Task.FromResult(_session.WithStateLock(() => ToDto(_interactionSystem.TryOpen(_session, targetEntityId))));

        public Task<InteractionResultDto> CloseAsync(string targetEntityId)
            => Task.FromResult(_session.WithStateLock(() => ToDto(_interactionSystem.TryClose(_session, targetEntityId))));

        public Task<AttackResultDto> AttackAsync(string targetEntityId)
            => Task.FromResult(_session.WithStateLock(() =>
            {
                if (_session.Player is null)
                    return AttackResultDto.Fail("No player");

                var result = _combatSystem.TryAttack(_session.World, _session.Player, targetEntityId);
                return new AttackResultDto
                {
                    Success = result.Success,
                    Reason = result.Reason,
                    Damage = result.Damage,
                    RemainingHealth = result.RemainingHealth,
                    TargetDefeated = result.TargetDefeated,
                    TargetType = result.TargetType,
                    TargetEntityId = result.TargetEntityId,
                };
            }));

        private static InteractionResultDto ToDto(InteractionResult result)
        {
            var dto = new InteractionResultDto
            {
                Success = result.Success,
                Reason = result.Reason,
            };
            if (result.Options is { Count: > 0 })
            {
                dto.Options = result.Options
                    .Select(o => new UsageOptionDto
                    {
                        UsageId = o.UsageId,
                        Label = o.Label,
                        Description = o.Description,
                    })
                    .ToList();
            }
            return dto;
        }
    }
}
