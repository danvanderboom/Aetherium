using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Agents.Telemetry;
using Aetherium.Model.Telemetry;
using Aetherium.Server.Hubs;
using Aetherium.Server.Management;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.MultiWorld;
using Microsoft.AspNetCore.SignalR;
using Orleans;
using Orleans.Runtime;

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

        // Live-map attach state. When _mapId is set, the agent occupies a real
        // GameMapGrain as a Character (via JoinPlayerAsync) and acts through
        // _gateway, so its moves fan out to human players. When _mapId is null the
        // agent uses the legacy session path (_mgmt) instead.
        private string? _mapId;
        private IMapMutationGateway? _gateway;

        // Grain-timer run loop. Replaces the previous off-scheduler Task.Run so every
        // StepAsync runs on the grain's activation turn (serialized, no races).
        private IDisposable? _timer;
        private int? _maxSteps;
        private int _stepsAtRunStart;

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
            if (_dashboardHub == null)
            {
                Console.WriteLine($"[AgentRunner {key}] Warning: Dashboard hub context not available - SignalR broadcasting disabled");
            }
            
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
            var attached = status != null;
            
            // Broadcast initial telemetry state to dashboard if attached
            if (attached && _telemetryGrain != null && _dashboardHub != null && _agentId != null)
            {
                var analysis = await _telemetryGrain.GetAnalysisAsync();
                if (analysis != null)
                {
                    await _dashboardHub.Clients.Group($"agent:{_agentId}").SendAsync("TelemetryUpdate", analysis);
                }
            }
            
            return attached;
        }

        /// <summary>
        /// Attaches the agent as a first-class participant in a live, grain-hosted map.
        /// The agent joins the map as a Character (id == agentId, exactly like a
        /// player) and routes its tool actions through a <see cref="GrainMutationGateway"/>,
        /// so its moves mutate canonical world state and fan out to every human player
        /// on the map — it plays the shared world, not a private session copy. Unlike
        /// <see cref="AttachAsync"/> this needs no pre-existing human session.
        /// </summary>
        public async Task<bool> AttachToWorldAsync(string worldId, string mapId, string agentId)
        {
            _agentId = agentId;
            _sessionId = agentId; // the agent's in-world entity id doubles as its session id
            _mapId = mapId;
            _mgmt = null;         // map path uses the gateway, not the legacy management path
            _telemetryGrain = GrainFactory.GetGrain<IAgentTelemetryGrain>(agentId);

            var mapGrain = GrainFactory.GetGrain<IGameMapGrain>(mapId);
            var join = await mapGrain.JoinPlayerAsync(agentId);
            if (!join.Success)
            {
                Console.WriteLine($"[AgentRunner {this.GetPrimaryKeyString()}] AttachToWorld failed: {join.Reason}");
                _mapId = null;
                _sessionId = null;
                _agentId = null;
                return false;
            }

            // Route this agent's tool verbs to the canonical map grain — same gateway a
            // human player gets after JoinWorld — so observers see the agent act.
            _gateway = new GrainMutationGateway(GrainFactory, mapId, agentId);
            return true;
        }

        public async Task DetachAsync()
        {
            // Store current replay if it exists and was a failure
            if (_currentReplay != null && _currentReplay.Steps.Any())
            {
                var lastStep = _currentReplay.Steps.LastOrDefault();
                if (lastStep != null && !lastStep.Succeeded && _telemetryGrain != null)
                {
                    _currentReplay.FailureReason = "Session ended with failed action";
                    // Fire and forget - don't await in synchronous method
                    var replayJson = System.Text.Json.JsonSerializer.Serialize(_currentReplay);
                    _ = _telemetryGrain.RecordFailedRunAsync(replayJson);
                }
                _currentReplay = null;
            }

            // Broadcast final telemetry state before detaching
            if (_telemetryGrain != null && _dashboardHub != null && _agentId != null)
            {
                var analysis = await _telemetryGrain.GetAnalysisAsync();
                if (analysis != null)
                {
                    await _dashboardHub.Clients.Group($"agent:{_agentId}").SendAsync("TelemetryUpdate", analysis);
                }
            }

            _isRunning = false;
            _timer?.Dispose();
            _timer = null;

            // Live-map agents remove their Character so they don't linger as a frozen
            // entity after they stop playing.
            if (_mapId != null && _sessionId != null)
            {
                try
                {
                    await GrainFactory.GetGrain<IGameMapGrain>(_mapId).LeavePlayerAsync(_sessionId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AgentRunner {this.GetPrimaryKeyString()}] LeavePlayer failed: {ex.Message}");
                }
            }

            _sessionId = null;
            _agentId = null;
            _mapId = null;
            _gateway = null;
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

        /// <summary>
        /// Builds a tool-execution context for this agent. On the live-map path it
        /// carries the <see cref="GrainMutationGateway"/> (so tools mutate canonical
        /// state and fan out); on the legacy path it carries the management grain.
        /// </summary>
        private ToolExecutionContext BuildContext(HashSet<string> capabilities) => new ToolExecutionContext
        {
            SessionId = _sessionId!,
            AgentId = _agentId,
            ManagementGrain = _mgmt,
            MutationGateway = _gateway,
            GrantedCapabilities = capabilities,
            ServiceProvider = ServiceProvider
        };

        /// <summary>
        /// Constructs the LLM adapter, wiring in the <see cref="PromptRegistry"/> (when registered)
        /// so the system prompt is rendered from an editable Prompts/*.md template (P3-5). The
        /// template name (default <c>agent_decision</c>) and the agent goal are overridable via the
        /// <c>AGENT_PROMPT_TEMPLATE</c> and <c>AGENT_GOAL</c> environment variables. When no registry
        /// is registered, the adapter falls back to its built-in default prompt.
        /// </summary>
        private MicrosoftAgentAdapter BuildLlmAdapter()
        {
            var registry = ServiceProvider?.GetService(typeof(PromptRegistry)) as PromptRegistry;
            var templateName = Environment.GetEnvironmentVariable("AGENT_PROMPT_TEMPLATE");
            var goal = Environment.GetEnvironmentVariable("AGENT_GOAL");
            return new MicrosoftAgentAdapter(promptRegistry: registry, systemTemplateName: templateName, goal: goal);
        }

        public async Task StepAsync()
        {
            if (_sessionId == null || (_mgmt == null && _mapId == null))
            {
                Console.WriteLine($"[AgentRunner {this.GetPrimaryKeyString()}] Step skipped: not attached");
                return;
            }

            // Pull perception (JSON). Live-map agents read the canonical world from
            // the map grain; legacy agents read their session's mirror via management.
            var p = _mapId != null
                ? await GrainFactory.GetGrain<IGameMapGrain>(_mapId).ComputeAgentPerceptionAsync(_sessionId)
                : await _mgmt!.GetPerceptionAsync(_sessionId);
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
                // LLM-driven execution using tool system. The adapter renders its system prompt
                // from an editable Prompts/*.md template via PromptRegistry (P3-5) when available,
                // so agent behavior is tunable at runtime; template and goal are overridable by env.
                _adapter ??= BuildLlmAdapter();
                
                // Get available tools for this agent
                var availableTools = _toolRegistry.GetToolsForProfile(_toolProfile).ToList();
                
                var decision = await _adapter.DecideAsync(p, availableTools, CancellationToken.None);
                var tool = _toolRegistry.GetTool(decision.Action ?? "move");
                
                if (tool != null && _toolProfile.IsToolAllowed(tool))
                {
                    var context = BuildContext(_toolProfile.GrantedCapabilities);

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
                        var context = BuildContext(_toolProfile.GrantedCapabilities);
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
                    var context = BuildContext(new HashSet<string> { "basic_movement" });

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
            else if (_mgmt != null)
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
            else
            {
                result = ToolExecutionResult.Error("No execution path available");
                actionSummary = "error";
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
                    var replayJson = System.Text.Json.JsonSerializer.Serialize(_currentReplay);
                    var replayId = await _telemetryGrain.RecordFailedRunAsync(replayJson);
                    
                    // Broadcast replay storage event via SignalR if hub context available
                    if (_dashboardHub != null && _agentId != null)
                    {
                        await _dashboardHub.Clients.Group($"agent:{_agentId}").SendAsync("ReplayStored", replayId, replayJson);
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
            _maxSteps = maxSteps;
            _stepsAtRunStart = _steps;

            // Grain timer: each tick runs StepAsync on the grain's activation turn,
            // serialized with every other call to this grain (Interleave = false), so
            // there is no off-scheduler state mutation. The timer self-disposes once
            // the requested step budget is reached or on Stop/Detach.
            var period = TimeSpan.FromMilliseconds(Math.Max(1, stepDelayMs));
            _timer = this.RegisterGrainTimer(
                async _ =>
                {
                    if (!_isRunning) return;
                    try
                    {
                        await StepAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AgentRunner {this.GetPrimaryKeyString()}] Step error: {ex.Message}");
                    }

                    if (_maxSteps.HasValue && (_steps - _stepsAtRunStart) >= _maxSteps.Value)
                    {
                        await StopAsync();
                    }
                },
                state: (object?)null,
                new GrainTimerCreationOptions { DueTime = period, Period = period, Interleave = false });

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            _timer?.Dispose();
            _timer = null;

            // Broadcast final telemetry state when stopping
            if (_telemetryGrain != null && _dashboardHub != null && _agentId != null)
            {
                var analysis = await _telemetryGrain.GetAnalysisAsync();
                if (analysis != null)
                {
                    await _dashboardHub.Clients.Group($"agent:{_agentId}").SendAsync("TelemetryUpdate", analysis);
                }
            }
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



