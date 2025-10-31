using System;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Management;
using Orleans;

namespace Aetherium.Server.Agents
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
        private MicrosoftAgentAdapter? _adapter;

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
            {
                Console.WriteLine($"[AgentRunner {this.GetPrimaryKeyString()}] Step skipped: No session or management grain");
                return;
            }

            // Pull perception (JSON)
            var p = await _mgmt.GetPerceptionAsync(_sessionId);
            if (p == null)
            {
                Console.WriteLine($"[AgentRunner {this.GetPrimaryKeyString()}] Step skipped: No perception for session {_sessionId}");
                return;
            }

            OperationResult result;
            string actionSummary;

            var llmEnabled = string.Equals(Environment.GetEnvironmentVariable("AGENT_LLM_ENABLED"), "1", StringComparison.OrdinalIgnoreCase);
            if (llmEnabled)
            {
                _adapter ??= new MicrosoftAgentAdapter();
                var decision = await _adapter.DecideAsync(p, CancellationToken.None);
                var act = (decision.Action ?? string.Empty).Trim().ToLowerInvariant();
                var args = decision.Args ?? new System.Collections.Generic.Dictionary<string, string>();

                switch (act)
                {
                    case "move":
                        var dir = args.TryGetValue("direction", out var d) ? d : "F";
                        actionSummary = $"move {dir}";
                        result = await _mgmt.MoveAsync(_sessionId, dir);
                        break;
                    case "pickup":
                        var pickId = args.TryGetValue("targetEntityId", out var pe) ? pe : string.Empty;
                        actionSummary = $"pickup {pickId}";
                        result = await _mgmt.PickupAsync(_sessionId, pickId);
                        break;
                    case "drop":
                        var dropId = args.TryGetValue("itemEntityId", out var de) ? de : string.Empty;
                        actionSummary = $"drop {dropId}";
                        result = await _mgmt.DropAsync(_sessionId, dropId);
                        break;
                    case "open":
                        var openId = args.TryGetValue("targetEntityId", out var oe) ? oe : string.Empty;
                        actionSummary = $"open {openId}";
                        result = await _mgmt.OpenAsync(_sessionId, openId);
                        break;
                    case "close":
                        var closeId = args.TryGetValue("targetEntityId", out var ce) ? ce : string.Empty;
                        actionSummary = $"close {closeId}";
                        result = await _mgmt.CloseAsync(_sessionId, closeId);
                        break;
                    case "use":
                        var itemId = args.TryGetValue("itemEntityId", out var ie) ? ie : string.Empty;
                        var onId = args.TryGetValue("onEntityId", out var oe2) ? oe2 : string.Empty;
                        actionSummary = $"use {itemId} on {onId}";
                        result = await _mgmt.UseAsync(_sessionId, itemId, onId);
                        break;
                    default:
                        actionSummary = "move F";
                        result = await _mgmt.MoveAsync(_sessionId, "F");
                        break;
                }
            }
            else
            {
                // Heuristic: try to move forward; if blocked, turn right then move
                actionSummary = "move F";
                result = await _mgmt.MoveAsync(_sessionId, "F");
                if (!result.Success)
                {
                    actionSummary = "move R";
                    result = await _mgmt.MoveAsync(_sessionId, "R");
                }
            }

            _steps++;
            _lastAction = actionSummary;
            _lastResult = result.Success ? "ok" : result.Message;
            
            var debug = string.Equals(Environment.GetEnvironmentVariable("AGENT_DEBUG"), "1", StringComparison.OrdinalIgnoreCase);
            if (debug || !result.Success)
            {
                var policy = llmEnabled ? "LLM" : "heuristic";
                var status = result.Success ? "✓" : "✗";
                Console.WriteLine($"[AgentRunner {this.GetPrimaryKeyString()}] Step #{_steps} ({policy}): {status} {actionSummary}" + 
                    (!result.Success ? $" - {result.Message}" : ""));
            }
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



