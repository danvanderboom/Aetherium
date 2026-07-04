using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Model.Analysis;
using Aetherium.Server.Narrative;
using Aetherium.Model.Narrative;
using Aetherium.Server.WorldGen.Adaptation;
using Orleans;

namespace Aetherium.Server.Controllers
{
    /// <summary>
    /// REST API controller for accessing adaptation data and behavior analysis.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AdaptationController : ControllerBase
    {
        private readonly IClusterClient _orleansClient;

        public AdaptationController(IClusterClient orleansClient)
        {
            _orleansClient = orleansClient;
        }

        /// <summary>
        /// Gets behavior analysis for an agent.
        /// </summary>
        [HttpGet("behavior/{agentId}")]
        public async Task<ActionResult<BehaviorAnalysis>> GetBehaviorAnalysis(string agentId)
        {
            var behaviorGrain = _orleansClient.GetGrain<IBehaviorAnalysisGrain>(agentId);
            var analysis = await behaviorGrain.AnalyzeBehaviorAsync();
            
            return Ok(analysis);
        }

        /// <summary>
        /// Gets interest profile for an agent.
        /// </summary>
        [HttpGet("interest/{agentId}")]
        public async Task<ActionResult<InterestProfile>> GetInterestProfile(string agentId)
        {
            var behaviorGrain = _orleansClient.GetGrain<IBehaviorAnalysisGrain>(agentId);
            var profile = await behaviorGrain.GetInterestProfileAsync();
            
            return Ok(profile);
        }

        /// <summary>
        /// Gets content needs for an agent.
        /// </summary>
        [HttpGet("needs/{agentId}")]
        public async Task<ActionResult<List<ContentNeed>>> GetContentNeeds(string agentId)
        {
            var behaviorGrain = _orleansClient.GetGrain<IBehaviorAnalysisGrain>(agentId);
            var needs = await behaviorGrain.GetContentNeedsAsync();
            
            return Ok(needs);
        }

        /// <summary>
        /// Gets adaptive quests for an agent.
        /// </summary>
        [HttpGet("quests/{agentId}")]
        public async Task<ActionResult<List<QuestDefinition>>> GetAdaptiveQuests(
            string agentId,
            [FromQuery] string? narrativeId = null,
            [FromQuery] int maxQuests = 5)
        {
            // Use provided narrative ID or default
            narrativeId ??= "default";
            
            var adaptiveGrain = _orleansClient.GetGrain<IAdaptiveNarrativeGrain>(narrativeId);
            var quests = await adaptiveGrain.GenerateAdaptiveQuestsAsync(agentId, maxQuests);
            
            return Ok(quests);
        }

        /// <summary>
        /// Updates behavior analysis for an agent.
        /// </summary>
        [HttpPost("behavior/{agentId}/update")]
        public async Task<ActionResult> UpdateBehaviorAnalysis(string agentId)
        {
            var behaviorGrain = _orleansClient.GetGrain<IBehaviorAnalysisGrain>(agentId);
            await behaviorGrain.UpdateAnalysisAsync();
            
            return Ok();
        }

        /// <summary>
        /// Gets action preferences for an agent.
        /// </summary>
        [HttpGet("preferences/{agentId}/actions")]
        public async Task<ActionResult<Dictionary<string, ActionPreference>>> GetActionPreferences(string agentId)
        {
            var behaviorGrain = _orleansClient.GetGrain<IBehaviorAnalysisGrain>(agentId);
            var preferences = await behaviorGrain.GetActionPreferencesAsync();
            
            return Ok(preferences);
        }

        /// <summary>
        /// Gets exploration patterns for an agent.
        /// </summary>
        [HttpGet("preferences/{agentId}/exploration")]
        public async Task<ActionResult<Dictionary<string, AreaPreference>>> GetExplorationPatterns(string agentId)
        {
            var behaviorGrain = _orleansClient.GetGrain<IBehaviorAnalysisGrain>(agentId);
            var patterns = await behaviorGrain.GetExplorationPatternsAsync();
            
            return Ok(patterns);
        }

        /// <summary>
        /// Gets all adaptation rules.
        /// </summary>
        [HttpGet("rules")]
        public ActionResult<List<AdaptationRuleDefinition>> GetAllRules()
        {
            var rules = AdaptationRuleEngine.GetAllRules();
            return Ok(rules);
        }

        /// <summary>
        /// Reloads adaptation rules from disk.
        /// </summary>
        [HttpPost("rules/reload")]
        public ActionResult ReloadRules([FromQuery] string? rulesDirectory = null)
        {
            rulesDirectory ??= Path.Combine("Data", "AdaptationRules");
            AdaptationRuleEngine.LoadRules(rulesDirectory);
            return Ok();
        }
    }
}

