using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Aetherium.WorldGen;

namespace Aetherium.Server.HubWorld
{
    /// <summary>
    /// Generates hub worlds from HubDefinition templates.
    /// </summary>
    public class HubWorldGenerator
    {
        private readonly HubWorldLoader _loader;

        /// <summary>
        /// Creates a new hub world generator.
        /// </summary>
        public HubWorldGenerator(HubWorldLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        /// <summary>
        /// Generates a WorldConfig from a hub definition.
        /// </summary>
        public async Task<WorldConfig?> GenerateWorldConfigAsync(string hubId, string? clusterId = null)
        {
            var hub = await _loader.GetHubAsync(hubId);
            if (hub == null)
                return null;

            return GenerateWorldConfigFromHub(hub, clusterId);
        }

        /// <summary>
        /// Generates a WorldConfig from a hub definition instance.
        /// </summary>
        public WorldConfig GenerateWorldConfigFromHub(HubDefinition hub, string? clusterId = null)
        {
            var config = new WorldConfig
            {
                WorldId = $"hub-{hub.HubId}-{Guid.NewGuid()}",
                Name = hub.Name,
                Description = hub.Description,
                GeneratorType = hub.GeneratorType,
                GeneratorParameters = new Dictionary<string, object>(hub.GeneratorParameters),
                Size = new WorldSize
                {
                    Width = hub.Size.Width,
                    Height = hub.Size.Height,
                    Depth = hub.Size.Depth
                },
                MaxPlayers = 100,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "hub-generator",
                ClusterId = clusterId
            };

            // Set narrative ID if provided
            if (!string.IsNullOrEmpty(hub.NarrativeId))
            {
                config.NarrativeId = hub.NarrativeId;
            }

            // Store hub metadata
            config.GeneratorParameters["hubId"] = hub.HubId;
            config.GeneratorParameters["isHub"] = true;
            
            if (hub.Tags != null && hub.Tags.Count > 0)
            {
                config.GeneratorParameters["tags"] = string.Join(",", hub.Tags);
            }

            // Store portal definitions for later use during generation
            if (hub.Portals != null && hub.Portals.Count > 0)
            {
                config.GeneratorParameters["portalDefinitions"] = SerializePortals(hub.Portals);
            }

            return config;
        }

        /// <summary>
        /// Resolves hub template name to hub ID.
        /// </summary>
        public async Task<string?> ResolveHubIdAsync(string templateName)
        {
            if (string.IsNullOrEmpty(templateName))
                return null;

            // Try exact match first
            if (await _loader.GetHubAsync(templateName) != null)
                return templateName;

            // Try matching by name
            var allHubs = await _loader.GetAllHubsAsync();
            var matched = allHubs.FirstOrDefault(h => 
                h.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase) ||
                h.HubId.Equals(templateName, StringComparison.OrdinalIgnoreCase));

            return matched?.HubId;
        }

        /// <summary>
        /// Gets all available hub IDs.
        /// </summary>
        public async Task<List<string>> GetAvailableHubIdsAsync()
        {
            var hubs = await _loader.GetAllHubsAsync();
            return hubs.Select(h => h.HubId).ToList();
        }

        /// <summary>
        /// Serializes portal definitions to JSON for storage in generator parameters.
        /// JSON property names match the keys expected by PortalNetworkPass.PortalDefinitionDto.
        /// </summary>
        private static string SerializePortals(List<PortalDefinition> portals)
        {
            var dtos = portals.Select(p => new
            {
                id = p.PortalId,
                worldTag = p.TargetWorldTag,
                worldTemplate = p.TargetWorldTemplate,
                mapTag = p.TargetMapTag,
                mapName = p.TargetMapName,
                activation = p.Activation
            }).ToList();

            return JsonSerializer.Serialize(dtos);
        }
    }
}

