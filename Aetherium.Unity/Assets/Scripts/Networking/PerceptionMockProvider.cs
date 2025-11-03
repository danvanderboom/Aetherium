using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aetherium.Unity.Model;
using UnityEngine;

namespace Aetherium.Unity.Networking
{
    /// <summary>
    /// Mock provider that replays Perception JSON frames from StreamingAssets.
    /// </summary>
    public class PerceptionMockProvider : IPerceptionProvider
    {
        private List<PerceptionLite> frames = new List<PerceptionLite>();
        private int currentFrameIndex = 0;
        private string framesPath;

        public event Action<PerceptionLite>? PerceptionUpdated;

        public PerceptionMockProvider()
        {
            framesPath = Path.Combine(Application.streamingAssetsPath, "PerceptionFrames");
            LoadFrames();
        }

        private void LoadFrames()
        {
            frames.Clear();
            
            if (!Directory.Exists(framesPath))
            {
                Debug.LogWarning($"PerceptionFrames directory not found at {framesPath}");
                // Create a default empty perception
                frames.Add(new PerceptionLite());
                return;
            }

            var jsonFiles = Directory.GetFiles(framesPath, "*.json")
                .OrderBy(f => f)
                .ToArray();

            if (jsonFiles.Length == 0)
            {
                Debug.LogWarning($"No JSON files found in {framesPath}. Creating default frame.");
                frames.Add(new PerceptionLite());
                return;
            }

            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(jsonFile);
                    var perception = JsonUtility.FromJson<PerceptionLite>(json);
                    if (perception != null)
                    {
                        frames.Add(perception);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load perception frame from {jsonFile}: {ex.Message}");
                }
            }

            Debug.Log($"Loaded {frames.Count} perception frames from {framesPath}");
        }

        public PerceptionLite? GetCurrent()
        {
            if (frames.Count == 0)
                return null;

            return frames[currentFrameIndex];
        }

        /// <summary>
        /// Advances to the next frame, cycling if at the end.
        /// </summary>
        public void NextFrame()
        {
            if (frames.Count == 0)
                return;

            currentFrameIndex = (currentFrameIndex + 1) % frames.Count;
            var current = GetCurrent();
            if (current != null)
            {
                PerceptionUpdated?.Invoke(current);
            }
        }

        /// <summary>
        /// Updates local mock state based on a tool execution (for offline mode).
        /// </summary>
        public void UpdateMockState(string toolId, Dictionary<string, object> args)
        {
            if (frames.Count == 0)
                return;

            var current = frames[currentFrameIndex];
            if (current == null)
                return;

            // Simple mock state mutations for offline testing
            switch (toolId.ToLower())
            {
                case "move":
                    HandleMove(current, args);
                    break;
                case "rotate":
                    HandleRotate(current, args);
                    break;
                case "changelevel":
                    HandleChangeLevel(current, args);
                    break;
            }

            PerceptionUpdated?.Invoke(current);
        }

        private void HandleMove(PerceptionLite perception, Dictionary<string, object> args)
        {
            if (args.TryGetValue("direction", out var directionObj))
            {
                var direction = directionObj.ToString()?.ToLower();
                var loc = perception.PlayerLocation;

                switch (direction)
                {
                    case "forward":
                    case "north":
                        loc.Y += 1;
                        break;
                    case "backward":
                    case "south":
                        loc.Y -= 1;
                        break;
                    case "right":
                    case "east":
                        loc.X += 1;
                        break;
                    case "left":
                    case "west":
                        loc.X -= 1;
                        break;
                }
            }
        }

        private void HandleRotate(PerceptionLite perception, Dictionary<string, object> args)
        {
            if (args.TryGetValue("clockwise", out var clockwiseObj) && clockwiseObj is bool clockwise)
            {
                if (clockwise)
                {
                    perception.HeadingDegrees = (perception.HeadingDegrees + 90) % 360;
                }
                else
                {
                    perception.HeadingDegrees = (perception.HeadingDegrees - 90 + 360) % 360;
                }

                // Update enum heading
                perception.PlayerHeading = DegreesToDirection(perception.HeadingDegrees);
            }
        }

        private void HandleChangeLevel(PerceptionLite perception, Dictionary<string, object> args)
        {
            if (args.TryGetValue("up", out var upObj) && upObj is bool up)
            {
                if (up)
                {
                    perception.PlayerLocation.Z += 1;
                }
                else
                {
                    perception.PlayerLocation.Z -= 1;
                }
            }
        }

        private WorldDirectionLite DegreesToDirection(int degrees)
        {
            degrees = ((degrees % 360) + 360) % 360;
            if (degrees >= 315 || degrees < 45) return WorldDirectionLite.North;
            if (degrees >= 45 && degrees < 135) return WorldDirectionLite.East;
            if (degrees >= 135 && degrees < 225) return WorldDirectionLite.South;
            return WorldDirectionLite.West;
        }
    }
}

