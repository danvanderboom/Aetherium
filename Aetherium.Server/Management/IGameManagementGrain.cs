using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model;
using Orleans;

namespace Aetherium.Server.Management
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

        // World Management
        Task<List<Aetherium.Server.MultiWorld.WorldInfo>> ListWorldsAsync();
        Task<Aetherium.Server.MultiWorld.WorldInfo?> GetWorldInfoAsync(string worldId);
        Task<string> CreateWorldAsync(Aetherium.Server.MultiWorld.CreateWorldRequest request);
        Task<OperationResult> PauseWorldAsync(string worldId);
        Task<OperationResult> ResumeWorldAsync(string worldId);
        Task<OperationResult> ShutdownWorldAsync(string worldId);

        // Gameplay control + perception (for agents)
        Task<string?> GetPerceptionAsync(string sessionId); // JSON-serialized PerceptionDto
        Task<OperationResult> MoveAsync(string sessionId, string direction);
        Task<OperationResult> PickupAsync(string sessionId, string targetEntityId);
        Task<OperationResult> DropAsync(string sessionId, string itemEntityId);
        Task<OperationResult> UseAsync(string sessionId, string itemEntityId, string onEntityId);
        Task<OperationResult> OpenAsync(string sessionId, string targetEntityId);
        Task<OperationResult> CloseAsync(string sessionId, string targetEntityId);
    }
}


