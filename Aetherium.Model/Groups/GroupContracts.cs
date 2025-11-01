using System;
using System.Collections.Generic;
using Orleans;
using Aetherium.Model.Worlds;

namespace Aetherium.Model.Groups
{
    /// <summary>
    /// Unique identifier for a raid (larger party).
    /// </summary>
    [GenerateSerializer]
    public readonly record struct RaidId(string Value);

    /// <summary>
    /// Party member information.
    /// </summary>
    [GenerateSerializer]
    public class PartyMember
    {
        [Id(0)] public PlayerId PlayerId { get; set; }
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public PartyRole Role { get; set; } = PartyRole.Member;
        [Id(3)] public DateTime JoinedAt { get; set; }
        [Id(4)] public bool IsOnline { get; set; }
    }

    /// <summary>
    /// Role of a party member.
    /// </summary>
    [GenerateSerializer]
    public enum PartyRole
    {
        Leader,
        Officer,
        Member
    }

    /// <summary>
    /// Party information.
    /// </summary>
    [GenerateSerializer]
    public class PartyInfo
    {
        [Id(0)] public PartyId PartyId { get; set; }
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public List<PartyMember> Members { get; set; } = new List<PartyMember>();
        [Id(3)] public int MaxMembers { get; set; } = 5;
        [Id(4)] public DateTime CreatedAt { get; set; }
        [Id(5)] public WorldId? WorldId { get; set; }
    }

    /// <summary>
    /// Raid information (larger party, typically up to 40 members).
    /// </summary>
    [GenerateSerializer]
    public class RaidInfo
    {
        [Id(0)] public RaidId RaidId { get; set; }
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public List<PartyMember> Members { get; set; } = new List<PartyMember>();
        [Id(3)] public int MaxMembers { get; set; } = 40;
        [Id(4)] public DateTime CreatedAt { get; set; }
        [Id(5)] public WorldId? WorldId { get; set; }
    }
}

