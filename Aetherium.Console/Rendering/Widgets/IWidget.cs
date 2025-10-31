using System;

namespace Aetherium.Rendering.Widgets
{
    /// <summary>
    /// Base interface for all UI widgets
    /// </summary>
    public interface IWidget
    {
        /// <summary>
        /// Unique identifier for this widget
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Whether this widget should be rendered
        /// </summary>
        bool IsVisible { get; set; }

        /// <summary>
        /// Display priority (higher = rendered later/on top)
        /// </summary>
        int ZOrder { get; set; }

        /// <summary>
        /// Get the renderable content for this widget
        /// </summary>
        object GetRenderData();
    }
}


