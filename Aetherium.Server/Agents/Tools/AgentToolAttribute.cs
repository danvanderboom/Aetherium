using System;

namespace Aetherium.Server.Agents.Tools
{
    /// <summary>
    /// Marks a class as a discoverable agent tool.
    /// Used by the tool registry for reflection-based discovery.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class AgentToolAttribute : Attribute
    {
        /// <summary>
        /// Unique identifier for the tool.
        /// </summary>
        public string ToolId { get; }
        
        /// <summary>
        /// Human-readable description of what the tool does.
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// Optional categories for the tool (e.g., "movement", "inventory").
        /// </summary>
        public string[] Categories { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Optional required capabilities for the tool.
        /// </summary>
        public string[] RequiredCapabilities { get; set; } = Array.Empty<string>();
        
        public AgentToolAttribute(string toolId, string description)
        {
            ToolId = toolId ?? throw new ArgumentNullException(nameof(toolId));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }
    }
}

