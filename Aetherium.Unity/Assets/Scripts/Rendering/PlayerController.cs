using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Unity.Model;
using Aetherium.Unity.Networking;
using Aetherium.Unity.Spatial;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Handles player input and communicates with GameClientFacade to execute tools.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private GameClientFacade? gameClientFacade;
        [SerializeField] private GameManager? gameManager;
        [SerializeField] private Text? hudText;
        [SerializeField] private float moveSpeed = 1.0f;

        private Vector3 targetPosition;
        private bool isMoving = false;

        // Option selection state
        private bool isChoosingOption = false;
        private string? pendingToolId;
        private Dictionary<string, object>? pendingArgsBase;
        private List<UsageOptionDto> options = new List<UsageOptionDto>();
        private int selectedOptionIndex = 0;
        private string? originalHudText;

        private void Awake()
        {
            if (gameClientFacade == null)
            {
                gameClientFacade = FindObjectOfType<GameClientFacade>();
            }

            if (gameManager == null)
            {
                gameManager = FindObjectOfType<GameManager>();
            }

            // Try to get HUD text from GameManager if not assigned
            if (hudText == null && gameManager != null)
            {
                hudText = gameManager.HudText;
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
            if (!context.performed || gameClientFacade == null || isChoosingOption)
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
            if (!context.performed || gameClientFacade == null || isChoosingOption)
                return;

            // Read as float axis: negative = counter-clockwise, positive = clockwise
            float axisValue = context.ReadValue<float>();
            if (Mathf.Abs(axisValue) < 0.1f)
                return;

            bool clockwise = axisValue > 0f;

            var args = new Dictionary<string, object>
            {
                { "clockwise", clockwise }
            };
            gameClientFacade.ExecuteTool("rotate", args);
        }

        public void OnChangeLevel(InputAction.CallbackContext context)
        {
            if (!context.performed || gameClientFacade == null || isChoosingOption)
                return;

            // Read as float axis: negative = down, positive = up
            float axisValue = context.ReadValue<float>();
            if (Mathf.Abs(axisValue) < 0.1f)
                return;

            bool up = axisValue > 0f;

            var args = new Dictionary<string, object>
            {
                { "up", up }
            };
            gameClientFacade.ExecuteTool("changelevel", args);
        }

        public async void OnUse(InputAction.CallbackContext context)
        {
            if (!context.performed || gameClientFacade == null)
                return;

            if (isChoosingOption)
            {
                // Already in option selection mode, use OptionConfirm instead
                return;
            }

            // For now, execute a simple "use" tool (can be extended later for context use)
            // This is a placeholder - in a real implementation, this would determine what to use based on context
            var args = new Dictionary<string, object>();
            var result = await gameClientFacade.ExecuteToolAsync("use", args);

            // Check if result contains options (multi-use tool)
            if (result.Success && result.Data != null && result.Data.TryGetValue("options", out var optionsObj))
            {
                EnterOptionSelectionMode("use", args, optionsObj);
            }
        }

        public void OnOptionNavUp(InputAction.CallbackContext context)
        {
            if (!isChoosingOption || !context.performed)
                return;

            selectedOptionIndex = (selectedOptionIndex - 1 + options.Count) % options.Count;
            UpdateOptionsDisplay();
        }

        public void OnOptionNavDown(InputAction.CallbackContext context)
        {
            if (!isChoosingOption || !context.performed)
                return;

            selectedOptionIndex = (selectedOptionIndex + 1) % options.Count;
            UpdateOptionsDisplay();
        }

        public async void OnOptionConfirm(InputAction.CallbackContext context)
        {
            if (!isChoosingOption || !context.performed || gameClientFacade == null || options.Count == 0)
                return;

            if (pendingToolId == null || pendingArgsBase == null)
            {
                ExitOptionSelectionMode();
                return;
            }

            // Re-execute tool with selected usageId
            var selectedOption = options[selectedOptionIndex];
            var args = new Dictionary<string, object>(pendingArgsBase)
            {
                ["usageId"] = selectedOption.UsageId
            };

            var result = await gameClientFacade.ExecuteToolAsync(pendingToolId, args);
            
            // If result has options again (shouldn't happen, but handle it), stay in selection mode
            if (result.Success && result.Data != null && result.Data.TryGetValue("options", out var optionsObj))
            {
                EnterOptionSelectionMode(pendingToolId, pendingArgsBase, optionsObj);
            }
            else
            {
                ExitOptionSelectionMode();
            }
        }

        public void OnOptionCancel(InputAction.CallbackContext context)
        {
            if (!isChoosingOption || !context.performed)
                return;

            ExitOptionSelectionMode();
        }

        private void EnterOptionSelectionMode(string toolId, Dictionary<string, object> argsBase, object optionsObj)
        {
            isChoosingOption = true;
            pendingToolId = toolId;
            pendingArgsBase = new Dictionary<string, object>(argsBase);

            // Parse options from result
            options.Clear();
            if (optionsObj is List<object> optionsList)
            {
                foreach (var optObj in optionsList)
                {
                    if (optObj is Dictionary<string, object> optDict)
                    {
                        options.Add(new UsageOptionDto
                        {
                            UsageId = optDict.TryGetValue("usageId", out var id) ? id?.ToString() ?? string.Empty : string.Empty,
                            Label = optDict.TryGetValue("label", out var label) ? label?.ToString() ?? string.Empty : string.Empty,
                            Description = optDict.TryGetValue("description", out var desc) ? desc?.ToString() ?? string.Empty : string.Empty
                        });
                    }
                }
            }

            selectedOptionIndex = 0;
            UpdateOptionsDisplay();
        }

        private void ExitOptionSelectionMode()
        {
            isChoosingOption = false;
            pendingToolId = null;
            pendingArgsBase = null;
            options.Clear();
            selectedOptionIndex = 0;

            // Restore original HUD text
            if (hudText != null && originalHudText != null)
            {
                hudText.text = originalHudText;
                originalHudText = null;
            }
            else if (gameManager != null)
            {
                // GameManager will update HUD in next perception update
            }
        }

        private void UpdateOptionsDisplay()
        {
            if (hudText == null)
                return;

            // Save original HUD text if not already saved
            if (originalHudText == null)
            {
                originalHudText = hudText.text;
            }

            // Display options with selection indicator
            var optionTexts = new List<string>();
            for (int i = 0; i < options.Count; i++)
            {
                var opt = options[i];
                var indicator = (i == selectedOptionIndex) ? ">> " : "   ";
                optionTexts.Add($"{indicator}{opt.Label}: {opt.Description}");
            }

            hudText.text = "Select Option:\n" + string.Join("\n", optionTexts);
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

