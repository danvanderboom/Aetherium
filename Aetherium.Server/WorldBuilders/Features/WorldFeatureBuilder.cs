using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Aetherium.Core;
using Aetherium.Server.Agents.Tools;

namespace Aetherium.WorldBuilders
{
    public abstract class WorldFeatureBuilder
    {
        protected World World { get; set; }

        protected WorldFeature Feature { get; set; }

        protected AgentToolRegistry? ToolRegistry { get; set; }

        protected IServiceProvider? ServiceProvider { get; set; }

        public WorldFeatureBuilder(World world, WorldFeature feature)
        {
            World = world;
            Feature = feature;
        }

        public WorldFeatureBuilder(World world, WorldFeature feature, 
            AgentToolRegistry? toolRegistry = null, 
            IServiceProvider? serviceProvider = null)
            : this(world, feature)
        {
            ToolRegistry = toolRegistry;
            ServiceProvider = serviceProvider;
        }

        public abstract void Build(); // WorldBuilderOptions options = null);

        Random rand = new Random();

        // TODO: move to dedicated Randomizer class?
        protected int RandomSign() => rand.Next(0, 2) == 0 ? 1 : -1;

        /// <summary>
        /// Executes a tool during world building. Creates a WorldBuildingToolContext
        /// and executes the specified tool with the provided arguments.
        /// </summary>
        /// <param name="toolId">The tool ID to execute</param>
        /// <param name="args">Tool arguments as key-value pairs</param>
        /// <returns>True if the tool executed successfully, false otherwise</returns>
        protected async Task<bool> ExecuteToolAsync(string toolId, Dictionary<string, object> args)
        {
            if (ToolRegistry == null || ServiceProvider == null)
                return false;

            var tool = ToolRegistry.GetTool(toolId);
            if (tool == null)
                return false;

            var context = new WorldBuildingToolContext(World, ServiceProvider, Feature);
            var result = await tool.ExecuteAsync(context, args);
            return result.Success;
        }

        /// <summary>
        /// Synchronously executes a tool during world building.
        /// Blocks on async execution to keep Build() synchronous.
        /// </summary>
        /// <param name="toolId">The tool ID to execute</param>
        /// <param name="args">Tool arguments as key-value pairs</param>
        /// <returns>True if the tool executed successfully, false otherwise</returns>
        protected bool ExecuteTool(string toolId, Dictionary<string, object> args)
        {
            if (ToolRegistry == null || ServiceProvider == null)
                return false;

            return ExecuteToolAsync(toolId, args).GetAwaiter().GetResult();
        }
    }
}

