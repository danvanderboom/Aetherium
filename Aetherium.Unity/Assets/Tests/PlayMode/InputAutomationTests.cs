using System.Collections;
using Aetherium.Unity.Networking;
using Aetherium.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Aetherium.Unity.Tests
{
    public class InputAutomationTests
    {
        [UnityTest]
        public IEnumerator SimulateMoveInput_PlayerMovesOneCell()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("Main");

            var playerController = Object.FindObjectOfType<PlayerController>();
            var gameClientFacade = Object.FindObjectOfType<GameClientFacade>();
            
            if (playerController == null || gameClientFacade == null)
            {
                Assert.Fail("Required components not found in scene");
                yield break;
            }

            yield return new WaitForSeconds(0.5f);

            // Get initial position
            var initialPos = playerController.transform.position;

            // Act - Simulate move input (W key for forward/North)
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(0.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.2f);

            // Assert
            var newPos = playerController.transform.position;
            
            // Player should have moved up (Y axis) by approximately 1 cell
            var deltaY = newPos.y - initialPos.y;
            Assert.Greater(deltaY, 0.5f, 
                $"Player should have moved forward. Initial: {initialPos}, New: {newPos}, DeltaY: {deltaY}");

            InputSystem.RemoveDevice(keyboard);
        }
    }
}

