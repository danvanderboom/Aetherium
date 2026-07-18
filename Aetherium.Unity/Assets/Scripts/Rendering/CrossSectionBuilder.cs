#nullable enable
using System.Collections.Generic;
using Aetherium.Unity.Model;
using UnityEngine;

namespace Aetherium.Unity.Rendering
{
    /// <summary>What occupies a cross-section cell. PerceptionLite carries no per-cell
    /// entity type, so (unlike the console) a cell is simply empty, occupied, or the player.</summary>
    public enum CrossSectionContent
    {
        Empty,
        Content,
        Player,
    }

    /// <summary>One cell of a cross-section row.</summary>
    public readonly struct CrossSectionCell
    {
        public readonly CrossSectionContent Content;
        public readonly string TileTypeId;

        public CrossSectionCell(CrossSectionContent content, string tileTypeId)
        {
            Content = content;
            TileTypeId = tileTypeId;
        }
    }

    /// <summary>One band of the elevation schematic; <see cref="Cells"/> spans dx ∈ [-HalfWidth, +HalfWidth].</summary>
    public sealed class CrossSectionRow
    {
        public int Band { get; }
        public bool IsFocus { get; }
        public CrossSectionCell[] Cells { get; }
        public int HalfWidth { get; }

        public CrossSectionRow(int band, bool isFocus, CrossSectionCell[] cells, int halfWidth)
        {
            Band = band;
            IsFocus = isFocus;
            Cells = cells;
            HalfWidth = halfWidth;
        }
    }

    /// <summary>
    /// Builds a side-on elevation of the bands around the player, sliced along the east-west axis at the
    /// player's row — the Unity analogue of the console's <c>ClientConsoleMapView.BuildCrossSection</c>. One
    /// row per occupied band (top band first; the focus band is always present and flagged), each row a strip
    /// of cells across <c>halfWidth</c> either side of the player. No per-tile FOV — a schematic projection of
    /// whatever the perception slab already contains. Pure and side-effect-free (unit tested without a scene).
    /// </summary>
    public static class CrossSectionBuilder
    {
        public static List<CrossSectionRow> Build(PerceptionLite perception, int halfWidth)
        {
            var rows = new List<CrossSectionRow>();
            if (perception == null || halfWidth < 0)
                return rows;

            int px = perception.PlayerLocation?.X ?? 0;
            int py = perception.PlayerLocation?.Y ?? 0;
            int focus = perception.PlayerLocation?.Z ?? 0;

            // Index visuals by absolute (x,y,z) and collect the bands that intersect the player's row strip.
            var byLoc = new Dictionary<(int, int, int), VisualLite>();
            var bands = new SortedSet<int>();
            foreach (var visual in perception.Visuals.Values)
            {
                var loc = visual.Location;
                byLoc[(loc.X, loc.Y, loc.Z)] = visual;
                if (loc.Y == py && Mathf.Abs(loc.X - px) <= halfWidth)
                    bands.Add(loc.Z);
            }
            bands.Add(focus); // the focus band is always part of the elevation

            // SortedSet is ascending; walk it in reverse so the highest band renders as the top row.
            var ordered = new List<int>(bands);
            ordered.Reverse();

            foreach (var z in ordered)
            {
                var cells = new CrossSectionCell[2 * halfWidth + 1];
                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                {
                    int index = dx + halfWidth;

                    if (dx == 0 && z == focus)
                    {
                        cells[index] = new CrossSectionCell(CrossSectionContent.Player, "Player");
                        continue;
                    }

                    if (byLoc.TryGetValue((px + dx, py, z), out var visual))
                        cells[index] = new CrossSectionCell(CrossSectionContent.Content, visual.TileTypeId);
                    else
                        cells[index] = new CrossSectionCell(CrossSectionContent.Empty, string.Empty);
                }

                rows.Add(new CrossSectionRow(z, z == focus, cells, halfWidth));
            }

            return rows;
        }
    }
}
