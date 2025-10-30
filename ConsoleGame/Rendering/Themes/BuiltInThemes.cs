using System;
using System.Collections.Generic;

namespace ConsoleGame.Rendering.Themes
{
    /// <summary>
    /// Collection of built-in themes for the game
    /// </summary>
    public static class BuiltInThemes
    {
        /// <summary>
        /// Zen theme - Minimal, calm, meditative
        /// </summary>
        public static ThemeConfig Zen => new ThemeConfig
        {
            Name = "Zen",
            BackgroundColor = ConsoleColor.Black,
            ForegroundColor = ConsoleColor.Gray,
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
                ["warning"] = ConsoleColor.Yellow,
                ["widget_title"] = ConsoleColor.White,
                ["widget_text"] = ConsoleColor.Gray
            }
        };

        /// <summary>
        /// Cyberpunk theme - Neon colors, sharp edges, futuristic
        /// </summary>
        public static ThemeConfig Cyberpunk => new ThemeConfig
        {
            Name = "Cyberpunk",
            BackgroundColor = ConsoleColor.Black,
            ForegroundColor = ConsoleColor.Cyan,
            AccentColor = ConsoleColor.Magenta,
            BorderStyle = "Heavy",
            BorderColor = ConsoleColor.DarkCyan,
            Symbols = new Dictionary<string, string>
            {
                ["compass_n"] = "▲",
                ["compass_ne"] = "◥",
                ["compass_e"] = "▶",
                ["compass_se"] = "◢",
                ["compass_s"] = "▼",
                ["compass_sw"] = "◣",
                ["compass_w"] = "◀",
                ["compass_nw"] = "◤"
            },
            Palette = new Dictionary<string, ConsoleColor>
            {
                ["health"] = ConsoleColor.Cyan,
                ["danger"] = ConsoleColor.Magenta,
                ["info"] = ConsoleColor.Green,
                ["warning"] = ConsoleColor.Yellow,
                ["widget_title"] = ConsoleColor.Magenta,
                ["widget_text"] = ConsoleColor.Cyan
            }
        };

        /// <summary>
        /// Halloween theme - Spooky orange and black
        /// </summary>
        public static ThemeConfig Halloween => new ThemeConfig
        {
            Name = "Halloween",
            BackgroundColor = ConsoleColor.Black,
            ForegroundColor = ConsoleColor.DarkYellow,
            AccentColor = ConsoleColor.Red,
            BorderStyle = "Double",
            BorderColor = ConsoleColor.DarkRed,
            Symbols = new Dictionary<string, string>
            {
                ["compass_n"] = "↑",
                ["compass_ne"] = "↗",
                ["compass_e"] = "→",
                ["compass_se"] = "↘",
                ["compass_s"] = "↓",
                ["compass_sw"] = "↙",
                ["compass_w"] = "←",
                ["compass_nw"] = "↖",
                ["decoration"] = "🎃"
            },
            Palette = new Dictionary<string, ConsoleColor>
            {
                ["health"] = ConsoleColor.DarkGreen,
                ["danger"] = ConsoleColor.Red,
                ["info"] = ConsoleColor.DarkYellow,
                ["warning"] = ConsoleColor.Yellow,
                ["widget_title"] = ConsoleColor.DarkYellow,
                ["widget_text"] = ConsoleColor.Yellow
            }
        };

        /// <summary>
        /// Winter theme - Cool blues and whites
        /// </summary>
        public static ThemeConfig Winter => new ThemeConfig
        {
            Name = "Winter",
            BackgroundColor = ConsoleColor.Black,
            ForegroundColor = ConsoleColor.White,
            AccentColor = ConsoleColor.Blue,
            BorderStyle = "Rounded",
            BorderColor = ConsoleColor.DarkBlue,
            Symbols = new Dictionary<string, string>
            {
                ["compass_n"] = "⬆",
                ["compass_ne"] = "↗",
                ["compass_e"] = "➡",
                ["compass_se"] = "↘",
                ["compass_s"] = "⬇",
                ["compass_sw"] = "↙",
                ["compass_w"] = "⬅",
                ["compass_nw"] = "↖",
                ["decoration"] = "❄"
            },
            Palette = new Dictionary<string, ConsoleColor>
            {
                ["health"] = ConsoleColor.Cyan,
                ["danger"] = ConsoleColor.Blue,
                ["info"] = ConsoleColor.White,
                ["warning"] = ConsoleColor.Yellow,
                ["widget_title"] = ConsoleColor.White,
                ["widget_text"] = ConsoleColor.Cyan
            }
        };

        /// <summary>
        /// Classic theme - Traditional roguelike aesthetic
        /// </summary>
        public static ThemeConfig Classic => new ThemeConfig
        {
            Name = "Classic",
            BackgroundColor = ConsoleColor.Black,
            ForegroundColor = ConsoleColor.White,
            AccentColor = ConsoleColor.Yellow,
            BorderStyle = "Ascii",
            BorderColor = ConsoleColor.White,
            Symbols = new Dictionary<string, string>
            {
                ["compass_n"] = "^",
                ["compass_ne"] = "/",
                ["compass_e"] = ">",
                ["compass_se"] = "\\",
                ["compass_s"] = "v",
                ["compass_sw"] = "/",
                ["compass_w"] = "<",
                ["compass_nw"] = "\\"
            },
            Palette = new Dictionary<string, ConsoleColor>
            {
                ["health"] = ConsoleColor.Green,
                ["danger"] = ConsoleColor.Red,
                ["info"] = ConsoleColor.Yellow,
                ["warning"] = ConsoleColor.DarkYellow,
                ["widget_title"] = ConsoleColor.Yellow,
                ["widget_text"] = ConsoleColor.White
            }
        };

        /// <summary>
        /// Get all available themes
        /// </summary>
        public static Dictionary<string, ThemeConfig> GetAll()
        {
            return new Dictionary<string, ThemeConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["zen"] = Zen,
                ["cyberpunk"] = Cyberpunk,
                ["halloween"] = Halloween,
                ["winter"] = Winter,
                ["classic"] = Classic
            };
        }

        /// <summary>
        /// Get theme by name (case-insensitive)
        /// </summary>
        public static ThemeConfig GetByName(string name)
        {
            var themes = GetAll();
            return themes.TryGetValue(name, out var theme) ? theme : Zen;
        }
    }
}

