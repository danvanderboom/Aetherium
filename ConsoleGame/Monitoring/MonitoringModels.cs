using System;
using System.Collections.Generic;
using ConsoleGameModel;

namespace ConsoleGame.Monitoring
{
    /// <summary>
    /// Configuration for the monitoring service
    /// </summary>
    public class MonitoringConfig
    {
        public bool Enabled { get; set; } = true;
        public int Port { get; set; } = 5001;
        public FileLoggingConfig FileLogging { get; set; } = new FileLoggingConfig();
    }

    /// <summary>
    /// Configuration for file-based logging
    /// </summary>
    public class FileLoggingConfig
    {
        public bool Enabled { get; set; } = false;
        public string OutputPath { get; set; } = "./monitoring-logs";
    }

    /// <summary>
    /// Represents a complete frame update containing both raw perception data and rendered ASCII map
    /// </summary>
    public class MapFrameUpdate
    {
        public DateTime Timestamp { get; set; }
        public long FrameNumber { get; set; }
        public PerceptionDto? RawPerception { get; set; }
        public AsciiMapData AsciiMap { get; set; }

        public MapFrameUpdate()
        {
            Timestamp = DateTime.UtcNow;
            AsciiMap = new AsciiMapData();
        }
    }

    /// <summary>
    /// Contains the rendered ASCII map as a 2D array of tiles
    /// </summary>
    public class AsciiMapData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public string[][] Tiles { get; set; }

        public AsciiMapData()
        {
            Tiles = Array.Empty<string[]>();
        }

        public AsciiMapData(int width, int height)
        {
            Width = width;
            Height = height;
            Tiles = new string[height][];
            for (int i = 0; i < height; i++)
            {
                Tiles[i] = new string[width];
            }
        }

        /// <summary>
        /// Converts the 2D tile array to a human-readable ASCII string
        /// </summary>
        public string ToAsciiString()
        {
            var lines = new List<string>();
            
            // Top border
            lines.Add("┌" + new string('─', Width * 2) + "┐");
            
            // Content rows
            for (int y = 0; y < Height; y++)
            {
                var row = "│";
                for (int x = 0; x < Width; x++)
                {
                    row += Tiles[y][x] ?? "  ";
                }
                row += "│";
                lines.Add(row);
            }
            
            // Bottom border
            lines.Add("└" + new string('─', Width * 2) + "┘");
            
            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Message sent to monitoring clients via WebSocket
    /// </summary>
    public class MonitoringMessage
    {
        public string Type { get; set; } = "frame";
        public MapFrameUpdate? Data { get; set; }
        public string? Error { get; set; }
    }
}

