using Aetherium.Client;
using Aetherium.Unity;
using UnityEngine;

namespace Aphelion
{
    /// <summary>
    /// Follows the perception store's anchor — the player's client-space cell — with a
    /// smoothed three-quarter overhead view. The anchor is the only stable notion of "where
    /// the player is": the server never reveals absolute coordinates.
    /// </summary>
    public sealed class AphelionCameraRig : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -9f);
        [SerializeField] private float followSpeed = 5f;
        [Tooltip("World units per grid cell — keep equal to the map/entity views' cellSize.")]
        [SerializeField] private float cellSize = 1f;

        private AetheriumClientBehaviour _behaviour;

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

            // Grid +Y is south; scene -Z is south (same mapping as the bundled views).
            var anchor = _behaviour.Store.Anchor;
            var target = new Vector3(anchor.X * cellSize, anchor.Z * cellSize, -anchor.Y * cellSize);
            var desired = target + offset;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * followSpeed);
            transform.LookAt(target);
        }
    }
}
