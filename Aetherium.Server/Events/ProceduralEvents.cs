using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.Simulation;
using Aetherium.Server.MultiWorld;
using Orleans;

namespace Aetherium.Server.Events
{
    /// <summary>
    /// Handler for merchant caravan events.
    /// </summary>
    public class MerchantCaravanHandler : IEventHandler
    {
        private readonly IGrainFactory? _grainFactory;

        public MerchantCaravanHandler()
        {
            _grainFactory = null;
        }

        public MerchantCaravanHandler(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        public async Task HandleEventAsync(ScheduledEvent scheduledEvent, double currentGameTime, int day)
        {
            if (_grainFactory == null)
                return;
            // Resolve event instance id
            if (!scheduledEvent.EventData.TryGetValue("eventInstanceId", out var instanceIdObj))
                return;

            var eventInstanceId = instanceIdObj?.ToString();
            if (string.IsNullOrEmpty(eventInstanceId))
                return;

            // Get event instance info (map/location)
            var eventInstanceGrain = _grainFactory.GetGrain<IEventInstanceGrain>(eventInstanceId);
            var eventInfo = await eventInstanceGrain.GetInfoAsync();
            if (eventInfo == null || string.IsNullOrEmpty(eventInfo.MapId))
                return;

            var x = eventInfo.X ?? 0;
            var y = eventInfo.Y ?? 0;
            var z = eventInfo.Z ?? 0;
            var mapId = eventInfo.MapId;

            // Determine spawn parameters
            var spawnType = scheduledEvent.EventData.TryGetValue("spawnType", out var typeObj)
                ? typeObj?.ToString() ?? "merchant"
                : "merchant";

            var spawnCount = scheduledEvent.EventData.TryGetValue("spawnCount", out var countObj)
                ? Convert.ToInt32(countObj)
                : 3;

            // Spawn entities via SpawnControllerGrain
            var spawnController = _grainFactory.GetGrain<ISpawnControllerGrain>(eventInstanceId);
            var spawnConfig = new Dictionary<string, object>
            {
                { "spawnType", spawnType },
                { "eventType", scheduledEvent.EventType }
            };

            var spawnResult = await spawnController.SpawnEntitiesAsync(
                scheduledEvent.EventType,
                spawnConfig,
                mapId!,
                x, y, z,
                spawnCount
            );

            if (spawnResult.Success && spawnResult.EntityIds.Count > 0)
            {
                var broadcastData = new Dictionary<string, object>
                {
                    { "eventType", "merchant_caravan_spawned" },
                    { "entityCount", spawnResult.EntityIds.Count },
                    { "spawnType", spawnType },
                    { "location", new { x, y, z } }
                };

                await eventInstanceGrain.BroadcastToAreaAsync("spawn", broadcastData);
            }
        }
    }

    /// <summary>
    /// Handler for monster invasion events.
    /// </summary>
    public class MonsterInvasionHandler : IEventHandler
    {
        private readonly IGrainFactory? _grainFactory;
        private readonly SpawnManager? _spawnManager;

        public MonsterInvasionHandler()
        {
            _grainFactory = null;
            _spawnManager = null;
        }

        public MonsterInvasionHandler(SpawnManager? spawnManager)
        {
            _grainFactory = null;
            _spawnManager = spawnManager;
        }

        public MonsterInvasionHandler(IGrainFactory grainFactory, SpawnManager? spawnManager = null)
        {
            _grainFactory = grainFactory;
            _spawnManager = spawnManager;
        }

        public async Task HandleEventAsync(ScheduledEvent scheduledEvent, double currentGameTime, int day)
        {
            if (_grainFactory == null)
                return;
            // Resolve event instance id
            if (!scheduledEvent.EventData.TryGetValue("eventInstanceId", out var instanceIdObj))
                return;

            var eventInstanceId = instanceIdObj?.ToString();
            if (string.IsNullOrEmpty(eventInstanceId))
                return;

            // Get event instance to retrieve map/location
            var eventInstanceGrain = _grainFactory.GetGrain<IEventInstanceGrain>(eventInstanceId);
            var eventInfo = await eventInstanceGrain.GetInfoAsync();
            if (eventInfo == null || string.IsNullOrEmpty(eventInfo.MapId))
                return;

            // Get spawn type from event data or use default
            var spawnType = scheduledEvent.EventData.TryGetValue("spawnType", out var typeObj)
                ? typeObj?.ToString() ?? "Monster"
                : "Monster";

            var spawnCount = scheduledEvent.EventData.TryGetValue("spawnCount", out var countObj)
                ? Convert.ToInt32(countObj)
                : 5;

            var x = eventInfo.X ?? 0;
            var y = eventInfo.Y ?? 0;
            var z = eventInfo.Z ?? 0;
            var mapId = eventInfo.MapId!;

            // Prepare spawn config with optional SpawnManager integration
            var spawnConfig = new Dictionary<string, object>
            {
                { "spawnType", spawnType },
                { "spawnCount", spawnCount },
                { "eventType", scheduledEvent.EventType }
            };

            if (_spawnManager != null && !string.IsNullOrEmpty(eventInfo.RegionId))
            {
                var timeOfDay = currentGameTime % 24.0;
                var spawnRate = _spawnManager.GetSpawnRate(spawnType, eventInfo.RegionId, timeOfDay, day);
                spawnConfig["spawnRate"] = spawnRate;
            }

            var spawnController = _grainFactory.GetGrain<ISpawnControllerGrain>(eventInstanceId);
            var spawnResult = await spawnController.SpawnEntitiesAsync(
                scheduledEvent.EventType,
                spawnConfig,
                mapId,
                x, y, z,
                spawnCount
            );

            if (spawnResult.Success && spawnResult.EntityIds.Count > 0)
            {
                // Broadcast spawn event to players in AOI
                var broadcastData = new Dictionary<string, object>
                {
                    { "eventType", "monster_invasion_spawned" },
                    { "entityCount", spawnResult.EntityIds.Count },
                    { "spawnType", spawnType },
                    { "location", new { x, y, z } }
                };

                await eventInstanceGrain.BroadcastToAreaAsync("spawn", broadcastData);
            }
        }
    }
}

