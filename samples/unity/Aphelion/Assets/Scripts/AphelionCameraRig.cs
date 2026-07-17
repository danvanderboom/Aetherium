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

        // Optimistic heading: a turn's only visible effect is the camera orbit, and perception
        // frames (which carry the authoritative HeadingDegrees) arrive on the server's tick
        // cadence — so waiting for one before sweeping makes a turn lag ~1-2s while movement,
        // which advances the anchor client-side, feels instant. We predict the turn locally the
        // moment a key is pressed (PredictTurn/PredictHeadingTo) and hold that target until the
        // authoritative frame catches up, then resume following it. This mirrors the anchor
        // optimism in ToolClient.AdvanceAnchorForMove.
        private float _targetHeading;
        private int _pendingTurns; // optimistic turns the server hasn't confirmed yet

        private void Start()
        {
            _behaviour = FindAnyObjectByType<AetheriumClientBehaviour>();
            if (_behaviour == null)
                Debug.LogWarning("[Aphelion] Camera rig found no AetheriumClientBehaviour in the scene.");
        }

        /// <summary>Optimistically turn 90° now (arrow keys). Roll back with the opposite turn
        /// if the server rejects the rotate.</summary>
        public void PredictTurn(bool clockwise)
        {
            _targetHeading = NormalizeHeading(_targetHeading + (clockwise ? 90f : -90f));
            _pendingTurns++;
        }

        /// <summary>Undo an optimistic <see cref="PredictTurn"/> (rotate was rejected).</summary>
        public void RollbackTurn(bool clockwise)
        {
            _targetHeading = NormalizeHeading(_targetHeading - (clockwise ? 90f : -90f));
            _pendingTurns = Mathf.Max(0, _pendingTurns - 1);
        }

        /// <summary>Optimistically face an absolute compass heading now (WASD composes a
        /// rotate+step; the rotate always lands even if the step is blocked, so no rollback).</summary>
        public void PredictHeadingTo(float absoluteHeading)
        {
            _targetHeading = NormalizeHeading(absoluteHeading);
            _pendingTurns++;
        }

        private static float NormalizeHeading(float degrees) => Mathf.Repeat(degrees, 360f);

        private void LateUpdate()
        {
            if (_behaviour == null || _behaviour.Store == null)
                return;

            // HeadingDegrees is compass-clockwise (0=N, 90=E); in the scene north is +Z and
            // east is +X, so a positive Unity yaw equal to the heading keeps forward up-screen.
            float serverHeading = _behaviour.Store.LatestFrame?.HeadingDegrees ?? 0f;
            if (!_yawSeeded)
            {
                _targetHeading = serverHeading;
                _yaw = serverHeading; // don't sweep from north on the first frame after a join
                _yawSeeded = true;
            }
            // Reconcile: once the authoritative frame reports the heading we predicted, stop
            // overriding. While idle (nothing pending) follow the server exactly, so a
            // server-driven heading change (forced rotation, respawn) still shows.
            if (_pendingTurns > 0 && Mathf.Abs(Mathf.DeltaAngle(_targetHeading, serverHeading)) < 1f)
                _pendingTurns = 0;
            if (_pendingTurns == 0)
                _targetHeading = serverHeading;

            _yaw = Mathf.LerpAngle(_yaw, _targetHeading, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));

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
