using System;
using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Declares that an entity occupies more than one tile — the multi-tile footprint of a large
    /// object such as a boardable vehicle's exterior (add-boardable-vehicles Phase 1). The tiles are
    /// relative to the entity's anchor <see cref="WorldLocation"/>.
    ///
    /// <para>
    /// Two shapes are supported. The common case is a rectangular box (<see cref="Size"/>), anchored
    /// at the entity's <see cref="WorldLocation"/> as the minimum corner and extending +X (width),
    /// +Y (length), +Z (depth) — the same orientation <see cref="WorldChunk.AllLocations"/> uses to
    /// stamp terrain rectangles. For non-rectangular hulls, list explicit relative offsets in
    /// <see cref="Cells"/>, which override <see cref="Size"/> when non-empty.
    /// </para>
    ///
    /// <para>
    /// Entities without this component keep the engine's single-tile fast path — every footprint-aware
    /// branch in <see cref="World"/> is guarded by <c>Has&lt;Footprint&gt;()</c>, so nothing changes for
    /// ordinary characters, items, and terrain.
    /// </para>
    /// </summary>
    public class Footprint : Component
    {
        /// <summary>The box footprint anchored at the entity's <see cref="WorldLocation"/>. Width is the
        /// +X extent, Length the +Y extent, Depth the +Z extent. Defaults to a single 1×1×1 tile so a
        /// freshly-added Footprint behaves like no footprint until sized. Ignored when
        /// <see cref="Cells"/> is non-empty.</summary>
        public Size3d Size { get; set; } = new Size3d(1, 1, 1);

        /// <summary>Explicit relative tile offsets for non-rectangular footprints. When non-empty this
        /// overrides <see cref="Size"/>. Each offset is added to the anchor to yield an occupied tile;
        /// include (0,0,0) if the anchor tile itself is occupied.</summary>
        public IReadOnlyList<(int Dx, int Dy, int Dz)> Cells { get; set; } = Array.Empty<(int, int, int)>();

        public Footprint() : base() { }

        /// <summary>
        /// The absolute tiles this footprint occupies when anchored at <paramref name="anchor"/>. Uses
        /// <see cref="Cells"/> when non-empty, otherwise the <see cref="Size"/> box. Every tile is
        /// distinct for a well-formed footprint; callers that index into a set are unaffected by any
        /// accidental duplicate.
        /// </summary>
        public IEnumerable<WorldLocation> OccupiedTiles(WorldLocation anchor)
        {
            if (anchor is null)
                yield break;

            if (Cells is { Count: > 0 })
            {
                foreach (var (dx, dy, dz) in Cells)
                    yield return new WorldLocation(anchor.X + dx, anchor.Y + dy, anchor.Z + dz);
                yield break;
            }

            var size = Size ?? new Size3d(1, 1, 1);
            int width = Math.Max(1, size.Width);
            int length = Math.Max(1, size.Length);
            int depth = Math.Max(1, size.Depth);

            for (int z = 0; z < depth; z++)
                for (int y = 0; y < length; y++)
                    for (int x = 0; x < width; x++)
                        yield return new WorldLocation(anchor.X + x, anchor.Y + y, anchor.Z + z);
        }
    }
}
