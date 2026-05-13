#if USE_SIGNALR
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Unity.Model;
using UnityEngine;

namespace Aetherium.Unity.Networking
{
    /// <summary>
    /// SignalR client provider for Live mode connection to GameHub.
    /// Only compiled when USE_SIGNALR scripting define is set.
    /// </summary>
    public class PerceptionSignalRClient : IPerceptionProvider, IDisposable
    {
        // Note: Microsoft.AspNetCore.SignalR.Client would need to be added as a package
        // For now, this is a stub implementation showing the interface

        private readonly string serverUrl;
        private PerceptionLite? currentPerception;

        // Captured from ConnectAsync() so background SignalR callbacks can marshal
        // event invocations back onto Unity's main thread. Without this, anything
        // subscribed via PerceptionUpdated that touches UnityEngine API throws.
        private SynchronizationContext? unitySyncContext;

        public event Action<PerceptionLite>? PerceptionUpdated;

        public PerceptionSignalRClient(string serverUrl)
        {
            this.serverUrl = serverUrl;
        }

        public PerceptionLite? GetCurrent()
        {
            return currentPerception;
        }

        public async Task ConnectAsync()
        {
            // Capture before any awaits so we get Unity's sync context, not a pool thread's.
            unitySyncContext = SynchronizationContext.Current;

            try
            {
                // TODO: Implement SignalR connection
                // var connection = new HubConnectionBuilder()
                //     .WithUrl($"{serverUrl}")
                //     .WithAutomaticReconnect()
                //     .Build();

                // connection.On<PerceptionDto>("ReceivePerceptionUpdate", OnPerceptionReceived);
                // await connection.StartAsync();

                Debug.LogWarning("SignalR client connection not fully implemented. Requires Microsoft.AspNetCore.SignalR.Client package.");
                Debug.Log($"Would connect to: {serverUrl}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to connect to SignalR server: {ex.Message}");
                throw;
            }
        }

        public async Task<ToolExecutionResultDto> ExecuteToolAsync(
            string toolId,
            Dictionary<string, object> args,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // TODO: Implement tool execution
                // return await connection.InvokeAsync<ToolExecutionResultDto>("ExecuteTool", toolId, args, cancellationToken);

                Debug.LogWarning("SignalR tool execution not fully implemented.");
                Debug.Log($"Would execute tool: {toolId} with args: {string.Join(", ", args.Keys)}");

                await Task.CompletedTask;
                return new ToolExecutionResultDto
                {
                    Success = false,
                    Message = "SignalR tool execution not fully implemented"
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to execute tool: {ex.Message}");
                return new ToolExecutionResultDto
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public void Disconnect()
        {
            // TODO: Dispose SignalR connection
            Debug.Log("SignalR client disconnected (stub)");
        }

        public void Dispose()
        {
            Disconnect();
        }

        private void OnPerceptionReceived(object perceptionDto)
        {
            // SignalR fires callbacks on a thread-pool thread; marshal back to
            // Unity's main thread before raising the event so subscribers can
            // safely touch UnityEngine APIs.
            void Raise()
            {
                // TODO: Convert server PerceptionDto to PerceptionLite (e.g. via Newtonsoft).
                // currentPerception = ConvertDtoToLite(perceptionDto);
                // if (currentPerception != null) PerceptionUpdated?.Invoke(currentPerception);

                Debug.LogWarning("Perception update conversion not implemented. Requires full DTO mapping.");
            }

            if (unitySyncContext != null)
            {
                unitySyncContext.Post(_ => Raise(), null);
            }
            else
            {
                Raise();
            }
        }
    }
}
#endif
