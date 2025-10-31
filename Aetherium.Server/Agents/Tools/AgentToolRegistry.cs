using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Aetherium.Server.Agents.Tools
{
    /// <summary>
    /// Registry for discovering and managing agent tools using reflection.
    /// Follows the pattern established by MapGeneratorRegistry.
    /// </summary>
    public class AgentToolRegistry
    {
        private readonly Dictionary<string, Type> _toolTypes = new();
        private readonly Dictionary<string, IAgentTool> _toolInstances = new();
        private readonly IServiceProvider _serviceProvider;
        
        public AgentToolRegistry(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }
        
        /// <summary>
        /// Discovers all types implementing IAgentTool in the provided assemblies.
        /// </summary>
        public void DiscoverTools(params Assembly[] assemblies)
        {
            if (assemblies.Length == 0)
            {
                assemblies = new[] { Assembly.GetExecutingAssembly() };
            }
            
            foreach (var assembly in assemblies)
            {
                var toolTypes = assembly.GetTypes()
                    .Where(t => typeof(IAgentTool).IsAssignableFrom(t) &&
                               !t.IsInterface &&
                               !t.IsAbstract)
                    .ToList();
                
                foreach (var type in toolTypes)
                {
                    var attr = type.GetCustomAttribute<AgentToolAttribute>();
                    var toolId = attr?.ToolId ?? GetTypeName(type);
                    
                    _toolTypes[toolId] = type;
                    Console.WriteLine($"[ToolRegistry] Discovered tool: {toolId} ({type.Name})");
                }
            }
        }
        
        /// <summary>
        /// Gets a tool instance by ID. Creates the instance if it doesn't exist.
        /// Supports constructor dependency injection.
        /// </summary>
        public IAgentTool? GetTool(string toolId)
        {
            if (string.IsNullOrEmpty(toolId))
                return null;
            
            // Return cached instance if available
            if (_toolInstances.TryGetValue(toolId, out var cached))
                return cached;
            
            // Create new instance using DI
            if (_toolTypes.TryGetValue(toolId, out var type))
            {
                try
                {
                    var tool = (IAgentTool)ActivatorUtilities.CreateInstance(_serviceProvider, type);
                    _toolInstances[toolId] = tool;
                    return tool;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ToolRegistry] Error creating tool {toolId}: {ex.Message}");
                    return null;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Lists all discovered tool IDs.
        /// </summary>
        public IEnumerable<string> ListTools()
        {
            return _toolTypes.Keys;
        }
        
        /// <summary>
        /// Gets all tools in a specific category.
        /// </summary>
        public IEnumerable<IAgentTool> GetToolsByCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
                return Enumerable.Empty<IAgentTool>();
            
            return _toolTypes.Keys
                .Select(id => GetTool(id))
                .Where(t => t?.Categories.Contains(category) == true)!;
        }
        
        /// <summary>
        /// Gets all tools that require a specific capability.
        /// </summary>
        public IEnumerable<IAgentTool> GetToolsByCapability(string capability)
        {
            if (string.IsNullOrEmpty(capability))
                return Enumerable.Empty<IAgentTool>();
            
            return _toolTypes.Keys
                .Select(id => GetTool(id))
                .Where(t => t?.RequiredCapabilities.Contains(capability) == true)!;
        }
        
        /// <summary>
        /// Gets all tools allowed for a specific profile.
        /// </summary>
        public IEnumerable<IAgentTool> GetToolsForProfile(AgentToolProfile profile)
        {
            if (profile == null)
                return Enumerable.Empty<IAgentTool>();
            
            return _toolTypes.Keys
                .Select(id => GetTool(id))
                .Where(t => t != null && profile.IsToolAllowed(t))!;
        }
        
        /// <summary>
        /// Checks if a tool is registered.
        /// </summary>
        public bool HasTool(string toolId)
        {
            return _toolTypes.ContainsKey(toolId);
        }
        
        /// <summary>
        /// Gets the type name for a tool type.
        /// </summary>
        private static string GetTypeName(Type type)
        {
            var name = type.Name;
            
            // Remove "Tool" suffix if present
            if (name.EndsWith("Tool", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            
            return name.ToLowerInvariant();
        }
    }
}

