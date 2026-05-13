using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
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
        private PerceptionMockProvider mockProvider = null!;
#if USE_SIGNALR
        private PerceptionSignalRClient? signalRClient;
#endif
        private bool isLiveMode = false;

        // Cancels in-flight tool calls when the component is destroyed so awaiting
        // continuations don't resume against a torn-down GameObject.
        private readonly CancellationTokenSource lifetimeCts = new CancellationTokenSource();

        public event Action<PerceptionLite>? PerceptionUpdated;

        public PerceptionLite? CurrentPerception => currentProvider?.GetCurrent();

        private void Awake()
        {
            mockProvider = new PerceptionMockProvider();
            // Default to offline mode (subscribes the mock provider).
            SetMode(false);
        }

        private void OnDestroy()
        {
            try
            {
                lifetimeCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed; nothing to do.
            }

            if (mockProvider != null)
            {
                mockProvider.PerceptionUpdated -= OnPerceptionUpdated;
            }

#if USE_SIGNALR
            if (signalRClient != null)
            {
                signalRClient.PerceptionUpdated -= OnPerceptionUpdated;
                signalRClient.Dispose();
                signalRClient = null;
            }
#endif

            lifetimeCts.Dispose();
        }

        /// <summary>
        /// Sets the mode to Live (true) or Offline Mock (false). Subscribes to
        /// exactly one provider at a time so the two can't both fire events.
        /// </summary>
        public void SetMode(bool live)
        {
            if (currentProvider != null && isLiveMode == live)
                return;

            // Detach from whichever provider was previously active.
            DetachCurrentProvider();
            isLiveMode = live;

#if USE_SIGNALR
            if (live)
            {
                if (signalRClient == null)
                {
                    signalRClient = new PerceptionSignalRClient("http://localhost:5000/gamehub");
                }
                AttachProvider(signalRClient);
                StartCoroutine(ConnectSignalRAsync());
                return;
            }
#else
            if (live)
            {
                Debug.LogWarning("Live mode requested but USE_SIGNALR is not defined. Falling back to Offline Mock mode.");
                isLiveMode = false;
            }
#endif

            AttachProvider(mockProvider);
        }

        private void AttachProvider(IPerceptionProvider provider)
        {
            currentProvider = provider;
            provider.PerceptionUpdated += OnPerceptionUpdated;
        }

        private void DetachCurrentProvider()
        {
            if (currentProvider != null)
            {
                currentProvider.PerceptionUpdated -= OnPerceptionUpdated;
                currentProvider = null;
            }
        }

#if USE_SIGNALR
        private IEnumerator ConnectSignalRAsync()
        {
            if (signalRClient == null)
                yield break;

            var task = signalRClient.ConnectAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                Debug.LogException(task.Exception ?? new Exception("SignalR connect failed"));
                SetMode(false);
            }
            else if (task.IsCanceled)
            {
                Debug.LogWarning("SignalR connect was cancelled. Falling back to Offline Mock mode.");
                SetMode(false);
            }
        }
#endif

        /// <summary>
        /// Fire-and-forget tool execution. Logs faults to the Unity console so
        /// errors don't disappear into the unobserved-exception finalizer.
        /// </summary>
        public void ExecuteTool(string toolId, Dictionary<string, object> args)
        {
            _ = ExecuteToolAsync(toolId, args)
                .ContinueWith(
                    t => Debug.LogException(t.Exception!),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// Executes a tool against the active provider. The returned task is
        /// linked to the component lifetime; it will fault with
        /// <see cref="OperationCanceledException"/> if the GameObject is destroyed mid-flight.
        /// </summary>
        public Task<ToolExecutionResultDto> ExecuteToolAsync(string toolId, Dictionary<string, object> args)
            => ExecuteToolAsync(toolId, args, CancellationToken.None);

        public async Task<ToolExecutionResultDto> ExecuteToolAsync(
            string toolId,
            Dictionary<string, object> args,
            CancellationToken cancellationToken)
        {
            var provider = currentProvider;
            if (provider == null)
            {
                return new ToolExecutionResultDto { Success = false, Message = "No provider configured" };
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCts.Token, cancellationToken);
            return await provider.ExecuteToolAsync(toolId, args, linked.Token);
        }

        private void OnPerceptionUpdated(PerceptionLite perception)
        {
            PerceptionUpdated?.Invoke(perception);
        }
    }
}
