using System;
using System.Collections.Generic;
using System.Linq;
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
    public class VehicleGrain : Grain, IVehicleGrain
    {
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
    }
}
