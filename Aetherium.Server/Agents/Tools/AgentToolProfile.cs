using System;
using System.Collections.Generic;
using System.Linq;

namespace Aetherium.Server.Agents.Tools
{
    /// <summary>
    /// Defines which tools an agent has access to based on capabilities and permissions.
    /// </summary>
    public class AgentToolProfile
    {
        /// <summary>
        /// Name of this profile.
        /// </summary>
        public string ProfileName { get; set; } = "default";
        
        /// <summary>
        /// Explicitly allowed tool IDs.
        /// </summary>
        public HashSet<string> AllowedTools { get; set; } = new();
        
        /// <summary>
        /// Allowed tool categories (e.g., "movement", "inventory").
        /// </summary>
        public HashSet<string> AllowedCategories { get; set; } = new();
        
        /// <summary>
        /// Explicitly denied tool IDs (takes precedence over allowed).
        /// </summary>
        public HashSet<string> DeniedTools { get; set; } = new();
        
        /// <summary>
        /// Capabilities granted to agents with this profile.
        /// </summary>
        public HashSet<string> GrantedCapabilities { get; set; } = new();
        
        /// <summary>
        /// Checks if a tool is allowed for this profile.
        /// </summary>
        public bool IsToolAllowed(IAgentTool tool)
        {
            if (tool == null)
                return false;
            
            // Explicit deny takes precedence
            if (DeniedTools.Contains(tool.ToolId))
                return false;
            
            // Check if explicitly allowed
            if (AllowedTools.Contains(tool.ToolId))
                return true;
            
            // Check if any category is allowed
            if (tool.Categories.Any(c => AllowedCategories.Contains(c)))
                return true;
            
            // Check if all required capabilities are granted
            if (tool.RequiredCapabilities.All(cap => GrantedCapabilities.Contains(cap)))
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Predefined profile for basic exploration agents.
        /// </summary>
        public static AgentToolProfile Explorer => new()
        {
            ProfileName = "explorer",
            AllowedCategories = new() { "movement", "navigation", "perception" },
            GrantedCapabilities = new() { "basic_movement", "vision" }
        };
        
        /// <summary>
        /// Predefined profile with full player access.
        /// </summary>
        public static AgentToolProfile FullAccess => new()
        {
            ProfileName = "full",
            AllowedCategories = new() { "movement", "navigation", "inventory", "interaction", "perception", "vision" },
            GrantedCapabilities = new() { "basic_movement", "inventory_access", "vision", "interaction" }
        };
        
        /// <summary>
        /// Predefined profile for world-builder agents.
        /// </summary>
        public static AgentToolProfile WorldBuilder => new()
        {
            ProfileName = "worldbuilder",
            AllowedCategories = new() 
            { 
                "movement", "navigation", "worldbuilding", "entity_management", "terrain" 
            },
            GrantedCapabilities = new() 
            { 
                "basic_movement", "vision", "world_edit", "world_generate" 
            }
        };
        
        /// <summary>
        /// Predefined profile for narrative designer agents.
        /// </summary>
        public static AgentToolProfile NarrativeDesigner => new()
        {
            ProfileName = "narrativedesigner",
            AllowedCategories = new() 
            { 
                "movement", "navigation", "worldbuilding", "narrative", "quest" 
            },
            GrantedCapabilities = new() 
            { 
                "basic_movement", "vision", "narrative_edit", "world_edit" 
            }
        };
        
        /// <summary>
        /// Predefined profile for admin agents with unrestricted access.
        /// </summary>
        public static AgentToolProfile Admin => new()
        {
            ProfileName = "admin",
            AllowedCategories = new() 
            { 
                "movement", "navigation", "inventory", "interaction", "perception", 
                "vision", "worldbuilding", "entity_management", "terrain", "narrative", 
                "quest", "map_generation", "admin" 
            },
            GrantedCapabilities = new() 
            { 
                "basic_movement", "inventory_access", "vision", "interaction",
                "world_edit", "world_generate", "narrative_edit", "admin" 
            }
        };
        
        /// <summary>
        /// Predefined profile for human players.
        /// </summary>
        public static AgentToolProfile Player => new()
        {
            ProfileName = "player",
            AllowedCategories = new() { "movement", "navigation", "inventory", "interaction", "perception" },
            GrantedCapabilities = new() { "basic_movement", "inventory_access", "interaction", "vision" }
        };
        
        /// <summary>
        /// Gets a predefined profile by name.
        /// </summary>
        public static AgentToolProfile GetPredefinedProfile(string profileName)
        {
            return profileName?.ToLowerInvariant() switch
            {
                "explorer" => Explorer,
                "full" or "fullaccess" => FullAccess,
                "worldbuilder" => WorldBuilder,
                "narrativedesigner" => NarrativeDesigner,
                "admin" => Admin,
                "player" => Player,
                _ => Explorer // Default to explorer
            };
        }
    }
}

