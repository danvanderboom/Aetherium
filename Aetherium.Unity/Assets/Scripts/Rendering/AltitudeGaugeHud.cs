#nullable enable
using System.Collections.Generic;
using Aetherium.Unity.Model;
using UnityEngine;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// HUD altitude meter for a flying player: a discrete-step gauge over the flyer's [MinBand, MaxBand] with
    /// the current band highlighted, hidden when not flying. This holds the computed gauge state (rungs +
    /// normalized fill + visibility) each frame; a concrete UI meter can bind to it. Driven by the depth
    /// director / game manager from each perception frame.
    /// </summary>
    public class AltitudeGaugeHud : MonoBehaviour
    {
        private readonly List<(int band, bool isCurrent)> rungs = new List<(int band, bool isCurrent)>();

        /// <summary>True while the player is flying/piloting (has a flight envelope).</summary>
        public bool IsVisible { get; private set; }

        /// <summary>Current band position within the envelope as 0..1 for a continuous meter fill.</summary>
        public float Normalized { get; private set; }

        /// <summary>The player's current altitude band.</summary>
        public int CurrentBand { get; private set; }

        /// <summary>Discrete rungs (top band first), each flagged if it is the current band.</summary>
        public IReadOnlyList<(int band, bool isCurrent)> Rungs => rungs;

        /// <summary>Recomputes the gauge from a perception frame. Hides it when the player is not flying.</summary>
        public void UpdateFrom(PerceptionLite perception)
        {
            var envelope = perception?.FlightEnvelope;
            rungs.Clear();

            if (envelope == null)
            {
                IsVisible = false;
                Normalized = 0f;
                return;
            }

            rungs.AddRange(AltitudeGauge.BuildRungs(envelope));
            Normalized = AltitudeGauge.NormalizedPosition(envelope);
            CurrentBand = envelope.CurrentBand;
            IsVisible = rungs.Count > 0;
        }
    }
}
