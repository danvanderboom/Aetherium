#nullable enable
using System;

namespace Aetherium.Unity.Model
{
    /// <summary>
    /// Unity-friendly representation of FlightEnvelopeDto — the perceiver's flight envelope. Present only
    /// when the player can fly/pilot; drives the HUD altitude gauge. Null for non-flyers.
    /// </summary>
    [Serializable]
    public class FlightEnvelopeLite
    {
        public int MinBand { get; set; }
        public int MaxBand { get; set; }
        public int CurrentBand { get; set; }
        public string State { get; set; } = "Airborne";

        public FlightEnvelopeLite()
        {
        }
    }
}
