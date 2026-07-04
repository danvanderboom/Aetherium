using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Model.Instances;
using Aetherium.Model.Groups;
using Aetherium.Model.Worlds;

namespace Aetherium.Server.Instances
{
    /// <summary>
    /// Orleans grain managing lockout entries for a dungeon.
    /// </summary>
    public class LockoutLedgerGrain : Grain, ILockoutLedgerGrain
    {
        private readonly IPersistentState<LockoutLedgerState> _state;

        public LockoutLedgerGrain(
            [PersistentState("lockouts", "worldStore")] IPersistentState<LockoutLedgerState> state)
        {
            _state = state;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new LockoutLedgerState
                {
                    DungeonId = new DungeonId(this.GetPrimaryKeyString()),
                    Lockouts = new Dictionary<string, LockoutEntry>(),
                    Policy = null
                };
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public async Task<LockoutCheckResult> CheckLockoutAsync(PartyId? partyId, List<PlayerId> playerIds)
        {
            var now = DateTime.UtcNow;
            var dungeonId = new DungeonId(this.GetPrimaryKeyString());
            var policy = _state.State.Policy;

            if (policy == null)
            {
                // No policy = no lockout
                return new LockoutCheckResult
                {
                    CanEnter = true,
                    LockoutKey = null
                };
            }

            // Check party lockout if party ID provided
            if (partyId.HasValue)
            {
                var partyLockoutKey = GetPartyLockoutKey(partyId.Value);
                if (_state.State.Lockouts.TryGetValue(partyLockoutKey, out var partyLockout))
                {
                    if (IsLocked(partyLockout, policy, now))
                    {
                        return new LockoutCheckResult
                        {
                            CanEnter = false,
                            Reason = "Party is locked out",
                            LockoutUntil = partyLockout.LockoutUntil,
                            LockoutKey = partyLockout.LockoutKey
                        };
                    }
                }
            }

            // Check individual player lockouts
            foreach (var playerId in playerIds)
            {
                var playerLockoutKey = GetPlayerLockoutKey(playerId);
                if (_state.State.Lockouts.TryGetValue(playerLockoutKey, out var playerLockout))
                {
                    if (IsLocked(playerLockout, policy, now))
                    {
                        return new LockoutCheckResult
                        {
                            CanEnter = false,
                            Reason = $"Player {playerId.Value} is locked out",
                            LockoutUntil = playerLockout.LockoutUntil,
                            LockoutKey = playerLockout.LockoutKey
                        };
                    }
                }
            }

            // Can enter
            return new LockoutCheckResult
            {
                CanEnter = true,
                LockoutKey = null
            };
        }

        public async Task<LockoutKey> RecordLockoutAsync(PartyId? partyId, List<PlayerId> playerIds, InstanceId instanceId)
        {
            var now = DateTime.UtcNow;
            var dungeonId = new DungeonId(this.GetPrimaryKeyString());
            var policy = _state.State.Policy ?? CreateDefaultPolicy(dungeonId);

            // The ledger otherwise only ever grows: expired entries stay in the
            // dictionary forever. Piggyback the purge on record (which writes state
            // anyway) rather than adding a timer.
            PurgeStaleLockouts(policy, now);

            LockoutKey lockoutKey;

            // Record party lockout if party ID provided
            if (partyId.HasValue)
            {
                lockoutKey = new LockoutKey($"party:{partyId.Value.Value}");
                var partyLockout = new LockoutEntry
                {
                    LockoutKey = lockoutKey,
                    DungeonId = dungeonId,
                    PartyId = partyId,
                    LockoutUntil = now.Add(policy.Duration),
                    AttemptsUsed = 1,
                    IsLocked = true,
                    LastAttemptAt = now,
                    InstanceId = instanceId
                };

                if (_state.State.Lockouts.TryGetValue(lockoutKey.Value, out var existing))
                {
                    ApplyReEntry(existing, instanceId, policy, now);
                    _state.State.Lockouts[lockoutKey.Value] = existing;
                }
                else
                {
                    _state.State.Lockouts[lockoutKey.Value] = partyLockout;
                }
            }
            else
            {
                // Use first player ID for individual lockout
                lockoutKey = new LockoutKey(GetPlayerLockoutKey(playerIds[0]));
            }

            // Record individual player lockouts
            foreach (var playerId in playerIds)
            {
                var playerLockoutKey = GetPlayerLockoutKey(playerId);
                var playerLockout = new LockoutEntry
                {
                    LockoutKey = new LockoutKey(playerLockoutKey),
                    DungeonId = dungeonId,
                    PlayerId = playerId,
                    LockoutUntil = now.Add(policy.Duration),
                    AttemptsUsed = 1,
                    IsLocked = true,
                    LastAttemptAt = now,
                    InstanceId = instanceId
                };

                if (_state.State.Lockouts.TryGetValue(playerLockoutKey, out var existing))
                {
                    ApplyReEntry(existing, instanceId, policy, now);
                    _state.State.Lockouts[playerLockoutKey] = existing;
                }
                else
                {
                    _state.State.Lockouts[playerLockoutKey] = playerLockout;
                }
            }

            await _state.WriteStateAsync();
            return lockoutKey;
        }

        public async Task<bool> ClearLockoutAsync(LockoutKey lockoutKey)
        {
            if (_state.State.Lockouts.TryGetValue(lockoutKey.Value, out var lockout))
            {
                lockout.IsLocked = false;
                lockout.LockoutUntil = DateTime.MinValue;
                _state.State.Lockouts[lockoutKey.Value] = lockout;
                await _state.WriteStateAsync();
                return true;
            }

            return false;
        }

        public Task<List<LockoutEntry>> GetPlayerLockoutsAsync(PlayerId playerId)
        {
            var playerLockoutKey = GetPlayerLockoutKey(playerId);
            var lockouts = _state.State.Lockouts.Values
                .Where(l => l.PlayerId.HasValue && l.PlayerId.Value.Equals(playerId) && l.IsLocked)
                .ToList();

            return Task.FromResult(lockouts);
        }

        public Task<List<LockoutEntry>> GetPartyLockoutsAsync(PartyId partyId)
        {
            var lockouts = _state.State.Lockouts.Values
                .Where(l => l.PartyId.HasValue && l.PartyId.Value.Equals(partyId) && l.IsLocked)
                .ToList();

            return Task.FromResult(lockouts);
        }

        public async Task SetPolicyAsync(LockoutPolicy policy)
        {
            _state.State.Policy = policy;
            await _state.WriteStateAsync();
        }

        public Task<LockoutPolicy?> GetPolicyAsync()
        {
            return Task.FromResult(_state.State.Policy);
        }

        // How long an unlocked entry is kept (for attempt history) before purging.
        private static readonly TimeSpan StaleLockoutRetention = TimeSpan.FromDays(7);

        private void PurgeStaleLockouts(LockoutPolicy policy, DateTime now)
        {
            var staleBefore = now - StaleLockoutRetention;
            var staleKeys = _state.State.Lockouts
                .Where(kvp => !IsLocked(kvp.Value, policy, now) && kvp.Value.LastAttemptAt < staleBefore)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in staleKeys)
                _state.State.Lockouts.Remove(key);
        }

        /// <summary>
        /// Updates an existing lockout entry for a re-entry. Re-entering the SAME instance only
        /// touches the timestamp — it does not increment the attempt count or extend the window
        /// (a player rejoining their own run must not double-count against them). Entering a
        /// DIFFERENT instance is a genuine new run, so it increments attempts and extends the window.
        /// </summary>
        private static void ApplyReEntry(LockoutEntry existing, InstanceId instanceId, LockoutPolicy policy, DateTime now)
        {
            bool sameInstance = existing.InstanceId.HasValue && existing.InstanceId.Value.Equals(instanceId);

            existing.LastAttemptAt = now;
            existing.IsLocked = true;

            if (sameInstance)
                return;

            existing.InstanceId = instanceId;
            existing.AttemptsUsed++;
            existing.LockoutUntil = now.Add(policy.Duration);
        }

        private bool IsLocked(LockoutEntry lockout, LockoutPolicy policy, DateTime now)
        {
            if (!lockout.IsLocked)
                return false;

            // Check time-based lockout
            if (now < lockout.LockoutUntil)
                return true;

            // Check attempt-based lockout
            if (policy.MaxAttempts > 0 && lockout.AttemptsUsed >= policy.MaxAttempts)
                return true;

            // Not locked
            return false;
        }

        private string GetPartyLockoutKey(PartyId partyId)
        {
            return $"party:{partyId.Value}";
        }

        private string GetPlayerLockoutKey(PlayerId playerId)
        {
            return $"player:{playerId.Value}";
        }

        private LockoutPolicy CreateDefaultPolicy(DungeonId dungeonId)
        {
            return new LockoutPolicy
            {
                DungeonId = dungeonId,
                Type = LockoutType.TimeBased,
                Duration = TimeSpan.FromHours(24),
                MaxAttempts = -1,
                ResetOnSuccess = false
            };
        }
    }

    /// <summary>
    /// State for the lockout ledger grain.
    /// </summary>
    [GenerateSerializer]
    public class LockoutLedgerState
    {
        [Id(0)] public DungeonId DungeonId { get; set; }
        [Id(1)] public Dictionary<string, LockoutEntry> Lockouts { get; set; } = new Dictionary<string, LockoutEntry>();
        [Id(2)] public LockoutPolicy? Policy { get; set; }
    }
}

