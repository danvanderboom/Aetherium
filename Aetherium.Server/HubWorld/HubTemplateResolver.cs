using System;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Server.HubWorld
{
    /// <summary>
    /// Resolves hub templates in WorldConfig generation.
    /// </summary>
    public class HubTemplateResolver
    {
        private readonly HubWorldGenerator _generator;

        /// <summary>
        /// Creates a new hub template resolver.
        /// </summary>
        public HubTemplateResolver(HubWorldGenerator generator)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        /// <summary>
        /// Checks if a generator type is a hub template and resolves it to a WorldConfig.
        /// </summary>
        /// <param name="generatorType">The generator type to check (e.g., "hub:central-hub" or "central-hub")</param>
        /// <param name="request">The original world creation request</param>
        /// <param name="clusterId">Optional cluster ID</param>
        /// <returns>Resolved WorldConfig if it's a hub, null otherwise</returns>
        public async Task<WorldConfig?> TryResolveHubAsync(string generatorType, CreateWorldRequest? request = null, string? clusterId = null)
        {
            if (string.IsNullOrEmpty(generatorType))
                return null;

            // Check if generator type is a hub template (starts with "hub:" or matches a hub ID)
            string? hubId = null;
            
            if (generatorType.StartsWith("hub:", StringComparison.OrdinalIgnoreCase))
            {
                hubId = generatorType.Substring(4); // Remove "hub:" prefix
            }
            else
            {
                // Try to resolve as hub ID directly
                hubId = await _generator.ResolveHubIdAsync(generatorType);
            }

            if (string.IsNullOrEmpty(hubId))
                return null;

            // Generate WorldConfig from hub definition
            var hubConfig = await _generator.GenerateWorldConfigAsync(hubId, clusterId);
            
            if (hubConfig == null)
                return null;

            // Override with request parameters if provided
            if (request != null)
            {
                if (!string.IsNullOrEmpty(request.Name))
                    hubConfig.Name = request.Name;
                if (!string.IsNullOrEmpty(request.Description))
                    hubConfig.Description = request.Description;
                if (!string.IsNullOrEmpty(request.NarrativeId))
                    hubConfig.NarrativeId = request.NarrativeId;
                if (request.Size != null)
                    hubConfig.Size = request.Size;
                if (request.MaxPlayers > 0)
                    hubConfig.MaxPlayers = request.MaxPlayers;
                
                // Merge generator parameters
                if (request.GeneratorParameters != null)
                {
                    foreach (var kvp in request.GeneratorParameters)
                    {
                        hubConfig.GeneratorParameters[kvp.Key] = kvp.Value;
                    }
                }
            }

            return hubConfig;
        }
    }
}

