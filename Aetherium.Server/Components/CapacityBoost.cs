using Aetherium.Core;

namespace Aetherium.Components
{
    public class CapacityBoost : Component
    {
        public int AdditionalCapacity { get; set; } = 5;

        /// <summary>
        /// Whether this item's capacity bonus is currently applied to its owner's
        /// inventory. Tracked so re-equipping the same item can't stack the bonus
        /// repeatedly (the "equip a backpack 1000× for infinite capacity" exploit).
        /// </summary>
        public bool IsEquipped { get; set; } = false;

        public CapacityBoost() : base() { }
    }
}


