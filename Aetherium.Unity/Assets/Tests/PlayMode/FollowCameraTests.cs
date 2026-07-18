using Aetherium.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    /// <summary>
    /// PlayMode tests for the orthographic follow camera (Task 3.3/3.5). Runs in
    /// PlayMode so the camera's Awake configures orthographic projection.
    /// </summary>
    public class FollowCameraTests
    {
        private GameObject? camGo;
        private GameObject? targetGo;

        [TearDown]
        public void TearDown()
        {
            if (camGo != null) { Object.DestroyImmediate(camGo); camGo = null; }
            if (targetGo != null) { Object.DestroyImmediate(targetGo); targetGo = null; }
        }

        [Test]
        public void Follow_SnapsToTargetXY_HoldingZOffset()
        {
            camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            var follow = camGo.AddComponent<FollowCamera>();

            targetGo = new GameObject("Player");
            targetGo.transform.position = new Vector3(5f, 10f, 0f);
            follow.SetTarget(targetGo.transform);

            follow.Follow(0.016f);

            Assert.AreEqual(5f, camGo.transform.position.x, 1e-4f);
            Assert.AreEqual(10f, camGo.transform.position.y, 1e-4f);
            Assert.Less(camGo.transform.position.z, 0f, "Camera should hold a negative Z offset for 2D framing");
            Assert.IsTrue(cam.orthographic, "Follow camera should be orthographic");
        }

        [Test]
        public void Follow_TracksTargetAsItMoves()
        {
            camGo = new GameObject("Cam");
            camGo.AddComponent<Camera>();
            var follow = camGo.AddComponent<FollowCamera>();

            targetGo = new GameObject("Player");
            follow.SetTarget(targetGo.transform);

            targetGo.transform.position = new Vector3(2f, 3f, 0f);
            follow.Follow(0.016f);
            Assert.AreEqual(2f, camGo.transform.position.x, 1e-4f);
            Assert.AreEqual(3f, camGo.transform.position.y, 1e-4f);

            targetGo.transform.position = new Vector3(-4f, 7f, 0f);
            follow.Follow(0.016f);
            Assert.AreEqual(-4f, camGo.transform.position.x, 1e-4f);
            Assert.AreEqual(7f, camGo.transform.position.y, 1e-4f);
        }

        [Test]
        public void Follow_NoTarget_DoesNotThrowOrMove()
        {
            camGo = new GameObject("Cam");
            camGo.AddComponent<Camera>();
            var follow = camGo.AddComponent<FollowCamera>();
            camGo.transform.position = new Vector3(1f, 2f, -10f);

            Assert.DoesNotThrow(() => follow.Follow(0.016f));
            Assert.AreEqual(1f, camGo.transform.position.x, 1e-4f);
            Assert.AreEqual(2f, camGo.transform.position.y, 1e-4f);
            Assert.AreEqual(-10f, camGo.transform.position.z, 1e-4f);
        }

        [Test]
        public void OrthographicSize_SetterUpdatesCamera()
        {
            camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            var follow = camGo.AddComponent<FollowCamera>();

            follow.OrthographicSize = 12f;
            Assert.AreEqual(12f, cam.orthographicSize, 1e-4f);
        }
    }
}
