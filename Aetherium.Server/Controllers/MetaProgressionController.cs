using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Aetherium.Server.MetaProgression;
using Orleans;

namespace Aetherium.Server.Controllers
{
    /// <summary>
    /// REST API controller for managing meta-progression (discoveries, unlocks, generator filtering).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MetaProgressionController : ControllerBase
    {
        private readonly IClusterClient _orleansClient;

        public MetaProgressionController(IClusterClient orleansClient)
        {
            _orleansClient = orleansClient;
        }

        /// <summary>
        /// Gets meta-progression state for a player.
        /// </summary>
        [HttpGet("{playerId}")]
        public async Task<ActionResult<MetaProgressionStateDto>> GetState(string playerId)
        {
            try
            {
                var metaProgGrain = _orleansClient.GetGrain<IMetaProgressionGrain>(playerId);
                var state = await metaProgGrain.GetStateAsync();

                if (state == null)
                    return NotFound($"Meta-progression state not found for player: {playerId}");

                var dto = new MetaProgressionStateDto
                {
                    PlayerId = state.PlayerId,
                    DiscoveredWorldTemplates = state.DiscoveredWorldTemplates.ToList(),
                    DiscoveredTags = state.DiscoveredTags.ToList(),
                    VisitedWorldIds = state.VisitedWorldIds.ToList(),
                    VisitedMapIds = state.VisitedMapIds.ToList(),
                    CompletedQuestIds = state.CompletedQuestIds.ToList(),
                    CompletedCrossWorldQuestIds = state.CompletedCrossWorldQuestIds.ToList(),
                    UnlockedGenerators = state.UnlockedGenerators.ToList(),
                    TagVisitCounts = state.TagVisitCounts,
                    CreatedAt = state.CreatedAt,
                    LastUpdatedAt = state.LastUpdatedAt
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get meta-progression state: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets all discoveries for a player.
        /// </summary>
        [HttpGet("{playerId}/discoveries")]
        public async Task<ActionResult<DiscoveriesDto>> GetDiscoveries(string playerId)
        {
            try
            {
                var metaProgGrain = _orleansClient.GetGrain<IMetaProgressionGrain>(playerId);
                var state = await metaProgGrain.GetStateAsync();

                if (state == null)
                    return NotFound($"Meta-progression state not found for player: {playerId}");

                var dto = new DiscoveriesDto
                {
                    VisitedWorldIds = state.VisitedWorldIds.ToList(),
                    VisitedMapIds = state.VisitedMapIds.ToList(),
                    DiscoveredWorldTemplates = state.DiscoveredWorldTemplates.ToList(),
                    DiscoveredTags = state.DiscoveredTags.ToList(),
                    CompletedQuestIds = state.CompletedQuestIds.ToList(),
                    CompletedCrossWorldQuestIds = state.CompletedCrossWorldQuestIds.ToList(),
                    TagVisitCounts = state.TagVisitCounts
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get discoveries: {ex.Message}" });
            }
        }

        /// <summary>
        /// Records a discovery (world/map visit).
        /// </summary>
        [HttpPost("{playerId}/discoveries")]
        public async Task<ActionResult> RecordDiscovery(string playerId, [FromBody] RecordDiscoveryRequest request)
        {
            try
            {
                var metaProgGrain = _orleansClient.GetGrain<IMetaProgressionGrain>(playerId);
                await metaProgGrain.RecordDiscoveryAsync(
                    request.WorldId,
                    request.MapId,
                    request.WorldTemplate,
                    request.Tags);

                return Ok(new { message = "Discovery recorded" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to record discovery: {ex.Message}" });
            }
        }

        /// <summary>
        /// Records a quest completion.
        /// </summary>
        [HttpPost("{playerId}/quests/{questId}/complete")]
        public async Task<ActionResult> RecordQuestCompletion(string playerId, string questId, [FromBody] RecordQuestCompletionRequest? request = null)
        {
            try
            {
                var metaProgGrain = _orleansClient.GetGrain<IMetaProgressionGrain>(playerId);
                var isCrossWorld = request?.IsCrossWorld ?? false;
                
                await metaProgGrain.RecordQuestCompletionAsync(questId, isCrossWorld);

                return Ok(new { message = "Quest completion recorded" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to record quest completion: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets all unlocked generators for a player.
        /// </summary>
        [HttpGet("{playerId}/unlocks")]
        public async Task<ActionResult<List<string>>> GetUnlocks(string playerId)
        {
            try
            {
                var metaProgGrain = _orleansClient.GetGrain<IMetaProgressionGrain>(playerId);
                var generators = await metaProgGrain.GetAllowedGeneratorsAsync();
                return Ok(generators);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get unlocks: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets all unlocked generators for a player.
        /// </summary>
        [HttpGet("{playerId}/generators")]
        public async Task<ActionResult<List<string>>> GetAllowedGenerators(string playerId)
        {
            try
            {
                var metaProgGrain = _orleansClient.GetGrain<IMetaProgressionGrain>(playerId);
                var generators = await metaProgGrain.GetAllowedGeneratorsAsync();
                return Ok(generators);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get allowed generators: {ex.Message}" });
            }
        }

        /// <summary>
        /// Checks if a generator is unlocked for a player.
        /// </summary>
        [HttpGet("{playerId}/generators/{generatorName}/unlocked")]
        public async Task<ActionResult<GeneratorUnlockStatusDto>> IsGeneratorUnlocked(string playerId, string generatorName)
        {
            try
            {
                var metaProgGrain = _orleansClient.GetGrain<IMetaProgressionGrain>(playerId);
                var isUnlocked = await metaProgGrain.IsGeneratorUnlockedAsync(generatorName);

                var dto = new GeneratorUnlockStatusDto
                {
                    GeneratorName = generatorName,
                    IsUnlocked = isUnlocked
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to check generator unlock status: {ex.Message}" });
            }
        }

        /// <summary>
        /// Evaluates unlock criteria and unlocks new generators if conditions are met.
        /// </summary>
        [HttpPost("{playerId}/unlocks/evaluate")]
        public async Task<ActionResult> EvaluateUnlocks(string playerId)
        {
            try
            {
                var metaProgGrain = _orleansClient.GetGrain<IMetaProgressionGrain>(playerId);
                await metaProgGrain.EvaluateUnlocksAsync();

                return Ok(new { message = "Unlocks evaluated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to evaluate unlocks: {ex.Message}" });
            }
        }

        /// <summary>
        /// Adds an unlock criteria definition.
        /// </summary>
        [HttpPost("{playerId}/unlocks/criteria")]
        public async Task<ActionResult> AddUnlockCriteria(string playerId, [FromBody] UnlockCriteriaDto criteriaDto)
        {
            try
            {
                var metaProgGrain = _orleansClient.GetGrain<IMetaProgressionGrain>(playerId);
                
                var criteria = new UnlockCriteria
                {
                    CriteriaId = criteriaDto.CriteriaId ?? Guid.NewGuid().ToString(),
                    UnlocksGenerator = criteriaDto.UnlocksGenerator,
                    MinWorldVisits = criteriaDto.MinWorldVisits,
                    MinWorldsOfTag = criteriaDto.MinWorldsOfTag,
                    RequiredTag = criteriaDto.RequiredTag,
                    MinCrossWorldQuests = criteriaDto.MinCrossWorldQuests,
                    RequiredQuestIds = criteriaDto.RequiredQuestIds,
                    RequiredWorldTemplates = criteriaDto.RequiredWorldTemplates,
                    TagVisitRequirements = criteriaDto.TagVisitRequirements
                };

                await metaProgGrain.AddUnlockCriteriaAsync(criteria);

                return CreatedAtAction(nameof(GetState), new { playerId }, criteriaDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to add unlock criteria: {ex.Message}" });
            }
        }
    }

    // DTOs
    public class MetaProgressionStateDto
    {
        public string PlayerId { get; set; } = string.Empty;
        public List<string> DiscoveredWorldTemplates { get; set; } = new List<string>();
        public List<string> DiscoveredTags { get; set; } = new List<string>();
        public List<string> VisitedWorldIds { get; set; } = new List<string>();
        public List<string> VisitedMapIds { get; set; } = new List<string>();
        public List<string> CompletedQuestIds { get; set; } = new List<string>();
        public List<string> CompletedCrossWorldQuestIds { get; set; } = new List<string>();
        public List<string> UnlockedGenerators { get; set; } = new List<string>();
        public Dictionary<string, int> TagVisitCounts { get; set; } = new Dictionary<string, int>();
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    public class RecordDiscoveryRequest
    {
        public string WorldId { get; set; } = string.Empty;
        public string MapId { get; set; } = string.Empty;
        public string? WorldTemplate { get; set; }
        public List<string>? Tags { get; set; }
    }

    public class RecordQuestCompletionRequest
    {
        public bool IsCrossWorld { get; set; }
    }

    public class GeneratorUnlockStatusDto
    {
        public string GeneratorName { get; set; } = string.Empty;
        public bool IsUnlocked { get; set; }
    }

    public class UnlockCriteriaDto
    {
        public string? CriteriaId { get; set; }
        public string UnlocksGenerator { get; set; } = string.Empty;
        public int? MinWorldVisits { get; set; }
        public int? MinWorldsOfTag { get; set; }
        public string? RequiredTag { get; set; }
        public int? MinCrossWorldQuests { get; set; }
        public List<string>? RequiredQuestIds { get; set; }
        public List<string>? RequiredWorldTemplates { get; set; }
        public Dictionary<string, int>? TagVisitRequirements { get; set; }
    }

    public class DiscoveriesDto
    {
        public List<string> VisitedWorldIds { get; set; } = new List<string>();
        public List<string> VisitedMapIds { get; set; } = new List<string>();
        public List<string> DiscoveredWorldTemplates { get; set; } = new List<string>();
        public List<string> DiscoveredTags { get; set; } = new List<string>();
        public List<string> CompletedQuestIds { get; set; } = new List<string>();
        public List<string> CompletedCrossWorldQuestIds { get; set; } = new List<string>();
        public Dictionary<string, int> TagVisitCounts { get; set; } = new Dictionary<string, int>();
    }
}

