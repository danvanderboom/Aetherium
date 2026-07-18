#nullable enable
using System.Collections.Generic;
using Aetherium.Unity.Model;
using UnityEngine;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Pure altitude-gauge math (the Unity analogue of the console glyph ladder). Turns a flight envelope
    /// into discrete rungs from MaxBand down to MinBand with the current band flagged, plus a 0..1 normalized
    /// position for a continuous HUD meter. Holds no scene state — unit tested in EditMode.
    /// </summary>
    public static class AltitudeGauge
    {
        /// <summary>Rungs from MaxBand (top) to MinBand, flagging the current band. Empty when not flying.</summary>
        public static List<(int band, bool isCurrent)> BuildRungs(FlightEnvelopeLite? envelope)
        {
            var rungs = new List<(int band, bool isCurrent)>();
            if (envelope == null)
                return rungs;

            int min = Mathf.Min(envelope.MinBand, envelope.MaxBand);
            int max = Mathf.Max(envelope.MinBand, envelope.MaxBand);
            for (int b = max; b >= min; b--)
                rungs.Add((b, b == envelope.CurrentBand));
            return rungs;
        }

        /// <summary>
        /// The current band's position within [MinBand, MaxBand] as 0..1 (0 = MinBand, 1 = MaxBand), for a
        /// continuous meter fill. 0 when not flying or when the envelope is a single band.
        /// </summary>
        public static float NormalizedPosition(FlightEnvelopeLite? envelope)
        {
            if (envelope == null)
                return 0f;

            int min = Mathf.Min(envelope.MinBand, envelope.MaxBand);
            int max = Mathf.Max(envelope.MinBand, envelope.MaxBand);
            if (max == min)
                return 0f;

            return Mathf.Clamp01((float)(envelope.CurrentBand - min) / (max - min));
        }
    }
}
