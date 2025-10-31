using System;
using System.Collections.Generic;
using Aetherium.Model;
using Aetherium.Rendering.Themes;
using Aetherium.Rendering.Widgets;

namespace Aetherium.Rendering
{
    /// <summary>
    /// Complete UI state for a single frame render.
    /// This is presentation-agnostic and can be rendered by any IGameRenderer implementation.
    /// </summary>
    public class GameViewState
    {
        /// <summary>
        /// Core game perception data from the server
        /// </summary>
        public PerceptionDto? Perception { get; set; }

        /// <summary>
        /// Active UI widgets and their state
        /// </summary>
        public Dictionary<string, IWidget> Widgets { get; set; } = new Dictionary<string, IWidget>();

        /// <summary>
        /// Current visual theme configuration
        /// </summary>
        public ThemeConfig Theme { get; set; } = ThemeConfig.Default;

        /// <summary>
        /// Connection status
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Frame timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Status message to display (temporary notifications)
        /// </summary>
        public string? StatusMessage { get; set; }

        /// <summary>
        /// Whether to show debug information
        /// </summary>
        public bool ShowDebugInfo { get; set; }
    }
}


