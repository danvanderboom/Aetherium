using System;

namespace Aetherium.Input
{
    /// <summary>
    /// Maps real console keypresses to the <c>Aetherium.Model.Accessibility.ActionIntent</c> ids
    /// the seed catalog covers (engine gap-analysis §4.13, wire-accessibility-live). Only the keys
    /// with a real, already-shipped catalog entry are mapped — movement, pickup, drop, open/close.
    /// Debug/meta keys (rotation, level nav, vision presets, music, theme, teleport, quit) and
    /// combat (an implicit bump-attack on <c>move</c>, not a distinct keypress) fall outside the
    /// catalog and resolve to <c>null</c>; see wire-accessibility-live/design.md for why.
    /// </summary>
    public static class ConsoleActionIntentBinding
    {
        public static string? Resolve(ConsoleKey key) => key switch
        {
            ConsoleKey.UpArrow or ConsoleKey.W => "move",
            ConsoleKey.DownArrow or ConsoleKey.S => "move",
            ConsoleKey.LeftArrow or ConsoleKey.A => "move",
            ConsoleKey.RightArrow or ConsoleKey.D => "move",
            ConsoleKey.G => "pickup",
            ConsoleKey.P => "drop",
            ConsoleKey.O => "interact_open",
            ConsoleKey.L => "interact_close",
            _ => null,
        };
    }
}
