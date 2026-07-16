using System;
using System.Collections.Generic;

namespace Aetherium.Client.Contracts
{
    // Mirrors of the lobby/world-membership wire types. Note: WorldInfo, WorldState, and
    // JoinWorldResult live in the SERVER assembly (Aetherium.Server.MultiWorld), not
    // Aetherium.Model — the drift tests reflect against the server types for these.

    public enum WorldState
    {
        Creating,
        Active,
        Paused,
        ShuttingDown,
        Stopped,
    }

    /// <summary>Returned by ListWorlds/GetWorldInfo.</summary>
    public class WorldInfo
    {
        public string WorldId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public WorldState State { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public string? NarrativeId { get; set; }
        public List<string> MapIds { get; set; } = new List<string>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        public string? ClusterId { get; set; }
        public string? GameDefinitionId { get; set; }
        public string? GameDefinitionVersion { get; set; }
    }

    /// <summary>Returned by JoinWorld. Spawn coordinates are server-side bookkeeping the
    /// client must NOT treat as world position — the client's frame stays player-relative.</summary>
    public class JoinWorldResult
    {
        public bool Success { get; set; }
        public string? Reason { get; set; }
        public string? WorldId { get; set; }
        public string? MapId { get; set; }
        public int SpawnX { get; set; }
        public int SpawnY { get; set; }
        public int SpawnZ { get; set; }
    }

    /// <summary>Returned by UsePortal — the one surviving non-tool interaction verb.</summary>
    public class InteractionResultDto
    {
        public bool Success { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<UsageOptionDto>? Options { get; set; }
    }

    public class UsageOptionDto
    {
        public string UsageId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    // --- quest / party / instance surface (dedicated hub methods) ---

    public class QuestObjectiveDto
    {
        public string ObjectiveId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Completed { get; set; }
        public int Progress { get; set; }
        public int Required { get; set; } = 1;
    }

    public class QuestSummaryDto
    {
        public string QuestId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<QuestObjectiveDto> Objectives { get; set; } = new List<QuestObjectiveDto>();
        public bool IsActive { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class QuestLogDto
    {
        public List<QuestSummaryDto> Active { get; set; } = new List<QuestSummaryDto>();
        public List<string> Completed { get; set; } = new List<string>();
    }

    public class EnterDungeonResultDto
    {
        public bool Success { get; set; }
        public string? InstanceId { get; set; }
        public string? MapId { get; set; }
        public string? Error { get; set; }
    }

    public class PartyMemberDto
    {
        public string PlayerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
    }

    public class PartyInfoDto
    {
        public string PartyId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int MaxMembers { get; set; }
        public List<PartyMemberDto> Members { get; set; } = new List<PartyMemberDto>();
    }
}
