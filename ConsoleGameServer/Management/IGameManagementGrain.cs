using System.Collections.Generic;
using System.Threading.Tasks;
using ConsoleGameModel;
using Orleans;

namespace ConsoleGameServer.Management
{
    /// <summary>
    /// Orleans grain interface for managing and controlling active game sessions.
    /// </summary>
    public interface IGameManagementGrain : IGrainWithStringKey
    {
        // Lifecycle (called by GameHub)
        Task RegisterSessionAsync(string sessionId, string connectionId);
        Task UnregisterSessionAsync(string sessionId);
        
        // Queries
        Task<List<SessionInfo>> ListSessionsAsync();
        Task<SessionInfo?> GetSessionInfoAsync(string sessionId);
        Task<SessionInfo?> GetSessionByConnectionIdAsync(string connectionId);
        Task<int> GetSessionCountAsync();
        
        // Vision Control
        Task<OperationResult> SetDirectionalVisionAsync(string sessionId, bool enabled);
        Task<OperationResult> SetFieldOfViewAsync(string sessionId, int degrees);
        Task<VisionStatus?> GetVisionStatusAsync(string sessionId);
        
        // Session Control
        Task<OperationResult> SetLightingModeAsync(string sessionId, LightingMode mode);
        Task<OperationResult> SetVisionModeAsync(string sessionId, VisionMode mode);
        Task<OperationResult> TerminateSessionAsync(string sessionId);
        Task<OperationResult> SetTimeScaleAsync(string sessionId, double scale);
    }
}

