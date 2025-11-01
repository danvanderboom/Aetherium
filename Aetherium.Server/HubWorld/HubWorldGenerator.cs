using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Serializes portal definitions to a string for storage in generator parameters.
        /// </summary>
        private string SerializePortals(List<PortalDefinition> portals)
        {
            // Simple serialization: store as JSON-like string
            // In a full implementation, you'd use proper JSON serialization
            var parts = new List<string>();
            foreach (var portal in portals)
            {
                var portalData = new List<string>();
                if (!string.IsNullOrEmpty(portal.PortalId))
                    portalData.Add($"id:{portal.PortalId}");
                if (!string.IsNullOrEmpty(portal.TargetWorldTag))
                    portalData.Add($"worldTag:{portal.TargetWorldTag}");
                if (!string.IsNullOrEmpty(portal.TargetWorldTemplate))
                    portalData.Add($"worldTemplate:{portal.TargetWorldTemplate}");
                if (!string.IsNullOrEmpty(portal.TargetMapTag))
                    portalData.Add($"mapTag:{portal.TargetMapTag}");
                if (!string.IsNullOrEmpty(portal.TargetMapName))
                    portalData.Add($"mapName:{portal.TargetMapName}");
                if (!string.IsNullOrEmpty(portal.Activation))
                    portalData.Add($"activation:{portal.Activation}");
                
                parts.Add(string.Join("|", portalData));
            }
            
            return string.Join(";", parts);
        }
    }
}

