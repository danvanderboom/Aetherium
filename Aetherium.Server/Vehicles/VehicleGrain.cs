using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Aetherium.Model.Vehicles;
using Aetherium.Server.MultiWorld;
using Aetherium.WorldGen;

namespace Aetherium.Server.Vehicles
{
    /// <summary>
    /// Owns one boardable vehicle (add-boardable-vehicles). Modeled on <c>DungeonInstanceGrain</c>: its
    /// map is a ship interior (the "Main" map of a dedicated world created at <see cref="InitializeAsync"/>),
    /// and it moves parties in/out of that interior. The one deviation from the dungeon grain is boarding —
    /// because the interior is on the vehicle's own (separate) world, players are moved with
    /// <c>IGameMapGrain.JoinPlayerAsync</c> + <c>IWorldGrain.RegisterPlayerLocationAsync</c>/
    /// <c>UnregisterPlayerAsync</c> (the cross-world re-point primitives from Phase 0) rather than the
    /// in-world <c>MovePlayerToMapAsync</c>. Auto-discovered by Orleans; reuses the "worldStore" provider.
    /// </summary>
    public class VehicleGrain : Grain, IVehicleGrain, IRemindable
    {
        private const string VoyageReminderName = "vehicle-voyage";

        private readonly IPersistentState<VehicleState> _state;
        private readonly IGrainFactory _grainFactory;
        private readonly MapGeneratorRegistry _generatorRegistry;

        public VehicleGrain(
            [PersistentState("vehicle", "worldStore")] IPersistentState<VehicleState> state,
            IGrainFactory grainFactory,
            MapGeneratorRegistry generatorRegistry)
        {
            _state = state;
            _grainFactory = grainFactory;
            _generatorRegistry = generatorRegistry;
        }

        private GameSessionManager? SessionManager =>
            ServiceProvider.GetService(typeof(GameSessionManager)) as GameSessionManager;

        /// <summary>Prefix marking a world as a vehicle's interior world. The vehicle-instance id is
        /// encoded in the world id so a player on the interior can resolve their vehicle from the session's
        /// current world (used by the <c>disembark</c> verb).</summary>
        public const string InteriorWorldPrefix = "vehicle-world:";

        /// <summary>The interior world id for a vehicle instance.</summary>
        public static string InteriorWorldIdFor(string vehicleInstanceId) => InteriorWorldPrefix + vehicleInstanceId;

        /// <summary>The vehicle-instance id encoded in an interior world id, or null when
        /// <paramref name="worldId"/> is not a vehicle interior world.</summary>
        public static string? VehicleIdFromWorldId(string? worldId) =>
            !string.IsNullOrEmpty(worldId) && worldId.StartsWith(InteriorWorldPrefix, System.StringComparison.Ordinal)
                ? worldId.Substring(InteriorWorldPrefix.Length)
                : null;

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);

            // Recovery (add-boardable-vehicles Phase 3): a voyage in flight when the grain last
            // deactivated re-arms its reminder from the persisted ETA, so the journey resumes without a
            // fresh DepartAsync. If the ETA has already passed while the grain was down, the first wake
            // arrives it immediately.
            if (_state.State.InTransit)
                await TryArmVoyageReminderAsync();
        }

        public async Task InitializeAsync(VehicleConfig config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            // Idempotent — a re-activated grain with a persisted interior is already initialized.
            if (!string.IsNullOrEmpty(_state.State.InteriorMapId))
                return;

            _state.State.Config = config;

            // The interior is the "Main" map of a dedicated world for this vehicle, so it persists across
            // voyages while only the lightweight exterior footprint moves between surface worlds.
            var interiorWorldId = InteriorWorldIdFor(this.GetPrimaryKeyString());
            var worldGrain = _grainFactory.GetGrain<IWorldGrain>(interiorWorldId);
            await worldGrain.InitializeAsync(new WorldConfig
            {
                WorldId = interiorWorldId,
                Name = string.IsNullOrEmpty(config.DisplayName) ? "Vehicle interior" : $"{config.DisplayName} interior",
                GeneratorType = string.IsNullOrEmpty(config.InteriorGenerator) ? "rooms-and-corridors" : config.InteriorGenerator,
                Size = new WorldSize
                {
                    Width = Math.Max(4, config.InteriorWidth),
                    Height = Math.Max(4, config.InteriorHeight),
                    Depth = 1,
                },
                GeneratorParameters = new Dictionary<string, object> { { "seed", config.InteriorSeed } },
            });

            var mapIds = await worldGrain.GetMapIdsAsync();
            _state.State.InteriorWorldId = interiorWorldId;
            _state.State.InteriorMapId = mapIds.FirstOrDefault();
            await _state.WriteStateAsync();
        }

        public async Task<VehicleLandingResult> LandAsync(string surfaceWorldId, string surfaceMapId, int anchorX, int anchorY, int anchorZ)
        {
            if (_state.State.Config is null)
                return VehicleLandingResult.Fail("Vehicle not initialized");
            if (string.IsNullOrEmpty(surfaceWorldId) || string.IsNullOrEmpty(surfaceMapId))
                return VehicleLandingResult.Fail("surfaceWorldId and surfaceMapId required");

            var cfg = _state.State.Config;
            var mapGrain = _grainFactory.GetGrain<IGameMapGrain>(surfaceMapId);
            var placed = await mapGrain.PlaceVehicleExteriorAsync(
                this.GetPrimaryKeyString(), cfg.DisplayName,
                anchorX, anchorY, anchorZ,
                cfg.FootprintWidth, cfg.FootprintLength, cfg.FootprintDepth,
                cfg.ExteriorTerrain ?? string.Empty, cfg.LandingTerrain ?? new List<string>());

            if (!placed)
                return VehicleLandingResult.Fail("No valid landing footprint at that location");

            _state.State.DockWorldId = surfaceWorldId;
            _state.State.DockMapId = surfaceMapId;
            _state.State.AnchorX = anchorX;
            _state.State.AnchorY = anchorY;
            _state.State.AnchorZ = anchorZ;
            _state.State.Landed = true;
            _state.State.InTransit = false;
            await _state.WriteStateAsync();
            return VehicleLandingResult.Ok(surfaceMapId);
        }

        public async Task TakeOffAsync()
        {
            if (!_state.State.Landed || string.IsNullOrEmpty(_state.State.DockMapId))
                return;

            var mapGrain = _grainFactory.GetGrain<IGameMapGrain>(_state.State.DockMapId);
            await mapGrain.RemoveVehicleExteriorAsync(this.GetPrimaryKeyString());
            _state.State.Landed = false;
            await _state.WriteStateAsync();
        }

        public async Task<VehicleBoardResult> BoardAsync(IReadOnlyList<string> playerIds)
        {
            if (_state.State.Config is null || string.IsNullOrEmpty(_state.State.InteriorMapId) || string.IsNullOrEmpty(_state.State.InteriorWorldId))
                return VehicleBoardResult.Fail("Vehicle not initialized");
            if (!_state.State.Landed || string.IsNullOrEmpty(_state.State.DockMapId) || string.IsNullOrEmpty(_state.State.DockWorldId))
                return VehicleBoardResult.Fail("Vehicle must be landed to board");
            if (playerIds is null || playerIds.Count == 0)
                return VehicleBoardResult.Ok(0);

            var cfg = _state.State.Config;
            var interiorMap = _grainFactory.GetGrain<IGameMapGrain>(_state.State.InteriorMapId);
            var interiorWorld = _grainFactory.GetGrain<IWorldGrain>(_state.State.InteriorWorldId);
            var dockMap = _grainFactory.GetGrain<IGameMapGrain>(_state.State.DockMapId);
            var dockWorld = _grainFactory.GetGrain<IWorldGrain>(_state.State.DockWorldId);
            var sessionManager = SessionManager;

            var room = Math.Max(0, cfg.Capacity - _state.State.Passengers.Count);
            var manifest = playerIds.Where(p => !string.IsNullOrEmpty(p) && !_state.State.Passengers.Contains(p)).ToList();
            var toBoard = manifest.Take(room).ToList();
            int rejected = playerIds.Count - toBoard.Count; // already aboard, invalid, or over capacity

            int boarded = 0;
            foreach (var player in toBoard)
            {
                var join = await interiorMap.JoinPlayerAsync(player);
                if (!join.Success)
                {
                    rejected++;
                    continue;
                }

                await interiorWorld.RegisterPlayerLocationAsync(player, _state.State.InteriorMapId);
                await dockMap.LeavePlayerAsync(player);
                await dockWorld.UnregisterPlayerAsync(player);

                if (sessionManager is not null)
                    await sessionManager.RepointSessionToMapAsync(
                        _grainFactory, _generatorRegistry, player,
                        _state.State.InteriorWorldId, _state.State.InteriorMapId, join.SpawnLocation());

                _state.State.Passengers.Add(player);
                boarded++;
            }

            await _state.WriteStateAsync();
            return VehicleBoardResult.Ok(boarded, rejected);
        }

        public async Task<VehicleBoardResult> DisembarkAsync(IReadOnlyList<string> playerIds)
        {
            if (_state.State.Config is null || string.IsNullOrEmpty(_state.State.InteriorMapId) || string.IsNullOrEmpty(_state.State.InteriorWorldId))
                return VehicleBoardResult.Fail("Vehicle not initialized");
            if (!_state.State.Landed || string.IsNullOrEmpty(_state.State.DockMapId) || string.IsNullOrEmpty(_state.State.DockWorldId))
                return VehicleBoardResult.Fail("Vehicle must be landed to disembark onto a surface");
            if (playerIds is null || playerIds.Count == 0)
                return VehicleBoardResult.Ok(0);

            var interiorMap = _grainFactory.GetGrain<IGameMapGrain>(_state.State.InteriorMapId);
            var interiorWorld = _grainFactory.GetGrain<IWorldGrain>(_state.State.InteriorWorldId);
            var dockMap = _grainFactory.GetGrain<IGameMapGrain>(_state.State.DockMapId);
            var dockWorld = _grainFactory.GetGrain<IWorldGrain>(_state.State.DockWorldId);
            var sessionManager = SessionManager;

            int moved = 0, rejected = 0;
            foreach (var player in playerIds)
            {
                if (string.IsNullOrEmpty(player) || !_state.State.Passengers.Contains(player))
                {
                    rejected++;
                    continue;
                }

                var join = await dockMap.JoinPlayerAsync(player);
                if (!join.Success)
                {
                    rejected++;
                    continue;
                }

                await dockWorld.RegisterPlayerLocationAsync(player, _state.State.DockMapId);
                await interiorMap.LeavePlayerAsync(player);
                await interiorWorld.UnregisterPlayerAsync(player);

                if (sessionManager is not null)
                    await sessionManager.RepointSessionToMapAsync(
                        _grainFactory, _generatorRegistry, player,
                        _state.State.DockWorldId, _state.State.DockMapId, join.SpawnLocation());

                _state.State.Passengers.Remove(player);
                moved++;
            }

            await _state.WriteStateAsync();
            return VehicleBoardResult.Ok(moved, rejected);
        }

        public async Task TickAsync(TimeSpan gameTimeElapsed)
        {
            if (!string.IsNullOrEmpty(_state.State.InteriorMapId))
                await _grainFactory.GetGrain<IGameMapGrain>(_state.State.InteriorMapId).TickAsync(gameTimeElapsed);
        }

        public async Task<VoyageResult> DepartAsync(string destinationWorldId, string destinationMapId,
            int destAnchorX, int destAnchorY, int destAnchorZ, double voyageMinutes)
        {
            if (_state.State.Config is null || string.IsNullOrEmpty(_state.State.InteriorMapId))
                return VoyageResult.Fail("Vehicle not initialized");
            if (!_state.State.Landed || string.IsNullOrEmpty(_state.State.DockMapId))
                return VoyageResult.Fail("Vehicle must be landed to depart");
            if (string.IsNullOrEmpty(destinationWorldId) || string.IsNullOrEmpty(destinationMapId))
                return VoyageResult.Fail("destinationWorldId and destinationMapId required");

            // Takeoff: remove the exterior footprint from the origin surface.
            var originMap = _grainFactory.GetGrain<IGameMapGrain>(_state.State.DockMapId);
            await originMap.RemoveVehicleExteriorAsync(this.GetPrimaryKeyString());

            var now = DateTime.UtcNow;
            _state.State.DepartedUtc = now;
            _state.State.EtaUtc = now.AddMinutes(Math.Max(0.0, voyageMinutes));
            _state.State.DestWorldId = destinationWorldId;
            _state.State.DestMapId = destinationMapId;
            _state.State.DestAnchorX = destAnchorX;
            _state.State.DestAnchorY = destAnchorY;
            _state.State.DestAnchorZ = destAnchorZ;
            _state.State.Landed = false;
            _state.State.InTransit = true;

            // Schedule the authored in-transit events at their absolute due times (Phase 4). They fire on
            // the voyage reminder the grain already drives — no dependency on the global tick driver.
            _state.State.ScheduledEvents = (_state.State.Config.InTransitEvents ?? new List<VoyageEventDef>())
                .Select(e => new ScheduledVoyageEvent
                {
                    DueUtc = now.AddMinutes(Math.Max(0.0, e.OffsetMinutes)),
                    EventType = e.EventType ?? string.Empty,
                    Description = e.Description ?? string.Empty,
                    Fired = false,
                })
                .ToList();

            await _state.WriteStateAsync();

            await TryArmVoyageReminderAsync();
            await PushVoyageUpdateAsync(arrived: false);
            return VoyageResult.Ok(_state.State.EtaUtc.Value);
        }

        public async Task TickVoyageAsync()
        {
            if (!_state.State.InTransit)
                return;

            // Fire any due in-transit events first, so an event scheduled right at the ETA still fires
            // (and is broadcast to everyone aboard) before the arrival transition.
            await FireDueEventsAsync();

            if (_state.State.EtaUtc is DateTime eta && DateTime.UtcNow >= eta)
            {
                await ArriveAsync();
                return;
            }

            // En route: keep the interior alive so combat/exploration proceed, and nudge the HUD.
            if (!string.IsNullOrEmpty(_state.State.InteriorMapId))
                await _grainFactory.GetGrain<IGameMapGrain>(_state.State.InteriorMapId).TickAsync(TimeSpan.FromMinutes(1));
            await PushVoyageUpdateAsync(arrived: false);
        }

        /// <summary>Fires every scheduled in-transit event now due, broadcasting each to everyone aboard
        /// the interior (add-boardable-vehicles Phase 4). Idempotent — an event fires at most once.</summary>
        private async Task FireDueEventsAsync()
        {
            if (_state.State.ScheduledEvents.Count == 0)
                return;

            var now = DateTime.UtcNow;
            var due = _state.State.ScheduledEvents.Where(e => !e.Fired && e.DueUtc <= now).ToList();
            if (due.Count == 0)
                return;

            var sessionManager = SessionManager;
            foreach (var ev in due)
            {
                ev.Fired = true;
                if (sessionManager is null || _state.State.Passengers.Count == 0)
                    continue;

                var payload = new Dictionary<string, object?>
                {
                    ["vehicleId"] = this.GetPrimaryKeyString(),
                    ["eventType"] = ev.EventType,
                    ["description"] = ev.Description,
                };
                foreach (var passenger in _state.State.Passengers.ToList())
                    await sessionManager.NotifyPlayerEventAsync(passenger, "ReceiveVoyageEvent", payload);
            }

            await _state.WriteStateAsync();
        }

        private async Task ArriveAsync()
        {
            var cfg = _state.State.Config;
            if (cfg is null || string.IsNullOrEmpty(_state.State.DestMapId) || string.IsNullOrEmpty(_state.State.DestWorldId))
                return;

            var destMap = _grainFactory.GetGrain<IGameMapGrain>(_state.State.DestMapId);
            var placed = await destMap.PlaceVehicleExteriorAsync(
                this.GetPrimaryKeyString(), cfg.DisplayName,
                _state.State.DestAnchorX, _state.State.DestAnchorY, _state.State.DestAnchorZ,
                cfg.FootprintWidth, cfg.FootprintLength, cfg.FootprintDepth,
                cfg.ExteriorTerrain ?? string.Empty, cfg.LandingTerrain ?? new List<string>());

            if (!placed)
            {
                // Destination dock momentarily blocked — stay in transit and retry on the next wake.
                return;
            }

            _state.State.DockWorldId = _state.State.DestWorldId;
            _state.State.DockMapId = _state.State.DestMapId;
            _state.State.AnchorX = _state.State.DestAnchorX;
            _state.State.AnchorY = _state.State.DestAnchorY;
            _state.State.AnchorZ = _state.State.DestAnchorZ;
            _state.State.Landed = true;
            _state.State.InTransit = false;
            _state.State.DestWorldId = null;
            _state.State.DestMapId = null;
            _state.State.EtaUtc = null;
            _state.State.DepartedUtc = null;
            _state.State.ScheduledEvents.Clear(); // any un-fired events end with the voyage
            await _state.WriteStateAsync();

            await TryDisarmVoyageReminderAsync();
            await PushVoyageUpdateAsync(arrived: true);
        }

        public Task ReceiveReminder(string reminderName, TickStatus status)
            => reminderName == VoyageReminderName ? TickVoyageAsync() : Task.CompletedTask;

        private async Task TryArmVoyageReminderAsync()
        {
            try
            {
                // Orleans' minimum reminder period is 1 minute; each wake re-checks the ETA.
                await this.RegisterOrUpdateReminder(VoyageReminderName, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            }
            catch (Exception ex)
            {
                // No reminder service (e.g. a headless/test host without one): the voyage still advances
                // when TickVoyageAsync is driven externally; it just won't self-schedule.
                Console.WriteLine($"[VehicleGrain] voyage reminder unavailable: {ex.Message}");
            }
        }

        private async Task TryDisarmVoyageReminderAsync()
        {
            try
            {
                var reminder = await this.GetReminder(VoyageReminderName);
                if (reminder is not null)
                    await this.UnregisterReminder(reminder);
            }
            catch
            {
                // No reminder service or nothing registered — nothing to clean up.
            }
        }

        private async Task PushVoyageUpdateAsync(bool arrived)
        {
            var sessionManager = SessionManager;
            if (sessionManager is null || _state.State.Passengers.Count == 0)
                return;

            var payload = new Dictionary<string, object?>
            {
                ["vehicleId"] = this.GetPrimaryKeyString(),
                ["displayName"] = _state.State.Config?.DisplayName ?? string.Empty,
                ["inTransit"] = _state.State.InTransit,
                ["arrived"] = arrived,
                ["etaUtc"] = _state.State.EtaUtc,
                ["destMapId"] = _state.State.DestMapId ?? _state.State.DockMapId,
            };

            foreach (var passenger in _state.State.Passengers.ToList())
                await sessionManager.NotifyPlayerEventAsync(passenger, "ReceiveVoyageProgress", payload);
        }

        public Task<VehicleInfo> GetInfoAsync()
        {
            var s = _state.State;
            return Task.FromResult(new VehicleInfo
            {
                VehicleId = this.GetPrimaryKeyString(),
                DisplayName = s.Config?.DisplayName ?? string.Empty,
                InteriorWorldId = s.InteriorWorldId,
                InteriorMapId = s.InteriorMapId,
                Landed = s.Landed,
                InTransit = s.InTransit,
                DockWorldId = s.DockWorldId,
                DockMapId = s.DockMapId,
                AnchorX = s.AnchorX,
                AnchorY = s.AnchorY,
                AnchorZ = s.AnchorZ,
                PassengerCount = s.Passengers.Count,
                Capacity = s.Config?.Capacity ?? 0,
            });
        }

        public Task<string?> GetInteriorMapIdAsync() => Task.FromResult(_state.State.InteriorMapId);

        public Task<IReadOnlyList<string>> GetPassengersAsync() =>
            Task.FromResult<IReadOnlyList<string>>(_state.State.Passengers.ToList());
    }

    /// <summary>Persisted state for a <see cref="VehicleGrain"/>.</summary>
    [GenerateSerializer]
    public class VehicleState
    {
        [Id(0)] public VehicleConfig? Config { get; set; }
        [Id(1)] public string? InteriorWorldId { get; set; }
        [Id(2)] public string? InteriorMapId { get; set; }
        [Id(3)] public bool Landed { get; set; }
        [Id(4)] public bool InTransit { get; set; }
        [Id(5)] public string? DockWorldId { get; set; }
        [Id(6)] public string? DockMapId { get; set; }
        [Id(7)] public int AnchorX { get; set; }
        [Id(8)] public int AnchorY { get; set; }
        [Id(9)] public int AnchorZ { get; set; }
        [Id(10)] public HashSet<string> Passengers { get; set; } = new();

        // --- Voyage (Phase 3) ---
        [Id(11)] public string? DestWorldId { get; set; }
        [Id(12)] public string? DestMapId { get; set; }
        [Id(13)] public int DestAnchorX { get; set; }
        [Id(14)] public int DestAnchorY { get; set; }
        [Id(15)] public int DestAnchorZ { get; set; }
        [Id(16)] public DateTime? DepartedUtc { get; set; }
        [Id(17)] public DateTime? EtaUtc { get; set; }
        [Id(18)] public List<ScheduledVoyageEvent> ScheduledEvents { get; set; } = new();
    }

    /// <summary>A voyage's in-transit event stamped with an absolute due time and a fired flag
    /// (add-boardable-vehicles Phase 4).</summary>
    [GenerateSerializer]
    public class ScheduledVoyageEvent
    {
        [Id(0)] public DateTime DueUtc { get; set; }
        [Id(1)] public string EventType { get; set; } = string.Empty;
        [Id(2)] public string Description { get; set; } = string.Empty;
        [Id(3)] public bool Fired { get; set; }
    }
}
