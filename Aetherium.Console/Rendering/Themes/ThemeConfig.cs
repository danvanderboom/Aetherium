using System;
using System.Collections.Generic;

namespace Aetherium.Rendering.Themes
{
    /// <summary>
    /// Configuration for visual theme including colors, borders, and symbols
    /// </summary>
    public class ThemeConfig
    {
        public string Name { get; set; } = "Default";
        
        /// <summary>
        /// Primary background color
        /// </summary>
        public ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;
        
        /// <summary>
        /// Primary foreground color
        /// </summary>
        public ConsoleColor ForegroundColor { get; set; } = ConsoleColor.White;
        
        /// <summary>
        /// Accent color for highlights and important elements
        /// </summary>
        public ConsoleColor AccentColor { get; set; } = ConsoleColor.Cyan;
        
        /// <summary>
        /// Border style name (for Spectre.Console: "Ascii", "Double", "Heavy", "Rounded", etc.)
        /// </summary>
        public string BorderStyle { get; set; } = "Rounded";
        
        /// <summary>
        /// Border color
        /// </summary>
        public ConsoleColor BorderColor { get; set; } = ConsoleColor.DarkGray;
        
        /// <summary>
        /// Theme-specific symbols for UI elements
        /// </summary>
        public Dictionary<string, string> Symbols { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Color palette for various UI elements
        /// </summary>
        public Dictionary<string, ConsoleColor> Palette { get; set; } = new Dictionary<string, ConsoleColor>();

        /// <summary>
        /// Default theme (Zen style)
        /// </summary>
        public static ThemeConfig Default => new ThemeConfig
        {
            Name = "Zen",
            BackgroundColor = ConsoleColor.Black,
            ForegroundColor = ConsoleColor.White,
            AccentColor = ConsoleColor.Cyan,
            BorderStyle = "Rounded",
            BorderColor = ConsoleColor.DarkGray,
            Symbols = new Dictionary<string, string>
            {
                ["compass_n"] = "↑",
                ["compass_ne"] = "↗",
                ["compass_e"] = "→",
                ["compass_se"] = "↘",
                ["compass_s"] = "↓",
                ["compass_sw"] = "↙",
                ["compass_w"] = "←",
                ["compass_nw"] = "↖"
            },
            Palette = new Dictionary<string, ConsoleColor>
            {
                ["health"] = ConsoleColor.Green,
                ["danger"] = ConsoleColor.Red,
                ["info"] = ConsoleColor.Cyan,
                ["warning"] = ConsoleColor.Yellow
            }
        };

        /// <summary>
        /// Get a symbol by key, with fallback
        /// </summary>
        public string GetSymbol(string key, string fallback = "?")
        {
            return Symbols.TryGetValue(key, out var symbol) ? symbol : fallback;
        }

        /// <summary>
        /// Get a color by key, with fallback
        /// </summary>
        public ConsoleColor GetColor(string key, ConsoleColor fallback = ConsoleColor.White)
        {
            return Palette.TryGetValue(key, out var color) ? color : fallback;
        }
    }
}


