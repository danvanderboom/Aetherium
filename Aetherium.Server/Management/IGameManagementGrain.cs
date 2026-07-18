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

        // Game definitions (add-game-definition-loader): YAML-bundled games and their instances
        Task<List<Aetherium.Model.Games.GameDefinitionSummaryDto>> ListGameDefinitionsAsync();
        Task<Aetherium.Model.Games.GameInstanceResult> CreateGameInstanceAsync(string gameDefinitionId, string? instanceName = null);
        Task<List<Aetherium.Server.MultiWorld.WorldInfo>> ListGameInstancesAsync(string gameDefinitionId);
        
        // World ACL and Invites (using IWorldHost)
        Task<List<Aetherium.Model.Worlds.WorldSummary>> ListWorldsWithAclAsync(Aetherium.Model.Worlds.WorldQuery query);
        Task<OperationResult> SetWorldAclAsync(string worldId, Aetherium.Model.Worlds.WorldAcl acl);
        Task<Aetherium.Model.Worlds.WorldAcl?> GetWorldAclAsync(string worldId);
        Task<string> InvitePlayerAsync(string worldId, string playerId);
        Task<OperationResult> AcceptInviteAsync(string inviteId);

        // Operator / headless driving (see specs: game-management-grain)
        // Provisions a client-less session in an existing world and returns its sessionId.
        Task<HeadlessSessionResult> CreateHeadlessSessionAsync(string worldId, int? startX, int? startY, int? startZ, string? profile);
        // Operator perception with optional absolute (un-relativized) world coordinates.
        Task<string?> GetPerceptionAsync(string sessionId, bool absoluteCoordinates); // JSON-serialized PerceptionDto
        // Omniscient, FOV-independent snapshot of a world's tiles/entities (JSON WorldSnapshotDto).
        Task<string?> GetWorldSnapshotAsync(string worldId);
        // Terminates headless sessions idle beyond maxIdleSeconds; returns the number reaped.
        Task<int> ReapIdleHeadlessSessionsAsync(int maxIdleSeconds);
        // Executes a world-building tool (world_edit) against a live world at runtime.
        Task<ToolExecutionResultDto> ExecuteWorldToolAsync(string worldId, string toolId, Dictionary<string, object> args);
        // A character's accumulated memories (JSON CharacterMemoryDto); operator-gated god-view read.
        Task<string?> GetMemoryAsync(string sessionId);

        // Gameplay control + perception (for agents)
        Task<string?> GetPerceptionAsync(string sessionId); // JSON-serialized PerceptionDto
        Task<OperationResult> MoveAsync(string sessionId, string direction);
        Task<OperationResult> PickupAsync(string sessionId, string targetEntityId);
        Task<OperationResult> DropAsync(string sessionId, string itemEntityId);
        Task<OperationResult> UseAsync(string sessionId, string itemEntityId, string onEntityId);
        Task<OperationResult> OpenAsync(string sessionId, string targetEntityId);
        Task<OperationResult> CloseAsync(string sessionId, string targetEntityId);
        
        // Tool system
        Task<List<ToolInfoDto>> ListAvailableToolsAsync(string? profileName = null);
        Task<ToolExecutionResultDto> ExecuteToolAsync(string toolId, string sessionId, Dictionary<string, object> args);
        // Runs an ordered action sequence against one session in a single grain turn (deterministic ordering).
        Task<List<BatchActionResultDto>> ExecuteToolBatchAsync(string sessionId, List<ScriptedActionDto> actions, bool stopOnError);
    }
}


