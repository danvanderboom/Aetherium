using System;
using System.Linq;
using System.Collections.Generic;

namespace Aetherium.Core
{
    public class TileType
    {
        public string Name { get; set; } = string.Empty;

        public List<Component> DefaultComponents { get; set; }

        public Dictionary<string, string> Settings { get; set; }

        public bool IsNone { get; protected set; } = false;

        static TileType _None = new TileType { IsNone = true };
        public static TileType None => _None;

        public TileType()
        {
            DefaultComponents = new List<Component>();
            Settings = new Dictionary<string, string>();
        }

        public override string ToString() =>
            $"{Name}: {string.Join(", ", Settings.Select(s => $"{s.Key} = '{s.Value}'"))}";
    }
}

