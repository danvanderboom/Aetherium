using System.Collections.Generic;

namespace Aetherium.Server.Agents.Tools
{
    /// <summary>
    /// Represents the result of a tool execution.
    /// </summary>
    public class ToolExecutionResult
    {
        /// <summary>
        /// Indicates whether the tool execution was successful.
        /// </summary>
        public bool Success { get; init; }
        
        /// <summary>
        /// Human-readable message describing the result or error.
        /// </summary>
        public string Message { get; init; } = string.Empty;
        
        /// <summary>
        /// Optional data payload returned by the tool.
        /// </summary>
        public Dictionary<string, object>? Data { get; init; }
        
        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static ToolExecutionResult Ok(string message = "")
        {
            return new ToolExecutionResult
            {
                Success = true,
                Message = message
            };
        }
        
        /// <summary>
        /// Creates a successful result with data.
        /// </summary>
        public static ToolExecutionResult Ok(string message, Dictionary<string, object> data)
        {
            return new ToolExecutionResult
            {
                Success = true,
                Message = message,
                Data = data
            };
        }
        
        /// <summary>
        /// Creates an error result.
        /// </summary>
        public static ToolExecutionResult Error(string message)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Message = message
            };
        }
    }
}

