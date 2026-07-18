using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.WorldBuilders
{
    /// <summary>
    /// The terrain palette for the open-world <c>overworld</c> sample: a curated vocabulary
    /// kept separate from the diagnostic <see cref="TestMazeWorldBuilder"/> so the sandbox can
    /// carry richer terrain (desert, window walls) without perturbing the maze/arena worlds.
    ///
    /// <para>Unlike the maze builder, every terrain here sets <see cref="TerrainType.IsPassable"/>
    /// explicitly rather than leaning on the legacy name-based fallback in
    /// <c>World.PassableTerrain</c> — new names (Desert, WindowWall) aren't in that switch, so
    /// they would otherwise read as impassable by default.</para>
    ///
    /// <para>The two movement/sight axes are independent (see docs): a cell blocks passage via
    /// <c>IsPassable=false</c>; it blocks sight AND light via an <see cref="ObstructsView"/>
    /// component. That decoupling is what makes <b>WindowWall</b> possible — impassable, yet
    /// fully transparent to sight and light (the same trick <c>Water</c> uses).</para>
    ///
    /// <para>This is a plain palette provider (not a <c>WorldBuilder</c> subclass): generators
    /// pull <see cref="TileTypes"/> + <see cref="CreateTerrainTypes"/> into a fresh
    /// <see cref="World"/>, exactly as <see cref="Generators.ArenaGenerator"/> does with the
    /// maze builder.</para>
    /// </summary>
    public sealed class OverworldWorldBuilder
    {
        /// <summary>Terrain a character can walk onto. Everything not listed is impassable.</summary>
        private static readonly HashSet<string> Passable = new(StringComparer.Ordinal)
        {
            "Plains", "Forest", "Desert", "Hills", "Road", "Indoors", "Rail", "Subway",
        };

        public List<TileType> TileTypes => new()
        {
            // --- Wilderness ---
            new TileType
            {
                Name = "Plains",
                Settings = Render(".", ConsoleColor.DarkGreen, ConsoleColor.Green),
            },
            new TileType
            {
                Name = "Forest",
                // Semi-opaque: sight/light bleed through a little (cumulative — a thicket blocks).
                DefaultComponents = new List<Component> { new ObstructsView { Opacity = 0.5 } },
                Settings = Render("t", ConsoleColor.Black, ConsoleColor.Green),
            },
            new TileType
            {
                Name = "Desert",
                Settings = Render(".", ConsoleColor.DarkYellow, ConsoleColor.Yellow),
            },
            new TileType
            {
                Name = "Hills",
                Settings = Render("n", ConsoleColor.DarkGreen, ConsoleColor.DarkYellow),
            },
            new TileType
            {
                Name = "Mountain",
                // Impassable barrier that also blocks sight/light.
                DefaultComponents = new List<Component> { new ObstructsView { Opacity = 1 } },
                Settings = Render("^", ConsoleColor.DarkGray, ConsoleColor.White),
            },
            new TileType
            {
                Name = "Water",
                // Blocks movement, transparent to sight/light (rivers you see across but can't wade).
                DefaultComponents = new List<Component> { new ObstructsMovement() },
                Settings = Render("~", ConsoleColor.Blue, ConsoleColor.White),
            },
            new TileType
            {
                Name = "Road",
                Settings = Render("=", ConsoleColor.Black, ConsoleColor.White),
            },

            // --- Transit (docs/design/h3-sphere-worldgen.md P4) ---
            new TileType
            {
                // Surface rail — the high-capacity inter-city freight backbone.
                Name = "Rail",
                Settings = Render("+", ConsoleColor.Black, ConsoleColor.Gray),
            },
            new TileType
            {
                // Underground subway (rides a negative band); perceived from the surface through the slab.
                Name = "Subway",
                Settings = Render("=", ConsoleColor.DarkGray, ConsoleColor.Cyan),
            },

            // --- Structures ---
            new TileType
            {
                Name = "Indoors",
                Settings = Render(" ", ConsoleColor.Gray, ConsoleColor.Black),
            },
            new TileType
            {
                Name = "Wall",
                DefaultComponents = new List<Component> { new ObstructsView { Opacity = 1 } },
                Settings = Render("#", ConsoleColor.Gray, ConsoleColor.DarkRed),
            },
            new TileType
            {
                Name = "WindowWall",
                // The whole point: blocks passage (via IsPassable=false + ObstructsMovement),
                // but carries NO ObstructsView, so sight and light pass straight through.
                DefaultComponents = new List<Component> { new ObstructsMovement() },
                Settings = Render("o", ConsoleColor.Gray, ConsoleColor.Cyan),
            },
        };

        /// <summary>Project the tile list into terrain types, stamping each with an explicit
        /// passability flag so movement never depends on the legacy name switch.</summary>
        public List<TerrainType> CreateTerrainTypes(IList<TileType> tileTypes) =>
            tileTypes
                .Select(t => new TerrainType
                {
                    Name = t.Name,
                    TileType = t,
                    Settings = t.Settings,
                    IsPassable = Passable.Contains(t.Name),
                })
                .ToList();

        private static Dictionary<string, string> Render(string glyph, ConsoleColor bg, ConsoleColor fg) =>
            new()
            {
                { "MapCharacter", glyph },
                { "BackgroundColor", bg.ToString() },
                { "ForegroundColor", fg.ToString() },
            };
    }
}
