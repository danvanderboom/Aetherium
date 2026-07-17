using Aetherium.Client;
using Aetherium.Unity;
using UnityEngine;

namespace Aphelion
{
    /// <summary>
    /// Follows the perception store's anchor — the player's client-space cell — with a
    /// smoothed three-quarter overhead view that orbits to keep the player's heading pointing
    /// up-screen. Turning (arrow keys → the rotate tool) changes the frame's HeadingDegrees,
    /// and the camera sweeps to match, so a left/right turn visibly rotates the map. The
    /// anchor is the only stable notion of "where the player is": the server never reveals
    /// absolute coordinates.
    /// </summary>
    public sealed class AphelionCameraRig : MonoBehaviour
    {
        [Tooltip("Camera position relative to the player when facing north (heading 0). " +
                 "Orbited by the current heading each frame.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -9f);
        [SerializeField] private float followSpeed = 5f;
        [Tooltip("How sharply the view sweeps to a new heading; higher = snappier turn.")]
        [SerializeField] private float turnSharpness = 12f;
        [Tooltip("World units per grid cell — keep equal to the map/entity views' cellSize.")]
        [SerializeField] private float cellSize = 1f;

        private AetheriumClientBehaviour _behaviour;
        private float _yaw;
        private bool _yawSeeded;

        private void Start()
        {
            _behaviour = FindAnyObjectByType<AetheriumClientBehaviour>();
            if (_behaviour == null)
                Debug.LogWarning("[Aphelion] Camera rig found no AetheriumClientBehaviour in the scene.");
        }

        private void LateUpdate()
        {
            if (_behaviour == null || _behaviour.Store == null)
                return;

            // HeadingDegrees is compass-clockwise (0=N, 90=E); in the scene north is +Z and
            // east is +X, so a positive Unity yaw equal to the heading keeps forward up-screen.
            float targetYaw = _behaviour.Store.LatestFrame?.HeadingDegrees ?? 0f;
            if (!_yawSeeded)
            {
                _yaw = targetYaw; // don't sweep from north on the first frame after a join
                _yawSeeded = true;
            }
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));

            // Grid +Y is south; scene -Z is south (same mapping as the bundled views).
            var anchor = _behaviour.Store.Anchor;
            var target = new Vector3(anchor.X * cellSize, anchor.Z * cellSize, -anchor.Y * cellSize);
            var rotatedOffset = Quaternion.Euler(0f, _yaw, 0f) * offset;
            var desired = target + rotatedOffset;

            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * followSpeed);
            transform.LookAt(target);
        }

        /// <summary>Reset the sweep seed on a discontinuity (join/respawn/portal).</summary>
        public void ResetHeading() => _yawSeeded = false;
    }
}
