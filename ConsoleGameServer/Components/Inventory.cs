using System;
using System.Collections.Generic;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class Inventory : Component
    {
        public int Capacity { get; set; } = 10;
        public List<string> ItemEntityIds { get; } = new List<string>();
        public Dictionary<string, Entity> Items { get; } = new Dictionary<string, Entity>();

        public bool TryAdd(string entityId, Entity entity)
        {
            if (ItemEntityIds.Count >= Capacity)
                return false;
            ItemEntityIds.Add(entityId);
            Items[entityId] = entity;
            return true;
        }

        public bool Remove(string entityId)
        {
            var removed = ItemEntityIds.Remove(entityId);
            if (removed)
                Items.Remove(entityId);
            return removed;
        }
    }
}


