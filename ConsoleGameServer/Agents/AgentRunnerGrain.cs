using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleGameServer.Management;
using Orleans;

namespace ConsoleGameServer.Agents
{
    /// <summary>
    /// Simple runner that pulls perception and executes a heuristic policy.
    /// Pluggable with Microsoft Agent Framework in future iterations.
    /// </summary>
    public class AgentRunnerGrain : Grain, IAgentRunnerGrain
    {
        private string? _sessionId;
        private string? _agentId;
        private bool _isRunning;
        private int _steps;
        private string _lastAction = string.Empty;
        private string _lastResult = string.Empty;
        private CancellationTokenSource? _cts;

        private IGameManagementGrain? _mgmt;

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var key = this.GetPrimaryKeyString();
            Console.WriteLine($"[AgentRunner] Activated {key}");
            return base.OnActivateAsync(cancellationToken);
        }

        public async Task<bool> AttachAsync(string sessionId, string agentId)
        {
            _sessionId = sessionId;
            _agentId = agentId;
            _mgmt = GrainFactory.GetGrain<IGameManagementGrain>("GLOBAL");

            // Verify session exists
            var status = await _mgmt.GetVisionStatusAsync(sessionId);
            return status != null;
        }

        public Task DetachAsync()
        {
            _sessionId = null;
            _agentId = null;
            _isRunning = false;
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        public Task<RunnerStatus> GetStatusAsync()
        {
            return Task.FromResult(new RunnerStatus
            {
                SessionId = _sessionId,
                AgentId = _agentId,
                IsRunning = _isRunning,
                Steps = _steps,
                LastAction = _lastAction,
                LastResult = _lastResult
            });
        }

        public async Task StepAsync()
        {
            if (_mgmt == null || _sessionId == null)
                return;

            // Pull perception
            var p = await _mgmt.GetPerceptionAsync(_sessionId);
            if (p == null)
                return;

            // Heuristic: try to move forward; if blocked repeatedly, turn right move
            var action = "move F";
            var result = await _mgmt.MoveAsync(_sessionId, "F");
            if (!result.Success)
            {
                action = "move R";
                result = await _mgmt.MoveAsync(_sessionId, "R");
            }

            _steps++;
            _lastAction = action;
            _lastResult = result.Success ? "ok" : result.Message;
        }

        public Task RunAsync(int? maxSteps = null, int stepDelayMs = 200)
        {
            if (_isRunning)
                return Task.CompletedTask;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    var remaining = maxSteps ?? int.MaxValue;
                    while (_isRunning && remaining-- > 0 && !token.IsCancellationRequested)
                    {
                        await StepAsync();
                        await Task.Delay(stepDelayMs, token);
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AgentRunner] Run loop error: {ex.Message}");
                }
                finally
                {
                    _isRunning = false;
                }
            }, token);

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _isRunning = false;
            _cts?.Cancel();
            return Task.CompletedTask;
        }
    }
}


