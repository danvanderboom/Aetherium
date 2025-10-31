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
            bool categoryAllowed = tool.Categories.Any(c => AllowedCategories.Contains(c));
            
            // Check if tool has required capabilities
            bool hasRequiredCapabilities = tool.RequiredCapabilities.Any();
            
            // If tool has no required capabilities, allow if any category matches
            if (!hasRequiredCapabilities)
            {
                return categoryAllowed;
            }
            
            // If tool has required capabilities, check if all are granted
            bool capabilitiesGranted = tool.RequiredCapabilities.All(cap => GrantedCapabilities.Contains(cap));
            
            // Security: Tools with required capabilities need BOTH category match AND capability grant
            // This prevents tools like JumpToLocationTool (requires "admin") from being allowed
            // to Explorer profile just because it shares a category (e.g., "movement")
            if (capabilitiesGranted && categoryAllowed)
            {
                return true;
            }
            
            // Also allow if all capabilities are granted (even without category match) for explicit capability-based access
            // This allows flexibility for tools that might be granted by capability alone
            return capabilitiesGranted;
        }
        
        /// <summary>
        /// Predefined profile for ALL game characters (human players and NPCs).
        /// This is the default profile for any character that exists within a game world.
        /// All game characters get the same set of tools unless explicitly granted special access.
        /// </summary>
        public static AgentToolProfile Player => new()
        {
            ProfileName = "player",
            AllowedCategories = new() { "movement", "navigation", "inventory", "interaction", "perception", "vision" },
            GrantedCapabilities = new() { "basic_movement", "inventory_access", "interaction", "vision" }
        };
        
        /// <summary>
        /// Predefined profile for basic exploration-only agents (limited capabilities).
        /// Used for simple test agents or agents that only need basic movement/vision.
        /// NOT for game NPCs - use Player profile for those.
        /// </summary>
        public static AgentToolProfile Explorer => new()
        {
            ProfileName = "explorer",
            AllowedCategories = new() { "movement", "navigation", "perception" },
            GrantedCapabilities = new() { "basic_movement", "vision" }
        };
        
        /// <summary>
        /// Predefined profile for world-builder agents (separate from game NPCs).
        /// These agents create and modify the game world itself, not play within it.
        /// Includes entity spawning, terrain modification, and map generation tools.
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
        /// Predefined profile for narrative designer agents (separate from game NPCs).
        /// These agents create quests and narrative content for the game world.
        /// Includes quest creation, narrative state management, and story token placement.
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
        /// Use sparingly - grants access to all tools including debug/admin capabilities.
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
        /// Legacy profile name for backward compatibility.
        /// Maps to Player profile (all game characters).
        /// </summary>
        [Obsolete("Use Player profile instead. Player profile is for all game characters (NPCs and human players).")]
        public static AgentToolProfile FullAccess => Player;
        
        /// <summary>
        /// Gets a predefined profile by name.
        /// Defaults to Player profile (for all game characters).
        /// </summary>
        public static AgentToolProfile GetPredefinedProfile(string profileName)
        {
            return profileName?.ToLowerInvariant() switch
            {
                "player" => Player,
                "full" or "fullaccess" => Player, // Legacy: maps to Player
                "explorer" => Explorer,
                "worldbuilder" => WorldBuilder,
                "narrativedesigner" => NarrativeDesigner,
                "admin" => Admin,
                _ => Player // Default to Player (for all game characters)
            };
        }
    }
}

