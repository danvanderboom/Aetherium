using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools
{
    /// <summary>
    /// Defines an executable tool that agents can use to interact with the game world.
    /// </summary>
    public interface IAgentTool
    {
        /// <summary>
        /// Unique identifier for this tool (e.g., "move", "pickup").
        /// </summary>
        string ToolId { get; }
        
        /// <summary>
        /// Human-readable description for LLM context and documentation.
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Categories/tags for filtering (e.g., "movement", "inventory", "combat").
        /// </summary>
        IEnumerable<string> Categories { get; }
        
        /// <summary>
        /// Required capabilities to execute this tool.
        /// </summary>
        IEnumerable<string> RequiredCapabilities { get; }
        
        /// <summary>
        /// Gets the parameter schema for this tool.
        /// </summary>
        ToolParameterSchema GetParameterSchema();
        
        /// <summary>
        /// Executes the tool with the provided arguments.
        /// </summary>
        /// <param name="context">Execution context providing session, agent, and service access.</param>
        /// <param name="args">Tool arguments as key-value pairs.</param>
        /// <returns>Result of the tool execution.</returns>
        Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args);
    }
}

