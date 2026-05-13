using Aetherium.Unity.Model;
using Aetherium.Unity.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Main game manager that coordinates perception updates, rendering, and HUD display.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private GameClientFacade? gameClientFacade;
        [SerializeField] private TilemapRenderer2D? tilemapRenderer;
        [SerializeField] private PlayerController? playerController;
        [SerializeField] private Text? hudText;
        [SerializeField] private int currentZLevel = 0;

        private PerceptionLite? currentPerception;

        public Text? HudText => hudText;

        private void Awake()
        {
            if (gameClientFacade == null)
            {
                gameClientFacade = FindAnyObjectByType<GameClientFacade>();
            }

            if (tilemapRenderer == null)
            {
                tilemapRenderer = FindAnyObjectByType<TilemapRenderer2D>();
            }

            if (playerController == null)
            {
                playerController = FindAnyObjectByType<PlayerController>();
            }
        }

        private void Start()
        {
            if (gameClientFacade != null)
            {
                gameClientFacade.PerceptionUpdated += OnPerceptionUpdated;
                // Initial update
                var perception = gameClientFacade.CurrentPerception;
                if (perception != null)
                {
                    OnPerceptionUpdated(perception);
                }
            }
        }

        private void OnDestroy()
        {
            if (gameClientFacade != null)
            {
                gameClientFacade.PerceptionUpdated -= OnPerceptionUpdated;
            }
        }

        private void OnPerceptionUpdated(PerceptionLite perception)
        {
            currentPerception = perception;

            // Update tilemap renderer
            if (tilemapRenderer != null)
            {
                tilemapRenderer.RenderPerception(perception, currentZLevel);
            }

            // Update player marker
            if (playerController != null && perception.PlayerLocation != null)
            {
                playerController.UpdatePosition(perception);
            }

            // Update HUD
            UpdateHUD(perception);
        }

        /// <summary>
        /// Repaints the HUD from the most recent perception. Safe to call when
        /// option-selection mode has just ended and the controller needs the
        /// default HUD restored without waiting for the next perception tick.
        /// </summary>
        public void RefreshHUD()
        {
            if (currentPerception != null)
            {
                UpdateHUD(currentPerception);
            }
        }

        private void UpdateHUD(PerceptionLite perception)
        {
            if (hudText == null)
                return;

            // While the player is choosing a usage option, PlayerController owns the HUD.
            if (playerController != null && playerController.IsChoosingOption)
                return;

            var headingText = perception.PlayerHeading.ToString();
            var zText = perception.PlayerLocation != null ? perception.PlayerLocation.Z.ToString() : "0";
            var headingDegrees = perception.HeadingDegrees;
            var tileCount = tilemapRenderer != null ? tilemapRenderer.GetRenderedTileCount() : 0;

            hudText.text = $"Z: {zText} | Heading: {headingText} ({headingDegrees}°) | Tiles: {tileCount}";
        }

        /// <summary>
        /// Cycles to the next Z-level (called by input handler).
        /// </summary>
        public void CycleZLevel(bool up)
        {
            if (currentPerception == null)
                return;

            // Find min/max Z levels in perception
            int minZ = int.MaxValue;
            int maxZ = int.MinValue;

            foreach (var visual in currentPerception.Visuals.Values)
            {
                if (visual.Location.Z < minZ) minZ = visual.Location.Z;
                if (visual.Location.Z > maxZ) maxZ = visual.Location.Z;
            }

            if (minZ == int.MaxValue)
                return;

            if (up)
            {
                currentZLevel = (currentZLevel >= maxZ) ? minZ : currentZLevel + 1;
            }
            else
            {
                currentZLevel = (currentZLevel <= minZ) ? maxZ : currentZLevel - 1;
            }

            // Update renderer
            if (tilemapRenderer != null)
            {
                tilemapRenderer.SetZLevel(currentZLevel);
            }

            // Update HUD
            if (currentPerception != null)
            {
                UpdateHUD(currentPerception);
            }
        }

        /// <summary>
        /// Gets the current Z-level.
        /// </summary>
        public int GetCurrentZLevel() => currentZLevel;
    }
}

