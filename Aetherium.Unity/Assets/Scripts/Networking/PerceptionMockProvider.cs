using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Unity.Model;
using Newtonsoft.Json;
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
        private string framesPath = string.Empty;

        public event Action<PerceptionLite>? PerceptionUpdated;

        public PerceptionMockProvider()
        {
            framesPath = Path.Combine(Application.streamingAssetsPath, "PerceptionFrames");
            LoadFrames();
        }

        private void LoadFrames()
        {
            frames.Clear();

#if UNITY_ANDROID || UNITY_WEBGL
            // On Android (APK / asset bundle) and WebGL (HTTP), StreamingAssets is
            // not a real filesystem path — File.IO will silently return nothing.
            // The honest fix is to load via UnityWebRequest asynchronously, which
            // requires turning the constructor into an explicit InitializeAsync
            // step. Until that happens, refuse to pretend and warn loudly so
            // developers don't ship a "working" empty offline mode.
            if (Application.platform == RuntimePlatform.Android ||
                Application.platform == RuntimePlatform.WebGLPlayer)
            {
                Debug.LogWarning(
                    "PerceptionMockProvider: synchronous file loading is not supported on " +
                    $"{Application.platform}. Offline mock frames will not be available until " +
                    "an async UnityWebRequest-based loader is wired up. Use live (SignalR) mode instead.");
                frames.Add(new PerceptionLite());
                return;
            }
#endif

            if (!Directory.Exists(framesPath))
            {
                Debug.LogWarning($"PerceptionFrames directory not found at {framesPath}");
                // Create a default empty perception
                frames.Add(new PerceptionLite());
                return;
            }

            // Natural sort so `frame_10.json` follows `frame_9.json` instead of
            // sorting between `frame_1.json` and `frame_2.json` lexically.
            var jsonFiles = Directory.GetFiles(framesPath, "*.json")
                .OrderBy(NaturalSortKey, StringComparer.Ordinal)
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
                    // Unity's built-in JsonUtility cannot deserialize properties or
                    // Dictionary<,>, both of which the *Lite DTOs rely on. Use Newtonsoft.
                    var perception = JsonConvert.DeserializeObject<PerceptionLite>(json);
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
                PerceptionUpdated?.Invoke(ClonePerception(current));
            }
        }

        /// <summary>
        /// Executes a tool in mock mode and returns a result.
        /// </summary>
        public Task<ToolExecutionResultDto> ExecuteToolAsync(
            string toolId,
            Dictionary<string, object> args,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ExecuteToolMockSync(toolId, args));
        }

        private ToolExecutionResultDto ExecuteToolMockSync(string toolId, Dictionary<string, object> args)
        {
            if (frames.Count == 0)
                return new ToolExecutionResultDto { Success = false, Message = "No frames loaded" };

            var current = frames[currentFrameIndex];
            if (current == null)
                return new ToolExecutionResultDto { Success = false, Message = "No current frame" };

            bool handlerSucceeded;
            string message;

            switch (toolId.ToLower())
            {
                case "move":
                    handlerSucceeded = HandleMove(current, args);
                    message = handlerSucceeded ? "Executed move" : "Move: invalid or missing 'direction'";
                    break;
                case "rotate":
                    handlerSucceeded = HandleRotate(current, args);
                    message = handlerSucceeded ? "Executed rotate" : "Rotate: invalid or missing 'clockwise'";
                    break;
                case "changelevel":
                    handlerSucceeded = HandleChangeLevel(current, args);
                    message = handlerSucceeded ? "Executed changelevel" : "ChangeLevel: invalid or missing 'up'";
                    break;
                default:
                    handlerSucceeded = false;
                    message = $"Unknown tool: {toolId}";
                    break;
            }

            // Clone before raising so subscribers can safely snapshot the value;
            // the mock continues to mutate `current` in place across calls.
            if (handlerSucceeded)
            {
                PerceptionUpdated?.Invoke(ClonePerception(current));
            }

            return new ToolExecutionResultDto
            {
                Success = handlerSucceeded,
                Message = message
            };
        }

        private bool HandleMove(PerceptionLite perception, Dictionary<string, object> args)
        {
            if (!args.TryGetValue("direction", out var directionObj))
                return false;

            var direction = directionObj?.ToString()?.ToLower();
            if (string.IsNullOrEmpty(direction))
                return false;

            // Resolve heading-relative names ("forward"/"backward"/"left"/"right")
            // to absolute cardinals based on PlayerHeading. Absolute names pass through.
            var absolute = ResolveDirection(direction, perception.PlayerHeading);
            if (absolute == null)
                return false;

            var loc = perception.PlayerLocation;
            switch (absolute)
            {
                case WorldDirectionLite.North: loc.Y += 1; return true;
                case WorldDirectionLite.South: loc.Y -= 1; return true;
                case WorldDirectionLite.East:  loc.X += 1; return true;
                case WorldDirectionLite.West:  loc.X -= 1; return true;
                default: return false;
            }
        }

        private bool HandleRotate(PerceptionLite perception, Dictionary<string, object> args)
        {
            if (!args.TryGetValue("clockwise", out var clockwiseObj) || clockwiseObj is not bool clockwise)
                return false;

            perception.HeadingDegrees = clockwise
                ? (perception.HeadingDegrees + 90) % 360
                : (perception.HeadingDegrees - 90 + 360) % 360;
            perception.PlayerHeading = DegreesToDirection(perception.HeadingDegrees);
            return true;
        }

        private bool HandleChangeLevel(PerceptionLite perception, Dictionary<string, object> args)
        {
            if (!args.TryGetValue("up", out var upObj) || upObj is not bool up)
                return false;

            perception.PlayerLocation.Z += up ? 1 : -1;
            return true;
        }

        private static WorldDirectionLite? ResolveDirection(string direction, WorldDirectionLite heading)
        {
            switch (direction)
            {
                case "north": return WorldDirectionLite.North;
                case "south": return WorldDirectionLite.South;
                case "east":  return WorldDirectionLite.East;
                case "west":  return WorldDirectionLite.West;
                case "forward":  return heading;
                case "backward": return Opposite(heading);
                case "right":    return RotateCW(heading);
                case "left":     return RotateCW(Opposite(heading));
                default: return null;
            }
        }

        private static WorldDirectionLite Opposite(WorldDirectionLite d) => d switch
        {
            WorldDirectionLite.North => WorldDirectionLite.South,
            WorldDirectionLite.South => WorldDirectionLite.North,
            WorldDirectionLite.East  => WorldDirectionLite.West,
            WorldDirectionLite.West  => WorldDirectionLite.East,
            _ => d,
        };

        private static WorldDirectionLite RotateCW(WorldDirectionLite d) => d switch
        {
            WorldDirectionLite.North => WorldDirectionLite.East,
            WorldDirectionLite.East  => WorldDirectionLite.South,
            WorldDirectionLite.South => WorldDirectionLite.West,
            WorldDirectionLite.West  => WorldDirectionLite.North,
            _ => d,
        };

        private static PerceptionLite ClonePerception(PerceptionLite source)
        {
            // Round-trip via JSON. The mock is not in a hot path and this guarantees
            // a deep copy without hand-maintaining a Clone() on every Lite type.
            var json = JsonConvert.SerializeObject(source);
            return JsonConvert.DeserializeObject<PerceptionLite>(json) ?? new PerceptionLite();
        }

        private static string NaturalSortKey(string path)
        {
            // Zero-pad every numeric run to 10 digits so lexical compare matches
            // numeric compare for any sane filename length.
            return Regex.Replace(path, "[0-9]+", m => m.Value.PadLeft(10, '0'));
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

