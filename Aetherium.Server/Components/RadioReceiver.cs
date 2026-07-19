using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// A radio/transceiver that, while switched <see cref="On"/> and <see cref="Tuned"/>, grants an extra
    /// <b>channel</b> of perception — the orbital channel: satellites overhead, otherwise undetectable, appear
    /// in the perception frame within <see cref="SatelliteRange"/> cells of the perceiver's ground position,
    /// and can be hacked while they pass over. Attach it to an item the player carries (a special radio) or,
    /// for a creature with a built-in receiver, to the entity itself. Turned off or off-frequency, the channel
    /// goes silent and the sky reads empty to the naked eye.
    /// </summary>
    public class RadioReceiver : Component
    {
        /// <summary>Powered on.</summary>
        public bool On { get; set; } = true;

        /// <summary>Tuned to the orbital band (a real game would model a frequency; a bool suffices for the slice).</summary>
        public bool Tuned { get; set; } = true;

        /// <summary>How far (in cells, horizontally) a satellite's ground track can be and still be received.</summary>
        public int SatelliteRange { get; set; } = 48;

        /// <summary>Which channel this receiver opens (future-proofing for more channels; "orbital" today).</summary>
        public string Channel { get; set; } = "orbital";

        public RadioReceiver() : base() { }
    }
}
