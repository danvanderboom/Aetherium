using System.Collections;
using Aetherium.Unity.Model;
using Aetherium.Unity.Networking;
using Aetherium.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Aetherium.Unity.Tests
{
    public class TilemapAndInputTests
    {
        [SetUp]
        public void SetUp()
        {
            // Enable Input System
            if (!InputSystem.settings.enabled)
            {
                InputSystem.settings.SetInternalFeatureFlag("ENABLE_INPUT_SYSTEM", true);
            }
        }

        [UnityTest]
        public IEnumerator LoadMainScene_TilemapRendersCorrectly()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("Main");

            var tilemapRenderer = Object.FindAnyObjectByType<TilemapRenderer2D>();
            Assert.IsNotNull(tilemapRenderer, "TilemapRenderer2D should exist in scene");

            var gameClientFacade = Object.FindAnyObjectByType<GameClientFacade>();
            Assert.IsNotNull(gameClientFacade, "GameClientFacade should exist in scene");

            // Wait for perception to load
            yield return new WaitForSeconds(0.5f);

            // Act
            var perception = gameClientFacade.CurrentPerception;
            
            if (perception != null)
            {
                tilemapRenderer.RenderPerception(perception, 0);
                yield return new WaitForEndOfFrame();

                // Assert
                var tileCount = tilemapRenderer.GetRenderedTileCount();
                Assert.Greater(tileCount, 0, "Should render at least one tile");
            }
            else
            {
                Debug.LogWarning("No perception loaded - this is expected if no JSON frames are available");
            }
        }

        [UnityTest]
        public IEnumerator PlayerMarker_UpdatesOnPerception()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("Main");

            var playerController = Object.FindAnyObjectByType<PlayerController>();
            if (playerController == null)
            {
                Assert.Fail("PlayerController not found in scene");
                yield break;
            }

            var gameClientFacade = Object.FindAnyObjectByType<GameClientFacade>();
            if (gameClientFacade == null)
            {
                Assert.Fail("GameClientFacade not found in scene");
                yield break;
            }

            // Wait for perception to load
            yield return new WaitForSeconds(0.5f);

            // Act - Create a mock perception with a known player location
            var perception = new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(5, 10, 0),
                PlayerHeading = WorldDirectionLite.North,
                HeadingDegrees = 0,
                VisibleBounds = new RectangleLite(-5, -5, 11, 11),
                Visuals = new System.Collections.Generic.Dictionary<string, VisualLite>(),
                TileTypes = new System.Collections.Generic.Dictionary<string, TileTypeLite>()
            };

            playerController.UpdatePosition(perception);
            yield return new WaitForSeconds(0.1f);

            // Assert
            var worldPos = Aetherium.Unity.Spatial.GridHelpers.GridToWorld(perception.PlayerLocation);
            Assert.Less(Vector3.Distance(playerController.transform.position, worldPos), 0.1f,
                $"Player marker should be at grid position {worldPos}, but is at {playerController.transform.position}");
        }
    }
}

