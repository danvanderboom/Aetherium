using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Persistence;
using Aetherium.Server.Simulation;
using Aetherium.Server.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain managing a 64×64 chunk region of a map.
    /// Owns dynamic state: entities, terrain modifications, heatmaps.
    /// </summary>
    public class MapRegionGrain : Grain, IMapRegionGrain
    {
        private readonly IPersistentState<RegionStateSnapshot> _regionState;
        private readonly IWorldSnapshotStore _snapshotStore;
        
        private string _mapId = string.Empty;
        private int _regionX;
        private int _regionY;
        private int _zLevel;
        private int _regionSize;

        public MapRegionGrain(
            [PersistentState("region", "mapStore")] IPersistentState<RegionStateSnapshot> regionState,
            IWorldSnapshotStore snapshotStore)
        {
            _regionState = regionState;
            _snapshotStore = snapshotStore;
        }

        private WorldClock? GetClock()
        {
            return this.ServiceProvider.GetService<WorldClock>();
        }

        private TemporalModifierRegistry? GetModifierRegistry()
        {
            return this.ServiceProvider.GetService<TemporalModifierRegistry>();
        }

        private WeatherSystem? GetWeatherSystem()
        {
            return this.ServiceProvider.GetService<WeatherSystem>();
        }

        private SeasonManager? GetSeasonManager()
        {
            return this.ServiceProvider.GetService<SeasonManager>();
        }

        private IEventScheduler? GetEventScheduler()
        {
            return this.ServiceProvider.GetService<IEventScheduler>();
        }

        private IGameMapGrain? GetMapGrain()
        {
            if (string.IsNullOrEmpty(_mapId))
                return null;

            var grainFactory = this.ServiceProvider.GetService<Orleans.IGrainFactory>();
            if (grainFactory == null)
                return null;

            return grainFactory.GetGrain<IGameMapGrain>(_mapId);
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            // Try to restore from persisted state
            if (_regionState.State != null && !string.IsNullOrEmpty(_regionState.State.RegionId))
            {
                _mapId = _regionState.State.MapId;
                _regionX = _regionState.State.RegionX;
                _regionY = _regionState.State.RegionY;
                _zLevel = _regionState.State.ZLevel;
                _regionSize = _regionState.State.RegionSize;
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public Task InitializeAsync(string mapId, int regionX, int regionY, int zLevel, int regionSize)
        {
            _mapId = mapId;
            _regionX = regionX;
            _regionY = regionY;
            _zLevel = zLevel;
            _regionSize = regionSize;

            var regionId = GetRegionId();
            
            _regionState.State = new RegionStateSnapshot
            {
                RegionId = regionId,
                MapId = mapId,
                RegionX = regionX,
                RegionY = regionY,
                ZLevel = zLevel,
                RegionSize = regionSize,
                SavedAt = DateTime.UtcNow,
                GameTimeHours = 0.0,
                TerrainModifications = new Dictionary<string, string>(),
                TraversalHeatmap = new Dictionary<string, int>(),
                BuiltStructures = new Dictionary<string, string>()
            };

            return _regionState.WriteStateAsync();
        }

        public async Task TickAsync(TimeSpan gameTimeElapsed)
        {
            if (_regionState.State == null)
                return;

            // Update game time in snapshot
            _regionState.State.GameTimeHours += gameTimeElapsed.TotalHours;

            // Get time context for temporal modifiers
            var clock = GetClock();
            double timeOfDay = 0.0;
            int day = 0;

            if (clock != null)
            {
                timeOfDay = clock.GetTimeOfDay();
                day = clock.GetDay();
            }

            // Update weather for this region
            var weatherSystem = GetWeatherSystem();
            var seasonManager = GetSeasonManager();
            if (weatherSystem != null && seasonManager != null)
            {
                var season = seasonManager.GetSeason(day);
                var regionId = GetRegionId();
                weatherSystem.UpdateWeather(regionId, timeOfDay, day, season);
                
                // Persist weather and season in snapshot
                _regionState.State.WeatherType = weatherSystem.GetWeather(regionId).ToString();
                _regionState.State.Season = season;
            }

            // Apply temporal modifiers
            var modifierRegistry = GetModifierRegistry();
            if (modifierRegistry != null)
            {
                var snapshot = await GetSnapshotAsync();
                await modifierRegistry.ApplyAllAsync(this, snapshot, gameTimeElapsed, timeOfDay, day);
            }
            
			// Process scheduled events for this region
			// Prefer service-based scheduler when available to avoid unnecessary grain activations in tests
			var serviceScheduler = GetEventScheduler();
			if (serviceScheduler != null && clock != null)
			{
				var currentGameTime = clock.GetTotalGameTimeHours();
				await serviceScheduler.ProcessScheduledEventsAsync(currentGameTime, day);
			}
			else
			{
				// Fall back to grain-based scheduler if service is not registered
				var mapGrain = GetMapGrain();
				if (mapGrain != null && clock != null)
				{
					var worldId = await mapGrain.GetWorldAsync();
					if (!string.IsNullOrEmpty(worldId))
					{
						var grainFactory = this.ServiceProvider.GetService<Orleans.IGrainFactory>();
						if (grainFactory != null)
						{
							var eventSchedulerGrain = grainFactory.GetGrain<IEventSchedulerGrain>(worldId);
							var currentGameTime = clock.GetTotalGameTimeHours();
							await eventSchedulerGrain.ProcessScheduledEventsAsync(currentGameTime, day);
						}
					}
				}
			}
            
            // Periodically persist state (not every tick to reduce writes)
            // Persist if significant time has elapsed since last save
            var lastSaveTime = _regionState.State.SavedAt;
            var shouldPersist = DateTime.UtcNow - lastSaveTime > TimeSpan.FromMinutes(5);
            if (shouldPersist)
            {
                _regionState.State.SavedAt = DateTime.UtcNow;
                await _regionState.WriteStateAsync();
            }
            else
            {
                // Still update SavedAt to track last activity, but don't persist
                _regionState.State.SavedAt = DateTime.UtcNow;
            }
        }

        public Task<RegionStateSnapshot> GetSnapshotAsync()
        {
            if (_regionState.State == null)
            {
                throw new InvalidOperationException("Region not initialized");
            }

            return Task.FromResult(_regionState.State);
        }

        public async Task LoadSnapshotAsync(RegionStateSnapshot snapshot)
        {
            _regionState.State = snapshot;
            _mapId = snapshot.MapId;
            _regionX = snapshot.RegionX;
            _regionY = snapshot.RegionY;
            _zLevel = snapshot.ZLevel;
            _regionSize = snapshot.RegionSize;
            
            await _regionState.WriteStateAsync();
        }

        public async Task ApplyDeltaAsync(RegionDelta delta)
        {
            if (_regionState.State == null)
                throw new InvalidOperationException("Region not initialized");

            // Apply delta to state
            switch (delta.Type)
            {
                case DeltaType.TerrainModified:
                    if (delta.Data.TryGetValue("location", out var loc) &&
                        delta.Data.TryGetValue("terrainType", out var terrainType))
                    {
                        _regionState.State.TerrainModifications[loc.ToString()!] = terrainType.ToString()!;
                    }
                    break;
                case DeltaType.TraversalRecorded:
                    if (delta.Data.TryGetValue("location", out var travLoc) &&
                        delta.Data.TryGetValue("count", out var count))
                    {
                        _regionState.State.TraversalHeatmap[travLoc.ToString()!] = Convert.ToInt32(count);
                    }
                    break;
                case DeltaType.StructureBuilt:
                    if (delta.Data.TryGetValue("location", out var structLoc) &&
                        delta.Data.TryGetValue("structureType", out var structType))
                    {
                        _regionState.State.BuiltStructures[structLoc.ToString()!] = structType.ToString()!;
                    }
                    break;
            }

            await _regionState.WriteStateAsync();
        }

        public async Task RecordTraversalAsync(int x, int y)
        {
            if (_regionState.State == null)
                return;

            var locationKey = $"{x},{y}";
            if (!_regionState.State.TraversalHeatmap.TryGetValue(locationKey, out var count))
            {
                count = 0;
            }
            _regionState.State.TraversalHeatmap[locationKey] = count + 1;

            // Create delta for persistence
            var delta = new RegionDelta
            {
                RegionId = GetRegionId(),
                Timestamp = DateTime.UtcNow,
                Type = DeltaType.TraversalRecorded,
                Data = new Dictionary<string, object>
                {
                    ["location"] = locationKey,
                    ["count"] = count + 1
                }
            };

            await ApplyDeltaAsync(delta);
        }

        public Task<Dictionary<(int x, int y), int>> GetTraversalHeatmapAsync()
        {
            if (_regionState.State == null)
                return Task.FromResult(new Dictionary<(int x, int y), int>());

            var heatmap = _regionState.State.TraversalHeatmap
                .ToDictionary(
                    kvp =>
                    {
                        var parts = kvp.Key.Split(',');
                        return (int.Parse(parts[0]), int.Parse(parts[1]));
                    },
                    kvp => kvp.Value
                );

            return Task.FromResult(heatmap);
        }

        private string GetRegionId()
        {
            var regionKey = this.GetPrimaryKeyString();
            return string.IsNullOrEmpty(regionKey) 
                ? $"{_mapId}:region:{_regionX},{_regionY},{_zLevel}" 
                : regionKey;
        }
    }
}

