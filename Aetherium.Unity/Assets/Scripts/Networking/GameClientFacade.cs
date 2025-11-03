using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Unity.Model;
using UnityEngine;

namespace Aetherium.Unity.Networking
{
    /// <summary>
    /// Facade that manages perception providers and tool execution.
    /// Switches between Offline Mock and Live modes.
    /// </summary>
    public class GameClientFacade : MonoBehaviour
    {
        private IPerceptionProvider? currentProvider;
        private PerceptionMockProvider mockProvider;
#if USE_SIGNALR
        private PerceptionSignalRClient? signalRClient;
#endif
        private bool isLiveMode = false;

        public event Action<PerceptionLite>? PerceptionUpdated;

        public PerceptionLite? CurrentPerception => currentProvider?.GetCurrent();

        private void Awake()
        {
            mockProvider = new PerceptionMockProvider();
            mockProvider.PerceptionUpdated += OnPerceptionUpdated;
            
            // Default to offline mode
            SetMode(false);
        }

        private void OnDestroy()
        {
            if (mockProvider != null)
            {
                mockProvider.PerceptionUpdated -= OnPerceptionUpdated;
            }

#if USE_SIGNALR
            if (signalRClient != null)
            {
                signalRClient.PerceptionUpdated -= OnPerceptionUpdated;
                signalRClient.Disconnect();
            }
#endif
        }

        /// <summary>
        /// Sets the mode to Live (true) or Offline Mock (false).
        /// </summary>
        public void SetMode(bool live)
        {
            if (isLiveMode == live)
                return;

            isLiveMode = live;

            if (currentProvider != null && currentProvider != mockProvider)
            {
                currentProvider.PerceptionUpdated -= OnPerceptionUpdated;
            }

#if USE_SIGNALR
            if (live)
            {
                if (signalRClient == null)
                {
                    signalRClient = new PerceptionSignalRClient("http://localhost:5000/gamehub");
                }
                signalRClient.PerceptionUpdated += OnPerceptionUpdated;
                currentProvider = signalRClient;
                
                StartCoroutine(ConnectSignalRAsync());
            }
            else
            {
                if (signalRClient != null)
                {
                    signalRClient.PerceptionUpdated -= OnPerceptionUpdated;
                }
                currentProvider = mockProvider;
            }
#else
            if (live)
            {
                Debug.LogWarning("Live mode requested but USE_SIGNALR is not defined. Falling back to Offline Mock mode.");
                currentProvider = mockProvider;
            }
            else
            {
                currentProvider = mockProvider;
            }
#endif
        }

#if USE_SIGNALR
        private IEnumerator ConnectSignalRAsync()
        {
            if (signalRClient == null)
                yield break;

            var task = signalRClient.ConnectAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            if (!task.IsCompletedSuccessfully)
            {
                Debug.LogError("Failed to connect to SignalR server. Falling back to Offline Mock mode.");
                SetMode(false);
            }
        }
#endif

        /// <summary>
        /// Executes a tool with the specified ID and arguments.
        /// In Offline mode, mutates local mock state.
        /// In Live mode, sends command to server.
        /// </summary>
        public void ExecuteTool(string toolId, Dictionary<string, object> args)
        {
            // Fire-and-forget async version for backward compatibility
            _ = ExecuteToolAsync(toolId, args);
        }

        /// <summary>
        /// Executes a tool with the specified ID and arguments asynchronously, returning the result.
        /// In Offline mode, mutates local mock state and returns a success result.
        /// In Live mode, sends command to server and returns the server's result.
        /// </summary>
        public async Task<ToolExecutionResultDto> ExecuteToolAsync(string toolId, Dictionary<string, object> args)
        {
            if (isLiveMode)
            {
#if USE_SIGNALR
                if (signalRClient != null)
                {
                    return await signalRClient.ExecuteToolAsync(toolId, args);
                }
                else
                {
                    Debug.LogError("SignalR client not initialized. Falling back to mock.");
                    return mockProvider.ExecuteToolMock(toolId, args);
                }
#else
                Debug.LogWarning("ExecuteToolAsync called in Live mode but USE_SIGNALR is not defined. Using mock.");
                return mockProvider.ExecuteToolMock(toolId, args);
#endif
            }
            else
            {
                return mockProvider.ExecuteToolMock(toolId, args);
            }
        }

        private void OnPerceptionUpdated(PerceptionLite perception)
        {
            PerceptionUpdated?.Invoke(perception);
        }
    }
}

