using System;
using System.Collections.Generic;

namespace Aetherium.Unity.Model
{
    /// <summary>
    /// Unity-friendly representation of TileTypeDto.
    /// </summary>
    [Serializable]
    public class TileTypeLite
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        public TileTypeLite()
        {
        }

        public TileTypeLite(string name, Dictionary<string, string>? settings)
        {
            Name = name;
            Settings = settings ?? new Dictionary<string, string>();
        }
    }
}

