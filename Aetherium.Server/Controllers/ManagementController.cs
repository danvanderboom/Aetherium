using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Aetherium.Model;
using Aetherium.Server.Agents;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;
using Orleans;

namespace Aetherium.Server.Controllers
{
    /// <summary>
    /// REST API controller for managing worlds, sessions, and agents.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ManagementController : ControllerBase
    {
        private readonly IClusterClient _orleansClient;
        private const string ManagementGrainKey = "GLOBAL";

        public ManagementController(IClusterClient orleansClient)
        {
            _orleansClient = orleansClient;
        }

        private IGameManagementGrain GetManagementGrain()
        {
            return _orleansClient.GetGrain<IGameManagementGrain>(ManagementGrainKey);
        }

        /// <summary>
        /// Gets all worlds.
        /// </summary>
        [HttpGet("worlds")]
        public async Task<ActionResult<List<WorldInfoDto>>> GetWorlds()
        {
            try
            {
                var mgmt = GetManagementGrain();
                var worlds = await mgmt.ListWorldsAsync();
                
                var dtos = worlds.Select(w => new WorldInfoDto
                {
                    WorldId = w.WorldId,
                    Name = w.Name,
                    Description = w.Description,
                    State = w.State.ToString(),
                    PlayerCount = w.PlayerCount,
                    MaxPlayers = w.MaxPlayers,
                    CreatedAt = w.CreatedAt,
                    LastActivityAt = w.LastActivityAt,
                    NarrativeId = w.NarrativeId,
                    MapIds = w.MapIds ?? new List<string>(),
                    ClusterId = w.ClusterId
                }).ToList();

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to list worlds: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets a world by ID.
        /// </summary>
        [HttpGet("worlds/{worldId}")]
        public async Task<ActionResult<WorldInfoDto>> GetWorld(string worldId)
        {
            try
            {
                var mgmt = GetManagementGrain();
                var world = await mgmt.GetWorldInfoAsync(worldId);
                
                if (world == null)
                    return NotFound($"World not found: {worldId}");

                var dto = new WorldInfoDto
                {
                    WorldId = world.WorldId,
                    Name = world.Name,
                    Description = world.Description,
                    State = world.State.ToString(),
                    PlayerCount = world.PlayerCount,
                    MaxPlayers = world.MaxPlayers,
                    CreatedAt = world.CreatedAt,
                    LastActivityAt = world.LastActivityAt,
                    NarrativeId = world.NarrativeId,
                    MapIds = world.MapIds ?? new List<string>(),
                    ClusterId = world.ClusterId
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get world: {ex.Message}" });
            }
        }

        /// <summary>
        /// Creates a new world.
        /// </summary>
        [HttpPost("worlds")]
        public async Task<ActionResult<WorldInfoDto>> CreateWorld([FromBody] CreateWorldRequestDto request)
        {
            try
            {
                var mgmt = GetManagementGrain();
                
                var createRequest = new CreateWorldRequest
                {
                    Name = request.Name,
                    Description = request.Description,
                    GeneratorType = request.GeneratorType,
                    GeneratorParameters = request.GeneratorParameters ?? new Dictionary<string, object>(),
                    NarrativeId = request.NarrativeId,
                    MaxPlayers = request.MaxPlayers,
                    Size = request.Size != null ? new WorldSize
                    {
                        Width = request.Size.Width,
                        Height = request.Size.Height,
                        Depth = request.Size.Depth
                    } : null
                };

                var worldId = await mgmt.CreateWorldAsync(createRequest);
                var world = await mgmt.GetWorldInfoAsync(worldId);
                
                if (world == null)
                    return StatusCode(500, new { error = "World created but could not retrieve info" });

                var dto = new WorldInfoDto
                {
                    WorldId = world.WorldId,
                    Name = world.Name,
                    Description = world.Description,
                    State = world.State.ToString(),
                    PlayerCount = world.PlayerCount,
                    MaxPlayers = world.MaxPlayers,
                    CreatedAt = world.CreatedAt,
                    LastActivityAt = world.LastActivityAt,
                    NarrativeId = world.NarrativeId,
                    MapIds = world.MapIds ?? new List<string>(),
                    ClusterId = world.ClusterId
                };

                return CreatedAtAction(nameof(GetWorld), new { worldId = worldId }, dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to create world: {ex.Message}" });
            }
        }

        /// <summary>
        /// Shuts down a world.
        /// </summary>
        [HttpDelete("worlds/{worldId}")]
        public async Task<ActionResult> ShutdownWorld(string worldId)
        {
            try
            {
                var mgmt = GetManagementGrain();
                var result = await mgmt.ShutdownWorldAsync(worldId);
                
                if (!result.Success)
                    return BadRequest(new { error = result.Message });

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to shutdown world: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets all sessions.
        /// </summary>
        [HttpGet("sessions")]
        public async Task<ActionResult<List<SessionInfoDto>>> GetSessions()
        {
            try
            {
                var mgmt = GetManagementGrain();
                var sessions = await mgmt.ListSessionsAsync();
                
                var dtos = new List<SessionInfoDto>();
                foreach (var session in sessions)
                {
                    // Get attached agents for this session
                    var attachedAgents = await GetAttachedAgentsForSession(session.SessionId);
                    
                    var dto = new SessionInfoDto
                    {
                        SessionId = session.SessionId,
                        ConnectionId = session.ConnectionId,
                        DirectionalVisionMode = session.DirectionalVisionMode,
                        HeadingDegrees = session.HeadingDegrees,
                        FieldOfViewDegrees = session.FieldOfViewDegrees,
                        LightingMode = session.LightingMode,
                        VisionMode = session.VisionMode,
                        TimeScale = session.TimeScale,
                        ConnectedAt = session.ConnectedAt,
                        AttachedAgentIds = attachedAgents.Select(a => a.AgentId).ToList()
                    };
                    
                    dtos.Add(dto);
                }

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to list sessions: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets a session by ID.
        /// </summary>
        [HttpGet("sessions/{sessionId}")]
        public async Task<ActionResult<SessionInfoDto>> GetSession(string sessionId)
        {
            try
            {
                var mgmt = GetManagementGrain();
                var session = await mgmt.GetSessionInfoAsync(sessionId);
                
                if (session == null)
                    return NotFound($"Session not found: {sessionId}");

                var attachedAgents = await GetAttachedAgentsForSession(sessionId);
                
                var dto = new SessionInfoDto
                {
                    SessionId = session.SessionId,
                    ConnectionId = session.ConnectionId,
                    DirectionalVisionMode = session.DirectionalVisionMode,
                    HeadingDegrees = session.HeadingDegrees,
                    FieldOfViewDegrees = session.FieldOfViewDegrees,
                    LightingMode = session.LightingMode,
                    VisionMode = session.VisionMode,
                    TimeScale = session.TimeScale,
                    ConnectedAt = session.ConnectedAt,
                    AttachedAgentIds = attachedAgents.Select(a => a.AgentId).ToList()
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get session: {ex.Message}" });
            }
        }

        /// <summary>
        /// Terminates a session.
        /// </summary>
        [HttpDelete("sessions/{sessionId}")]
        public async Task<ActionResult> StopSession(string sessionId)
        {
            try
            {
                var mgmt = GetManagementGrain();
                var result = await mgmt.TerminateSessionAsync(sessionId);
                
                if (!result.Success)
                    return BadRequest(new { error = result.Message });

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to stop session: {ex.Message}" });
            }
        }

        /// <summary>
        /// Attaches an agent to a session.
        /// </summary>
        [HttpPost("sessions/{sessionId}/attach")]
        public async Task<ActionResult> AttachAgent(string sessionId, [FromBody] AttachAgentRequestDto request)
        {
            try
            {
                var runnerGrain = _orleansClient.GetGrain<IAgentRunnerGrain>(request.RunnerId);
                var success = await runnerGrain.AttachAsync(sessionId, request.AgentId);
                
                if (!success)
                    return BadRequest(new { error = "Failed to attach agent to session" });

                return Ok(new { success = true, message = $"Agent {request.AgentId} attached to session {sessionId}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to attach agent: {ex.Message}" });
            }
        }

        /// <summary>
        /// Detaches an agent from its session.
        /// </summary>
        [HttpPost("sessions/{sessionId}/detach")]
        public async Task<ActionResult> DetachAgent(string sessionId, [FromBody] DetachAgentRequestDto request)
        {
            try
            {
                var runnerGrain = _orleansClient.GetGrain<IAgentRunnerGrain>(request.RunnerId);
                await runnerGrain.DetachAsync();
                
                return Ok(new { success = true, message = $"Agent detached from session {sessionId}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to detach agent: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets all agents.
        /// </summary>
        [HttpGet("agents")]
        public async Task<ActionResult<List<AgentInfoDto>>> GetAgents()
        {
            try
            {
                // Note: We don't have a registry of all agent runners, so we can't list all agents.
                // This is a limitation - we'd need to track agent runners separately.
                // For now, return empty list or agents found via sessions.
                var mgmt = GetManagementGrain();
                var sessions = await mgmt.ListSessionsAsync();
                
                var agents = new List<AgentInfoDto>();
                foreach (var session in sessions)
                {
                    var sessionAgents = await GetAttachedAgentsForSession(session.SessionId);
                    agents.AddRange(sessionAgents);
                }

                // Deduplicate by AgentId
                var uniqueAgents = agents.GroupBy(a => a.AgentId)
                    .Select(g => g.First())
                    .ToList();

                return Ok(uniqueAgents);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to list agents: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets summary statistics.
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetStats()
        {
            try
            {
                var mgmt = GetManagementGrain();
                var sessions = await mgmt.ListSessionsAsync();
                var worlds = await mgmt.ListWorldsAsync();
                
                var agentCount = 0;
                foreach (var session in sessions)
                {
                    var agents = await GetAttachedAgentsForSession(session.SessionId);
                    agentCount += agents.Count;
                }

                return Ok(new
                {
                    ActiveAgents = agentCount,
                    ActiveSessions = sessions.Count,
                    ActiveWorlds = worlds.Count(w => w.State == WorldState.Active),
                    TotalWorlds = worlds.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get stats: {ex.Message}" });
            }
        }

        private async Task<List<AgentInfoDto>> GetAttachedAgentsForSession(string sessionId)
        {
            var agents = new List<AgentInfoDto>();
            
            // Note: We don't have a centralized registry of agent runners.
            // For now, we'll try to query known runner IDs (like "runner-1", "runner-2", etc.)
            // This is a limitation - ideally we'd track agent runners when they're created.
            
            // Try a few common runner IDs
            for (int i = 1; i <= 10; i++)
            {
                try
                {
                    var runnerId = $"runner-{i}";
                    var runnerGrain = _orleansClient.GetGrain<IAgentRunnerGrain>(runnerId);
                    var status = await runnerGrain.GetStatusAsync();
                    
                    if (status.SessionId == sessionId && !string.IsNullOrEmpty(status.AgentId))
                    {
                        agents.Add(new AgentInfoDto
                        {
                            AgentId = status.AgentId,
                            RunnerId = runnerId,
                            SessionId = status.SessionId,
                            IsRunning = status.IsRunning,
                            Steps = status.Steps,
                            LastAction = status.LastAction,
                            LastResult = status.LastResult
                        });
                    }
                }
                catch
                {
                    // Runner doesn't exist or failed - continue
                    continue;
                }
            }
            
            return agents;
        }
    }
}

