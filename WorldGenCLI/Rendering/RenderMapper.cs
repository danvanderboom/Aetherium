using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;
using Aetherium.WorldGen.Hybrid;
using WorldGenCLI.Models;

namespace WorldGenCLI.Rendering
{
    /// <summary>
    /// Maps World to MapRenderDto for preview visualization.
    /// </summary>
    public static class RenderMapper
    {
        /// <summary>
        /// Converts a World to MapRenderDto for visualization.
        /// </summary>
        public static MapRenderDto MapWorld(World world, int width, int height, int zLevel = 0, Aetherium.WorldGen.Hybrid.HybridLayout? hybridAnchors = null)
        {
            var dto = new MapRenderDto
            {
                Width = width,
                Height = height,
                Tiles = new byte[width * height],
                Rooms = new List<RoomOverlay>(),
                Corridors = new List<CorridorOverlay>(),
                Anchors = new List<AnchorOverlay>(),
                Regions = new List<RegionOverlay>(),
                Palette = new Dictionary<byte, TileInfo>()
            };

            // Create a mapping from tile type names to byte IDs
            var tileTypeToId = new Dictionary<string, byte>();
            byte nextId = 0;

            // Iterate through all locations in the world for the specified z-level
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var location = new WorldLocation(x, y, zLevel);
                    var index = y * width + x;

                    byte tileId = 0; // Default: empty/wall

                    if (world.EntitiesByLocation.TryGetValue(location, out var entitiesAtLocation))
                    {
                        var terrain = entitiesAtLocation.Values.OfType<Terrain>().FirstOrDefault();
                        if (terrain != null)
                        {
                            var tile = terrain.Get<Tile>();
                            if (tile != null && tile.Type != null && !tile.Type.IsNone)
                            {
                                var tileTypeName = tile.Type.Name;
                                
                                if (!tileTypeToId.TryGetValue(tileTypeName, out tileId))
                                {
                                    tileId = nextId++;
                                    tileTypeToId[tileTypeName] = tileId;

                                    // Add to palette
                                    dto.Palette[tileId] = new TileInfo
                                    {
                                        Name = tileTypeName,
                                        Symbol = GetSymbolForTileType(tileTypeName),
                                        Color = null // Could extract from tile settings if available
                                    };
                                }
                            }
                        }
                    }

                    dto.Tiles[index] = tileId;
                }
            }

            // Extract hybrid anchors if provided
            if (hybridAnchors != null)
            {
                foreach (var anchor in hybridAnchors.Anchors.Where(a => a.ZLevel == zLevel))
                {
                    var anchorOverlay = new AnchorOverlay
                    {
                        Anchor = ConvertAnchor(anchor),
                        Label = anchor.Tags.FirstOrDefault() ?? string.Empty
                    };
                    dto.Anchors.Add(anchorOverlay);
                }
            }

            return dto;
        }

        private static string GetSymbolForTileType(string tileTypeName)
        {
            // Map common tile type names to symbols
            return tileTypeName.ToLowerInvariant() switch
            {
                "wall" or "stone" => "#",
                "floor" or "ground" => ".",
                "door" => "+",
                "water" => "~",
                "grass" => ",",
                "dirt" => ":",
                "rock" => "^",
                "tree" => "T",
                "lava" => "!",
                _ => "?"
            };
        }

        private static Models.HybridAnchor ConvertAnchor(Aetherium.WorldGen.Hybrid.HybridAnchor anchor)
        {
            var dto = new Models.HybridAnchor
            {
                Type = (Models.AnchorType)anchor.Type,
                X = anchor.X,
                Y = anchor.Y,
                Width = anchor.Width,
                Height = anchor.Height,
                IsBlocking = anchor.IsBlocking,
                ZLevel = anchor.ZLevel,
                Priority = anchor.Priority
            };

            dto.Tags.AddRange(anchor.Tags);

            if (anchor.Vertices != null)
            {
                dto.Vertices = anchor.Vertices
                    .Select(v => new Models.HybridAnchor.Point { X = v.X, Y = v.Y })
                    .ToList();
            }

            return dto;
        }
    }
}

