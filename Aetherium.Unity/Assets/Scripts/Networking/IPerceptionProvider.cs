using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Unity.Model;

namespace Aetherium.Unity.Networking
{
    /// <summary>
    /// Interface for providing perception updates and tool execution to the game client.
    /// </summary>
    public interface IPerceptionProvider
    {
        /// <summary>
        /// Gets the current perception state.
        /// </summary>
        PerceptionLite? GetCurrent();

        /// <summary>
        /// Event fired when perception is updated.
        /// </summary>
        event Action<PerceptionLite>? PerceptionUpdated;

        /// <summary>
        /// Executes a tool against this provider. The mock provider mutates local state;
        /// the network provider forwards to the server.
        /// </summary>
        Task<ToolExecutionResultDto> ExecuteToolAsync(
            string toolId,
            Dictionary<string, object> args,
            CancellationToken cancellationToken);
    }
}
