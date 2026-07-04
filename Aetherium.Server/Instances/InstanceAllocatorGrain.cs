using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Model.Instances;
using Aetherium.Model.Worlds;
using Aetherium.Model.Groups;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Server.Instances
{
    /// <summary>
    /// Orleans grain allocating dungeon instances for parties/players.
    /// Coordinates instance creation, lockout checks, and reuse logic.
    /// </summary>
    public class InstanceAllocatorGrain : Grain, IInstanceAllocatorGrain
    {
        private readonly IPersistentState<InstanceAllocatorState> _state;
        private readonly IGrainFactory _grainFactory;
        private IGrainTimer? _sweepTimer;

        // How often the abandoned-instance sweeper runs, and how long an instance may sit idle
        // (no activity, but not yet marked Abandoned) before it is reaped.
        private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan IdleReapThreshold = TimeSpan.FromMinutes(30);

        public InstanceAllocatorGrain(
            [PersistentState("allocator", "worldStore")] IPersistentState<InstanceAllocatorState> state,
            IGrainFactory grainFactory)
        {
            _state = state;
            _grainFactory = grainFactory;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new InstanceAllocatorState
                {
                    WorldId = this.GetPrimaryKeyString(),
                    ActiveInstances = new Dictionary<string, InstanceAllocation>(),
                    PartyInstances = new Dictionary<string, InstanceId>(), // PartyId -> InstanceId
                    PlayerInstances = new Dictionary<string, InstanceId>()  // PlayerId -> InstanceId
                };
            }

            // Reap abandoned/idle instances periodically so dead dungeon maps stop being ticked and
            // allocations don't accumulate. Errors are swallowed inside the sweep so a bad instance
            // can't kill the timer.
            _sweepTimer = this.RegisterGrainTimer(
                async _ => await SweepAbandonedInstancesAsync(),
                state: (object?)null,
                new GrainTimerCreationOptions { DueTime = SweepInterval, Period = SweepInterval, Interleave = true });

            return base.OnActivateAsync(cancellationToken);
        }

        public async Task<EnterInstanceResult> EnterAsync(EnterInstanceRequest request)
        {
            var worldId = this.GetPrimaryKeyString();
            
            // Validate world ID matches
            if (request.WorldId.Value != worldId)
            {
                return new EnterInstanceResult
                {
                    Success = false,
                    ErrorMessage = $"World ID mismatch: expected {worldId}, got {request.WorldId.Value}"
                };
            }

            // Check lockout
            var lockoutLedger = _grainFactory.GetGrain<ILockoutLedgerGrain>(request.DungeonId.Value);
            var lockoutCheck = await lockoutLedger.CheckLockoutAsync(request.PartyId, request.PlayerIds);

            if (!lockoutCheck.CanEnter)
            {
                return new EnterInstanceResult
                {
                    Success = false,
                    ErrorMessage = lockoutCheck.Reason ?? "Locked out",
                    LockoutKey = lockoutCheck.LockoutKey
                };
            }

            // Try to get or reuse existing instance
            var existingInstanceId = await GetOrReuseInstanceAsync(request.DungeonId, request.PartyId, request.PlayerIds);

            if (existingInstanceId.HasValue)
            {
                // Reuse existing instance
                var instanceGrain = _grainFactory.GetGrain<IDungeonInstanceGrain>(existingInstanceId.Value.Value);
                var added = await instanceGrain.AddPlayersAsync(request.PlayerIds);

                if (added)
                {
                    // Record the lockout on the reuse path too, so re-entry isn't a loophole that
                    // avoids lockout. RecordLockoutAsync is idempotent for the same instance id, so
                    // re-entering your own run neither double-counts attempts nor extends the window.
                    var reuseLockoutKey = await lockoutLedger.RecordLockoutAsync(
                        request.PartyId, request.PlayerIds, existingInstanceId.Value);

                    return new EnterInstanceResult
                    {
                        Success = true,
                        InstanceId = existingInstanceId.Value,
                        LockoutKey = reuseLockoutKey
                    };
                }
            }

            // Allocate new instance
            try
            {
                var instanceId = await AllocateInstanceAsync(request.DungeonId, request.PartyId, request.PlayerIds);

                // Record lockout
                var lockoutKey = await lockoutLedger.RecordLockoutAsync(request.PartyId, request.PlayerIds, instanceId);

                return new EnterInstanceResult
                {
                    Success = true,
                    InstanceId = instanceId,
                    LockoutKey = lockoutKey
                };
            }
            catch (Exception ex)
            {
                return new EnterInstanceResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to allocate instance: {ex.Message}"
                };
            }
        }

        public async Task<InstanceId> AllocateInstanceAsync(DungeonId dungeonId, PartyId? partyId, List<PlayerId> playerIds)
        {
            // Generate new instance ID
            var instanceId = new InstanceId(Guid.NewGuid().ToString());

            // Create instance grain and initialize
            var instanceGrain = _grainFactory.GetGrain<IDungeonInstanceGrain>(instanceId.Value);

            // Get world grain to create a map for this instance
            var worldId = this.GetPrimaryKeyString();
            var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);

            // Create a map for this dungeon instance
            var generatorParams = new Dictionary<string, object>
            {
                { "dungeonId", dungeonId.Value },
                { "instanceId", instanceId.Value }
            };

            var mapId = await worldGrain.AddMapAsync(
                $"Dungeon-{dungeonId.Value}-{instanceId.Value}",
                "dungeon", // Generator type for dungeons
                generatorParams);

            // Store map ID in generator params for instance initialization
            generatorParams["mapId"] = mapId;

            // Initialize instance configuration
            var config = new InstanceConfig
            {
                InstanceId = instanceId,
                DungeonId = dungeonId,
                WorldId = new WorldId(worldId),
                DungeonName = $"Dungeon {dungeonId.Value}",
                GeneratorType = "dungeon",
                GeneratorParameters = generatorParams,
                PartyId = partyId,
                PlayerIds = playerIds,
                CreatedAt = DateTime.UtcNow
            };

            await instanceGrain.InitializeAsync(config);
            await instanceGrain.AddPlayersAsync(playerIds);

            // Track instance allocation
            var allocation = new InstanceAllocation
            {
                InstanceId = instanceId,
                DungeonId = dungeonId,
                PartyId = partyId,
                PlayerIds = playerIds,
                CreatedAt = DateTime.UtcNow,
                MapId = mapId
            };

            _state.State.ActiveInstances[instanceId.Value] = allocation;

            // Track party/player -> instance mappings
            if (partyId.HasValue)
            {
                _state.State.PartyInstances[partyId.Value.Value] = instanceId;
            }

            foreach (var playerId in playerIds)
            {
                _state.State.PlayerInstances[playerId.Value] = instanceId;
            }

            await _state.WriteStateAsync();

            return instanceId;
        }

        public Task<InstanceId?> GetOrReuseInstanceAsync(DungeonId dungeonId, PartyId? partyId, List<PlayerId> playerIds)
        {
            // Check party instance first
            if (partyId.HasValue && _state.State.PartyInstances.TryGetValue(partyId.Value.Value, out var partyInstanceId))
            {
                // Verify instance still exists and is for this dungeon
                if (_state.State.ActiveInstances.TryGetValue(partyInstanceId.Value, out var allocation) &&
                    allocation.DungeonId.Equals(dungeonId))
                {
                    return Task.FromResult<InstanceId?>(partyInstanceId);
                }
                else
                {
                    // Clean up stale mapping
                    _state.State.PartyInstances.Remove(partyId.Value.Value);
                }
            }

            // Check individual player instances
            foreach (var playerId in playerIds)
            {
                if (_state.State.PlayerInstances.TryGetValue(playerId.Value, out var playerInstanceId))
                {
                    // Verify instance still exists and is for this dungeon
                    if (_state.State.ActiveInstances.TryGetValue(playerInstanceId.Value, out var allocation) &&
                        allocation.DungeonId.Equals(dungeonId))
                    {
                        return Task.FromResult<InstanceId?>(playerInstanceId);
                    }
                    else
                    {
                        // Clean up stale mapping
                        _state.State.PlayerInstances.Remove(playerId.Value);
                    }
                }
            }

            return Task.FromResult<InstanceId?>(null);
        }

        public async Task ReleaseInstanceAsync(InstanceId instanceId)
        {
            if (!_state.State.ActiveInstances.TryGetValue(instanceId.Value, out var allocation))
                return;

            // Free the instance's map so it stops being ticked/saved by the world. Without this the
            // abandoned dungeon map lingers in WorldInfo.MapIds and is ticked forever.
            if (!string.IsNullOrEmpty(allocation.MapId))
            {
                try
                {
                    var worldGrain = _grainFactory.GetGrain<IWorldGrain>(this.GetPrimaryKeyString());
                    await worldGrain.RemoveMapAsync(allocation.MapId!);
                }
                catch (Exception ex)
                {
                    // Non-fatal: still drop the allocation below so the world isn't wedged.
                    Console.WriteLine($"[InstanceAllocatorGrain] Failed to remove map {allocation.MapId}: {ex.Message}");
                }
            }

            // Remove party mapping
            if (allocation.PartyId.HasValue)
            {
                _state.State.PartyInstances.Remove(allocation.PartyId.Value.Value);
            }

            // Remove player mappings
            foreach (var playerId in allocation.PlayerIds)
            {
                _state.State.PlayerInstances.Remove(playerId.Value);
            }

            // Remove from active instances
            _state.State.ActiveInstances.Remove(instanceId.Value);

            await _state.WriteStateAsync();
        }

        public async Task<int> SweepAbandonedInstancesAsync()
        {
            // Snapshot: ShutdownAsync → ReleaseInstanceAsync mutates ActiveInstances.
            var candidates = _state.State.ActiveInstances.Values.ToList();
            if (candidates.Count == 0)
                return 0;

            var now = DateTime.UtcNow;
            int reaped = 0;

            foreach (var allocation in candidates)
            {
                bool reap;
                try
                {
                    var instanceGrain = _grainFactory.GetGrain<IDungeonInstanceGrain>(allocation.InstanceId.Value);
                    var info = await instanceGrain.GetInfoAsync();

                    if (info == null)
                    {
                        // Instance grain has no live info (never fully created / already gone):
                        // drop the dangling allocation and free any map it referenced.
                        await ReleaseInstanceAsync(allocation.InstanceId);
                        reaped++;
                        continue;
                    }

                    bool abandoned = info.State == InstanceState.Abandoned || info.State == InstanceState.Stopped;
                    bool idle = info.PlayerCount == 0
                                && info.LastActivityAt.HasValue
                                && (now - info.LastActivityAt.Value) > IdleReapThreshold;
                    reap = abandoned || idle;

                    if (reap)
                    {
                        // Tear the instance down WITHOUT letting it call back into this allocator
                        // (ShutdownAsync → ReleaseInstanceAsync would re-enter this grain and
                        // deadlock), then release it locally to free its map and drop the allocation.
                        await instanceGrain.TeardownAsync();
                        await ReleaseInstanceAsync(allocation.InstanceId);
                        reaped++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[InstanceAllocatorGrain] Sweep failed for {allocation.InstanceId.Value}: {ex.Message}");
                }
            }

            return reaped;
        }
    }

    /// <summary>
    /// State for the instance allocator grain.
    /// </summary>
    [GenerateSerializer]
    public class InstanceAllocatorState
    {
        [Id(0)] public string WorldId { get; set; } = string.Empty;
        [Id(1)] public Dictionary<string, InstanceAllocation> ActiveInstances { get; set; } = new Dictionary<string, InstanceAllocation>();
        [Id(2)] public Dictionary<string, InstanceId> PartyInstances { get; set; } = new Dictionary<string, InstanceId>(); // PartyId -> InstanceId
        [Id(3)] public Dictionary<string, InstanceId> PlayerInstances { get; set; } = new Dictionary<string, InstanceId>(); // PlayerId -> InstanceId
    }

    /// <summary>
    /// Allocation tracking for an instance.
    /// </summary>
    [GenerateSerializer]
    public class InstanceAllocation
    {
        [Id(0)] public InstanceId InstanceId { get; set; }
        [Id(1)] public DungeonId DungeonId { get; set; }
        [Id(2)] public PartyId? PartyId { get; set; }
        [Id(3)] public List<PlayerId> PlayerIds { get; set; } = new List<PlayerId>();
        [Id(4)] public DateTime CreatedAt { get; set; }
        [Id(5)] public string? MapId { get; set; }
    }
}

