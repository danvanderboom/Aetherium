using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Marks a flyer whose control has been seized via a successful hack, recording which entity now controls it.
    /// A controller can then retask the flyer (e.g. issue a new AdHoc/Patterned flight plan) or read its feed.
    /// </summary>
    public class Hacked : Component
    {
        public string ControllerEntityId { get; set; } = string.Empty;

        public Hacked() : base() { }
    }
}
