using System.Collections.Generic;
using Aetherium.Unity.Model;
using Aetherium.Unity.Networking;
using Aetherium.Unity.Spatial;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Handles player input and communicates with GameClientFacade to execute tools.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private GameClientFacade? gameClientFacade;
        [SerializeField] private float moveSpeed = 1.0f;

        private Vector3 targetPosition;
        private bool isMoving = false;

        private void Awake()
        {
            if (gameClientFacade == null)
            {
                gameClientFacade = FindObjectOfType<GameClientFacade>();
            }

            targetPosition = transform.position;
        }

        private void Update()
        {
            // Smooth movement interpolation
            if (isMoving)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
                if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
                {
                    transform.position = targetPosition;
                    isMoving = false;
                }
            }
        }

        /// <summary>
        /// Updates the player marker position based on perception.
        /// </summary>
        public void UpdatePosition(PerceptionLite perception)
        {
            if (perception?.PlayerLocation == null)
                return;

            var worldPos = GridHelpers.GridToWorld(perception.PlayerLocation);
            targetPosition = worldPos;
            isMoving = true;

            // Update heading/rotation
            UpdateRotation(perception.HeadingDegrees);
        }

        private void UpdateRotation(int headingDegrees)
        {
            // Rotate sprite to face heading direction
            // Unity's 2D typically uses 0 degrees as right, so adjust for game's North-up convention
            float rotationZ = (headingDegrees - 90) % 360; // Convert North (0) to Up (-90)
            transform.rotation = Quaternion.Euler(0, 0, -rotationZ);
        }

        // Input System handlers
        public void OnMove(InputAction.CallbackContext context)
        {
            if (!context.performed || gameClientFacade == null)
                return;

            var moveInput = context.ReadValue<Vector2>();
            var direction = Vector2ToDirection(moveInput);
            
            if (direction != null)
            {
                var args = new Dictionary<string, object>
                {
                    { "direction", direction },
                    { "distance", 1 }
                };
                gameClientFacade.ExecuteTool("move", args);
            }
        }

        public void OnRotate(InputAction.CallbackContext context)
        {
            if (!context.performed || gameClientFacade == null)
                return;

            // Z = rotate left (counter-clockwise), X = rotate right (clockwise)
            var key = context.control.name.ToLower();
            bool clockwise = key == "x" || key == "e";

            var args = new Dictionary<string, object>
            {
                { "clockwise", clockwise }
            };
            gameClientFacade.ExecuteTool("rotate", args);
        }

        public void OnChangeLevel(InputAction.CallbackContext context)
        {
            if (!context.performed || gameClientFacade == null)
                return;

            // PageUp/U = up, PageDown/D = down
            var key = context.control.name.ToLower();
            bool up = key == "pageup" || key == "u" || key == "w";

            var args = new Dictionary<string, object>
            {
                { "up", up }
            };
            gameClientFacade.ExecuteTool("changelevel", args);
        }

        private string? Vector2ToDirection(Vector2 input)
        {
            // Threshold to avoid diagonal movement
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                return input.x > 0 ? "east" : "west";
            }
            else if (Mathf.Abs(input.y) > 0.1f)
            {
                return input.y > 0 ? "north" : "south";
            }
            return null;
        }
    }
}

