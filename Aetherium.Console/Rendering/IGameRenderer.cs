using System;
using System.Threading.Tasks;

namespace Aetherium.Rendering
{
    /// <summary>
    /// Abstraction for game rendering that can be implemented by different
    /// presentation technologies (Console, Spectre.Console, Unreal Engine, etc.)
    /// </summary>
    public interface IGameRenderer
    {
        /// <summary>
        /// Renders a complete frame based on the current game view state
        /// </summary>
        void RenderFrame(GameViewState state);

        /// <summary>
        /// Initialize the renderer (setup screen, load resources, etc.)
        /// </summary>
        void Initialize();

        /// <summary>
        /// Shutdown the renderer and cleanup resources
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Get the next input command from the user
        /// Returns null if no input is available (non-blocking)
        /// </summary>
        ConsoleKeyInfo? GetInputCommand();

        /// <summary>
        /// Wait for and return the next input command (blocking)
        /// </summary>
        Task<ConsoleKeyInfo> WaitForInputCommandAsync();

        /// <summary>
        /// Clear the entire rendering surface
        /// </summary>
        void Clear();
    }
}


