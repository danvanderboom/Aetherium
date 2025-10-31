using System.Collections.Generic;

namespace Aetherium.Model
{
    public class InventoryDto
    {
        public int Capacity { get; set; }
        public List<ItemDto> Items { get; set; } = new List<ItemDto>();
    }
}



