using System;
using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Component that marks an entity as having inscribed text (books, tablets, etc.).
    /// </summary>
    public class Inscription : Component
    {
        /// <summary>
        /// The text content of the inscription.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Topic category for lore organization.
        /// </summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>
        /// Title of the inscription.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Author or origin (e.g., "Ancient Dwarven Text", "Traveler's Journal").
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Historical era or time period this relates to.
        /// </summary>
        public string Era { get; set; } = string.Empty;

        /// <summary>
        /// Whether this inscription has been read by the player.
        /// </summary>
        public bool IsRead { get; set; } = false;
    }
}

