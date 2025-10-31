using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Agents.Telemetry;
using Aetherium.Server.Hubs;
using Aetherium.Server.Management;
using Aetherium.Server.Agents.Tools;
using Microsoft.AspNetCore.SignalR;
using Orleans;

namespace Aetherium.Server.Agents
{
    /// <summary>
    /// Simple runner that pulls perception and executes actions using the tool system.
    /// Supports both LLM-driven and heuristic policies.
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
        private AgentToolRegistry? _toolRegistry;
        private AgentToolProfile? _toolProfile;
        private IAgentTelemetryGrain? _telemetryGrain;
        private ReplayData? _currentReplay;
        private IHubContext<Hubs.AgentDashboardHub>? _dashboardHub;

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var key = this.GetPrimaryKeyString();
            Console.WriteLine($"[AgentRunner] Activated {key}");
            
            // Get tool registry from service provider
            _toolRegistry = ServiceProvider.GetService(typeof(AgentToolRegistry)) as AgentToolRegistry;
            
            // Get dashboard hub context from service provider
            _dashboardHub = ServiceProvider.GetService(typeof(IHubContext<Hubs.AgentDashboardHub>)) as IHubContext<Hubs.AgentDashboardHub>;
            
            // Default to Player profile (for all game characters/NPCs)
            // Can be changed to WorldBuilder/NarrativeDesigner for world-building agents
            _toolProfile = AgentToolProfile.Player;
            
            return base.OnActivateAsync(cancellationToken);
        }

        public async Task<bool> AttachAsync(string sessionId, string agentId)
        {
            _sessionId = sessionId;
            _agentId = agentId;
            _mgmt = GrainFactory.GetGrain<IGameManagementGrain>("GLOBAL");
            
            // Get telemetry grain for this agent
            _telemetryGrain = GrainFactory.GetGrain<IAgentTelemetryGrain>(agentId);

            // Verify session exists
            var status = await _mgmt.GetVisionStatusAsync(sessionId);
            return status != null;
        }

        public Task DetachAsync()
        {
            // Store current replay if it exists and was a failure
            if (_currentReplay != null && _currentReplay.Steps.Any())
            {
                var lastStep = _currentReplay.Steps.LastOrDefault();
                if (lastStep != null && !lastStep.Succeeded && _telemetryGrain != null)
                {
                    _currentReplay.FailureReason = "Session ended with failed action";
                    // Fire and forget - don't await in synchronous method
                    _ = _telemetryGrain.RecordFailedRunAsync(_currentReplay);
                }
                _currentReplay = null;
            }

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

            var stepStartTime = Stopwatch.StartNew();
            var perceptionComplexity = EstimatePerceptionComplexity(p);
            ToolExecutionResult result;
            string actionSummary;
            string actionType = "unknown";

            var llmEnabled = string.Equals(Environment.GetEnvironmentVariable("AGENT_LLM_ENABLED"), "1", StringComparison.OrdinalIgnoreCase);
            
            if (llmEnabled && _toolRegistry != null && _toolProfile != null)
            {
                // LLM-driven execution using tool system
                _adapter ??= new MicrosoftAgentAdapter();
                
                // Get available tools for this agent
                var availableTools = _toolRegistry.GetToolsForProfile(_toolProfile).ToList();
                
                var decision = await _adapter.DecideAsync(p, availableTools, CancellationToken.None);
                var tool = _toolRegistry.GetTool(decision.Action ?? "move");
                
                if (tool != null && _toolProfile.IsToolAllowed(tool))
                {
                    // Create execution context
                    var context = new ToolExecutionContext
                    {
                        SessionId = _sessionId,
                        AgentId = _agentId,
                        ManagementGrain = _mgmt,
                        GrantedCapabilities = _toolProfile.GrantedCapabilities,
                        ServiceProvider = ServiceProvider
                    };
                    
                    // Convert string args to object args
                    var objectArgs = decision.Args?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) 
                        ?? new Dictionary<string, object>();
                    
                    result = await tool.ExecuteAsync(context, objectArgs);
                    actionSummary = $"{tool.ToolId} {string.Join(", ", objectArgs.Select(kvp => $"{kvp.Key}={kvp.Value}"))}";
                    actionType = tool.ToolId;
                }
                else
                {
                    // Fallback to move forward if tool not found or not allowed
                    var moveTool = _toolRegistry.GetTool("move");
                    if (moveTool != null)
                    {
                        var context = new ToolExecutionContext
                        {
                            SessionId = _sessionId,
                            AgentId = _agentId,
                            ManagementGrain = _mgmt,
                            GrantedCapabilities = _toolProfile.GrantedCapabilities,
                            ServiceProvider = ServiceProvider
                        };
                        result = await moveTool.ExecuteAsync(context, new Dictionary<string, object> { ["direction"] = "F" });
                        actionSummary = "move F (fallback)";
                        actionType = "move";
                    }
                    else
                    {
                        result = ToolExecutionResult.Error("No tools available");
                        actionSummary = "error";
                    }
                }
            }
            else if (_toolRegistry != null)
            {
                // Heuristic execution using tool system: try to move forward, if blocked turn right
                var moveTool = _toolRegistry.GetTool("move");
                if (moveTool != null)
                {
                    var context = new ToolExecutionContext
                    {
                        SessionId = _sessionId,
                        AgentId = _agentId,
                        ManagementGrain = _mgmt,
                        GrantedCapabilities = new HashSet<string> { "basic_movement" },
                        ServiceProvider = ServiceProvider
                    };
                    
                    result = await moveTool.ExecuteAsync(context, new Dictionary<string, object> { ["direction"] = "F" });
                    actionSummary = "move F";
                    actionType = "move";

                    if (!result.Success)
                    {
                        result = await moveTool.ExecuteAsync(context, new Dictionary<string, object> { ["direction"] = "R" });
                        actionSummary = "move R";
                    }
                }
                else
                {
                    result = ToolExecutionResult.Error("Move tool not available");
                    actionSummary = "error";
                }
            }
            else
            {
                // Fallback to direct management grain calls if tool system not available
                var mgmtResult = await _mgmt.MoveAsync(_sessionId, "F");
                result = mgmtResult.Success ? ToolExecutionResult.Ok("Moved F") : ToolExecutionResult.Error(mgmtResult.Message);
                actionSummary = "move F (legacy)";
                actionType = "move";
                
                if (!mgmtResult.Success)
                {
                    mgmtResult = await _mgmt.MoveAsync(_sessionId, "R");
                    result = mgmtResult.Success ? ToolExecutionResult.Ok("Moved R") : ToolExecutionResult.Error(mgmtResult.Message);
                    actionSummary = "move R (legacy)";
                }
            }

            stepStartTime.Stop();
            _steps++;
            _lastAction = actionSummary;
            _lastResult = result.Success ? "ok" : result.Message;

            // Record telemetry snapshot
            if (_telemetryGrain != null && _agentId != null && _sessionId != null)
            {
                var snapshot = new PerformanceSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    StepNumber = _steps,
                    AgentId = _agentId,
                    SessionId = _sessionId,
                    ActionType = actionType,
                    ActionSummary = actionSummary,
                    ActionSucceeded = result.Success,
                    ErrorMessage = result.Success ? null : result.Message,
                    DecisionLatencyMs = stepStartTime.ElapsedMilliseconds,
                    PerceptionComplexity = perceptionComplexity,
                    Metadata = new Dictionary<string, object>
                    {
                        ["policy"] = llmEnabled ? "LLM" : "heuristic",
                        ["tool_registry_available"] = _toolRegistry != null
                    }
                };

                await _telemetryGrain.RecordSnapshotAsync(snapshot);

                // Broadcast telemetry update via SignalR if hub context available
                if (_dashboardHub != null && _agentId != null)
                {
                    var analysis = await _telemetryGrain.GetAnalysisAsync();
                    if (analysis != null)
                    {
                        await _dashboardHub.Clients.Group($"agent:{_agentId}").SendAsync("TelemetryUpdate", analysis);
                    }
                }
            }

            // Track replay data if action failed
            if (!result.Success && _agentId != null && _sessionId != null)
            {
                if (_currentReplay == null)
                {
                    _currentReplay = new ReplayData
                    {
                        AgentId = _agentId,
                        SessionId = _sessionId,
                        CreatedAt = DateTime.UtcNow,
                        Steps = new List<ReplayStep>()
                    };
                }

                _currentReplay.Steps.Add(new ReplayStep
                {
                    StepNumber = _steps,
                    ActionType = actionType,
                    ActionSummary = actionSummary,
                    ActionArgs = new Dictionary<string, object>(),
                    Succeeded = false,
                    PerceptionJson = p,
                    Timestamp = DateTime.UtcNow
                });

                _currentReplay.FailureReason = result.Message;
                _currentReplay.TotalSteps = _steps;

                // Store replay if this is a critical failure
                if (_currentReplay.Steps.Count(s => !s.Succeeded) >= 3 && _telemetryGrain != null)
                {
                    var replayId = await _telemetryGrain.RecordFailedRunAsync(_currentReplay);
                    
                    // Broadcast replay storage event via SignalR if hub context available
                    if (_dashboardHub != null && _agentId != null)
                    {
                        await _dashboardHub.Clients.Group($"agent:{_agentId}").SendAsync("ReplayStored", replayId, _currentReplay);
                    }
                    
                    _currentReplay = null; // Reset for next failure sequence
                }
            }
            else if (result.Success)
            {
                // Clear replay tracking on success
                _currentReplay = null;
            }
            
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

        public async Task<PerformanceAnalysis?> GetTelemetryAsync()
        {
            if (_telemetryGrain == null || _agentId == null)
                return null;

            return await _telemetryGrain.GetAnalysisAsync();
        }

        private int EstimatePerceptionComplexity(string perceptionJson)
        {
            if (string.IsNullOrWhiteSpace(perceptionJson))
                return 0;

            // Simple heuristic: count entities, items, and affordances mentioned in JSON
            // This is a rough estimate - in production, could parse JSON properly
            var complexity = 0;
            
            // Count occurrences of common entity/item patterns
            complexity += CountOccurrences(perceptionJson, "\"id\":");
            complexity += CountOccurrences(perceptionJson, "\"entityId\":");
            complexity += CountOccurrences(perceptionJson, "\"items\":");
            complexity += CountOccurrences(perceptionJson, "\"affordances\":");

            return complexity;
        }

        private int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }
    }
}



