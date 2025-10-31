using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Management;
using Aetherium.Server.Agents.Tools;
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

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var key = this.GetPrimaryKeyString();
            Console.WriteLine($"[AgentRunner] Activated {key}");
            
            // Get tool registry from service provider
            _toolRegistry = ServiceProvider.GetService(typeof(AgentToolRegistry)) as AgentToolRegistry;
            
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

            ToolExecutionResult result;
            string actionSummary;

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
                
                if (!mgmtResult.Success)
                {
                    mgmtResult = await _mgmt.MoveAsync(_sessionId, "R");
                    result = mgmtResult.Success ? ToolExecutionResult.Ok("Moved R") : ToolExecutionResult.Error(mgmtResult.Message);
                    actionSummary = "move R (legacy)";
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



