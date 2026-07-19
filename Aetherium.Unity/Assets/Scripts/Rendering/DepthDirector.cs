#nullable enable
using Aetherium.Unity.Model;
using UnityEngine;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Ties perception depth to camera framing and mode escalation (Section 5.2, Unity half). On each frame it
    /// counts the occupied bands, pulls the orthographic camera back proportionally (<see cref="DepthFraming"/>),
    /// and — past the escalation threshold — surfaces the cross-section overlay. Flat street: close framing, no
    /// overlay. Tall interchange: pulled back, overlay shown.
    /// </summary>
    public class DepthDirector : MonoBehaviour
    {
        [SerializeField] private FollowCamera? followCamera;
        [SerializeField] private CrossSectionOverlay? crossSection;

        [SerializeField] private float baseOrthographicSize = DepthFraming.DefaultBaseSize;
        [SerializeField] private float perBandSize = DepthFraming.DefaultPerBand;
        [SerializeField] private float maxOrthographicSize = DepthFraming.DefaultMaxSize;
        [SerializeField] private int crossSectionThreshold = DepthFraming.DefaultEscalationThreshold;
        [SerializeField] private int crossSectionHalfWidth = 6;

        /// <summary>Occupied-band count from the most recent perception.</summary>
        public int LastBandCount { get; private set; }

        /// <summary>Whether the cross-section overlay is currently surfaced.</summary>
        public bool CrossSectionActive { get; private set; }

        /// <summary>Wires dependencies at runtime (used by tests and code-driven scene setup).</summary>
        public void SetDependencies(FollowCamera camera, CrossSectionOverlay overlay)
        {
            followCamera = camera;
            crossSection = overlay;
        }

        /// <summary>Reframes and (de)escalates the view for the given perception frame.</summary>
        public void OnPerception(PerceptionLite perception)
        {
            int bandCount = DepthFraming.OccupiedBandCount(perception);
            LastBandCount = bandCount;

            if (followCamera != null)
            {
                followCamera.OrthographicSize = DepthFraming.OrthographicSizeFor(
                    bandCount, baseOrthographicSize, perBandSize, maxOrthographicSize);
            }

            bool surface = DepthFraming.ShouldSurfaceCrossSection(bandCount, crossSectionThreshold);
            CrossSectionActive = surface;

            if (crossSection != null)
            {
                if (surface)
                    crossSection.Render(perception, crossSectionHalfWidth);
                crossSection.Show(surface);
            }
        }
    }
}
