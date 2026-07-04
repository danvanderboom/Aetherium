using System;
using System.Threading.Tasks;
using Aetherium.WorldGen.Training;
using Orleans;

namespace Aetherium.Dashboard
{
    /// <summary>
    /// Reads an agent's curriculum progression from <see cref="ICurriculumProgressionGrain"/>
    /// (keyed by agent id). Null-safe like <see cref="AgentTelemetryService"/>: returns null on a
    /// missing client / error so the Blazor page can show an empty state rather than throw (P3-10).
    /// </summary>
    public class CurriculumProgressService
    {
        private readonly IClusterClient? _orleansClient;

        public CurriculumProgressService(IClusterClient? orleansClient = null)
        {
            _orleansClient = orleansClient;
        }

        public async Task<CurriculumProgress?> GetProgressAsync(string agentId)
        {
            if (_orleansClient == null || string.IsNullOrWhiteSpace(agentId))
                return null;

            try
            {
                var grain = _orleansClient.GetGrain<ICurriculumProgressionGrain>(agentId);
                return await grain.GetProgressAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CurriculumProgressService] Error getting progress for agent {agentId}: {ex.Message}");
                return null;
            }
        }

        public async Task<CurriculumStage?> GetCurrentStageAsync(string agentId)
        {
            if (_orleansClient == null || string.IsNullOrWhiteSpace(agentId))
                return null;

            try
            {
                var grain = _orleansClient.GetGrain<ICurriculumProgressionGrain>(agentId);
                return await grain.GetCurrentStageAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CurriculumProgressService] Error getting current stage for agent {agentId}: {ex.Message}");
                return null;
            }
        }
    }
}
