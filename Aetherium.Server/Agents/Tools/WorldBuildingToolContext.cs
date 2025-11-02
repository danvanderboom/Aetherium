using System;
using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Server.Agents.Tools
{
    /// <summary>
    /// Specialized execution context for tools executed during world building.
    /// Provides direct World access without requiring game sessions or Orleans grains.
    /// </summary>
    public sealed class WorldBuildingToolContext : ToolExecutionContext
    {
        /// <summary>
        /// The World being built. Required for world building tool execution.
        /// </summary>
        public World World { get; init; } = null!;
        
        /// <summary>
        /// The current WorldFeature being built (optional).
        /// </summary>
        public WorldFeature? CurrentFeature { get; init; }
        
        /// <summary>
        /// Creates a new WorldBuildingToolContext with the specified World and optional feature.
        /// Automatically grants world_edit capability.
        /// </summary>
        public WorldBuildingToolContext(World world, IServiceProvider serviceProvider, WorldFeature? currentFeature = null)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            CurrentFeature = currentFeature;
            
            // Automatically grant world_edit capability for world building operations
            GrantedCapabilities = new HashSet<string> { "world_edit" };
        }
    }
}

