#nullable enable
using UnityEngine;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Orthographic follow camera for the 2D depth renderer. Tracks a target (the
    /// player marker) in the XY plane while holding a fixed negative Z offset so the
    /// overhead view stays centered on the player. This is the plain follow;
    /// adaptive framing (zoom to the local vertical extent, isometric tilt past a
    /// complexity threshold) is layered on in Section 5.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform? target;
        [SerializeField] private float zOffset = -10f;
        [SerializeField] private float orthographicSize = 8f;

        // 0 = snap instantly to the target; higher = faster exponential approach.
        // Framerate-independent via 1 - exp(-sharpness·dt).
        [SerializeField] private float followSharpness = 0f;

        private Camera? cam;

        /// <summary>The transform this camera follows.</summary>
        public Transform? Target
        {
            get => target;
            set => target = value;
        }

        /// <summary>Orthographic half-height; assigning it updates the camera.</summary>
        public float OrthographicSize
        {
            get => orthographicSize;
            set
            {
                orthographicSize = value;
                if (cam != null)
                    cam.orthographicSize = value;
            }
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = orthographicSize;
            }
        }

        /// <summary>Convenience setter mirroring the console client's wiring style.</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;

        private void LateUpdate() => Follow(Time.deltaTime);

        /// <summary>
        /// Advances the follow by <paramref name="deltaTime"/>. Public so deterministic
        /// tests can step it without pumping real frames. No-op when there is no target.
        /// </summary>
        public void Follow(float deltaTime)
        {
            if (target == null)
                return;

            var desired = new Vector3(target.position.x, target.position.y, zOffset);

            if (followSharpness <= 0f)
            {
                transform.position = desired;
            }
            else
            {
                float t = 1f - Mathf.Exp(-followSharpness * deltaTime);
                transform.position = Vector3.Lerp(transform.position, desired, t);
            }
        }
    }
}
