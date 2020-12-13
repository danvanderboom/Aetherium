using System.Collections.Generic;
using System.Linq;
using System;

namespace ConsoleGame.Core
{
    public class TileType
    {
        public string Name { get; set; }

        public List<Component> DefaultComponents { get; set; }

        public Dictionary<string, string> Settings { get; set; }

        public TileType()
        {
            DefaultComponents = new List<Component>();
            Settings = new Dictionary<string, string>();
        }

        public override string ToString() =>
            $"{Name}: {string.Join(", ", Settings.Select(s => $"{s.Key} = '{s.Value}'"))}";
    }
}
