using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Aetherium.Model.Worlds;
using Aetherium.Server.MultiWorld;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Streams;

namespace Aetherium.Server.Services
{
    /// <summary>
    /// Orleans-based implementation of IWorldHost using virtual actors and streams.
    /// </summary>
    public class OrleansWorldHost : IWorldHost
    {
        private readonly IGrainFactory _grainFactory;
        private readonly IStreamProvider _streamProvider;
        private readonly ILogger<OrleansWorldHost> _logger;
        private readonly IWorldDirectoryGrain _directoryGrain;
        private const string StreamProviderName = "Default";

        public OrleansWorldHost(
            IGrainFactory grainFactory,
            IClusterClient clusterClient,
            ILogger<OrleansWorldHost> logger)
        {
            _grainFactory = grainFactory;
            _streamProvider = clusterClient.GetStreamProvider(StreamProviderName);
            _logger = logger;
            // Use a singleton directory grain
            _directoryGrain = grainFactory.GetGrain<IWorldDirectoryGrain>(Guid.Empty);
        }

        public async Task<WorldId> CreateWorldAsync(WorldTemplate template, WorldAcl acl, CancellationToken cancellationToken = default)
        {
            // Create a new world ID
            var worldId = new WorldId(Guid.NewGuid().ToString());

            // Get the world grain
            var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId.Value);

            // Convert template to WorldConfig
            var config = new WorldConfig
            {
                WorldId = worldId.Value,
                Name = template.Name,
                Description = template.Description,
                GeneratorType = template.GeneratorType,
                GeneratorParameters = template.GeneratorParameters,
                MaxPlayers = template.MaxPlayers,
                NarrativeId = template.NarrativeId,
                ClusterId = template.ClusterId,
                DeathPolicy = template.DeathPolicy,
                AbilityConfig = template.AbilityConfig,
                ProgressionConfig = template.ProgressionConfig,
                FactionConfig = template.FactionConfig,
                GameDefinitionId = template.GameDefinitionId,
                GameDefinitionVersion = template.GameDefinitionVersion,
                ContentConfig = template.ContentConfig,
                EcaConfig = template.EcaConfig,
                CreatedAt = System.DateTime.UtcNow,
                CreatedBy = "system" // TODO: Get from context
            };

            // WorldTemplate.Size arrived with add-game-definition-loader; before it, this path
            // silently fell back to WorldConfig's 100x100 default regardless of the request.
            if (template.Size is { } dims)
            {
                config.Size = new WorldSize { Width = dims.Width, Height = dims.Height, Depth = dims.Depth };
            }

            // Initialize the world
            await worldGrain.InitializeAsync(config);

            // Set ACL
            var aclGrain = _grainFactory.GetGrain<IWorldAclGrain>(worldId.Value);
            aclGrain.SetAclAsync(acl).Wait(cancellationToken);

            // Register in directory
            var worldInfo = await worldGrain.GetInfoAsync();
            if (worldInfo != null)
            {
                var summary = new WorldSummary
                {
                    WorldId = worldId,
                    Name = worldInfo.Name,
                    AccessLevel = acl.AccessLevel,
                    PlayerCount = worldInfo.PlayerCount,
                    MaxPlayers = worldInfo.MaxPlayers,
                    CreatedAt = worldInfo.CreatedAt,
                    LastActivityAt = worldInfo.LastActivityAt
                };

                await _directoryGrain.RegisterWorldAsync(worldId, summary);

                // Set as default if this is the first world
                var defaultWorld = await _directoryGrain.GetDefaultWorldAsync();
                if (defaultWorld == null)
                {
                    await _directoryGrain.SetDefaultWorldAsync(worldId);
                }
            }

            _logger.LogInformation("Created world {WorldId} with name {Name}", worldId.Value, template.Name);

            return worldId;
        }

        public async Task SetWorldAclAsync(WorldId worldId, WorldAcl acl, CancellationToken cancellationToken = default)
        {
            var aclGrain = _grainFactory.GetGrain<IWorldAclGrain>(worldId.Value);
            await aclGrain.SetAclAsync(acl);

            // Update summary in directory
            var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId.Value);
            var worldInfo = await worldGrain.GetInfoAsync();
            if (worldInfo != null)
            {
                var summary = new WorldSummary
                {
                    WorldId = worldId,
                    Name = worldInfo.Name,
                    AccessLevel = acl.AccessLevel,
                    PlayerCount = worldInfo.PlayerCount,
                    MaxPlayers = worldInfo.MaxPlayers,
                    CreatedAt = worldInfo.CreatedAt,
                    LastActivityAt = worldInfo.LastActivityAt
                };

                await _directoryGrain.RegisterWorldAsync(worldId, summary);
            }
        }

        public async Task<IReadOnlyList<WorldSummary>> ListWorldsAsync(WorldQuery query, CancellationToken cancellationToken = default)
        {
            return await _directoryGrain.ListWorldsAsync(query);
        }

        public async Task<InviteId> InviteAsync(WorldId worldId, PlayerId playerId, CancellationToken cancellationToken = default)
        {
            // Check if world exists and player can invite
            var aclGrain = _grainFactory.GetGrain<IWorldAclGrain>(worldId.Value);
            var canAccess = await aclGrain.CanAccessAsync(playerId); // TODO: Get from context

            if (!canAccess)
            {
                // Check if world is private and player is owner
                var acl = await aclGrain.GetAclAsync();
                if (acl.AccessLevel == WorldAccessLevel.Private && !acl.OwnerPlayers.Contains(playerId))
                {
                    throw new System.UnauthorizedAccessException($"Player {playerId.Value} cannot invite to world {worldId.Value}");
                }
            }

            var inviteGrain = _grainFactory.GetGrain<IWorldInviteGrain>(worldId.Value);
            var inviteId = await inviteGrain.CreateInviteAsync(
                playerId, // TODO: Get actual inviter from context
                playerId,
                System.TimeSpan.FromDays(7) // Default expiry: 7 days
            );

            return inviteId;
        }

        public async Task<bool> AcceptInviteAsync(InviteId inviteId, CancellationToken cancellationToken = default)
        {
            // Find the invite grain - we need to store a mapping or search
            // For now, we'll iterate through known worlds (inefficient, but works)
            // TODO: Store invite ID -> world ID mapping in a separate grain

            var query = new WorldQuery { MaxResults = 1000 };
            var worlds = await _directoryGrain.ListWorldsAsync(query);

            foreach (var world in worlds)
            {
                var inviteGrain = _grainFactory.GetGrain<IWorldInviteGrain>(world.WorldId.Value);
                var invite = await inviteGrain.GetInviteAsync(inviteId);
                if (invite != null)
                {
                    return await inviteGrain.AcceptInviteAsync(inviteId);
                }
            }

            return false;
        }

        public async IAsyncEnumerable<WorldEvent> SubscribeAsync(WorldId worldId, WorldStream stream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var streamNamespace = GetStreamNamespace(stream);
            // Create a stream key from worldId and stream type
            var streamKey = $"{worldId.Value}:{stream}";
            var streamRef = _streamProvider.GetStream<WorldEvent>(streamKey);
            
            // Use a queue to bridge Orleans observer pattern to async enumerable
            var queue = Channel.CreateUnbounded<WorldEvent>();
            var writer = queue.Writer;

            var subscriptionHandle = await streamRef.SubscribeAsync(async (evt, seq) =>
            {
                await writer.WriteAsync(evt, cancellationToken);
            });

            // Unsubscribe when cancellation is requested
            cancellationToken.Register(async () =>
            {
                await subscriptionHandle.UnsubscribeAsync();
                writer.Complete();
            });

            // Yield events from the queue
            await foreach (var evt in queue.Reader.ReadAllAsync(cancellationToken))
            {
                yield return evt;
            }
        }

        private static string GetStreamNamespace(WorldStream stream)
        {
            return stream switch
            {
                WorldStream.Events => "world-events",
                WorldStream.Chat => "world-chat",
                WorldStream.Zone => "world-zone",
                _ => "world-streams"
            };
        }
    }
}

