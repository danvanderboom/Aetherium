using System;
using System.Collections.Generic;

namespace Aetherium.Model
{
    public class TileTypeDto
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        public TileTypeDto()
        {
        }

        public TileTypeDto(string name, Dictionary<string, string> settings)
        {
            Name = name;
            Settings = settings ?? new Dictionary<string, string>();
        }
    }
}




