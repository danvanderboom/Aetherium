using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model
{
    /// <summary>
    /// Result of a player/agent request to enter a dungeon instance. Standalone (no server types)
    /// so it can cross the SignalR boundary.
    /// </summary>
    [GenerateSerializer]
    public class EnterDungeonResultDto
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string? InstanceId { get; set; }
        [Id(2)] public string? MapId { get; set; }
        [Id(3)] public string? Error { get; set; }
    }

    /// <summary>
    /// Player-facing view of a party.
    /// </summary>
    [GenerateSerializer]
    public class PartyInfoDto
    {
        [Id(0)] public string PartyId { get; set; } = string.Empty;
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public int MaxMembers { get; set; }
        [Id(3)] public List<PartyMemberDto> Members { get; set; } = new();
    }

    /// <summary>
    /// Player-facing view of a single party member.
    /// </summary>
    [GenerateSerializer]
    public class PartyMemberDto
    {
        [Id(0)] public string PlayerId { get; set; } = string.Empty;
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public string Role { get; set; } = string.Empty;
        [Id(3)] public bool IsOnline { get; set; }
    }
}
