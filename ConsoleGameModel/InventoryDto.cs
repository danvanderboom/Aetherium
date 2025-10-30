using System.Collections.Generic;

namespace ConsoleGameModel
{
    public class InventoryDto
    {
        public int Capacity { get; set; }
        public List<ItemDto> Items { get; set; } = new List<ItemDto>();
    }
}


