namespace Aetherium.Model
{
    /// <summary>
    /// The perceiving player's flight envelope, present only when they have a Flight
    /// component (i.e. they can fly or are piloting). Drives the altitude gauge: N discrete
    /// steps spanning <see cref="MinBand"/>…<see cref="MaxBand"/> with <see cref="CurrentBand"/>
    /// highlighted. Because relative-coordinate perception reports the player at Z 0, the real
    /// band is surfaced here explicitly. Null for non-flyers, so this is a purely additive,
    /// non-breaking field.
    /// </summary>
    public class FlightEnvelopeDto
    {
        /// <summary>Lowest altitude band the flyer can occupy.</summary>
        public int MinBand { get; set; }

        /// <summary>Highest altitude band the flyer can occupy.</summary>
        public int MaxBand { get; set; }

        /// <summary>The flyer's current altitude band (absolute Z), highlighted on the gauge.</summary>
        public int CurrentBand { get; set; }

        /// <summary>Flight state (Airborne / TakingOff / Landing / Landed) so the client can decide whether to surface the gauge.</summary>
        public string State { get; set; } = "Airborne";
    }
}
