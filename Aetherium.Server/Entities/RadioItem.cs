using Aetherium.Components;

namespace Aetherium.Entities
{
    /// <summary>
    /// A special radio / satellite transceiver. Carrying one that's on and tuned opens the orbital channel
    /// (see <see cref="RadioReceiver"/>), so the player can detect and hack satellites passing overhead —
    /// the item literally hands you a sense you don't otherwise have. A plain <see cref="Item"/> plus a
    /// <see cref="RadioReceiver"/>, mirroring how <see cref="CompassItem"/> carries a navigation component.
    /// </summary>
    public class RadioItem : Item
    {
        public RadioItem() : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Satellite Radio";
            carriable.Icon = "R";

            Set(new RadioReceiver());
        }
    }
}
