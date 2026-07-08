using System.Collections.Generic;

namespace Aetherium.Server.Factions
{
    /// <summary>
    /// A group as a first-class simulation entity (engine gap-analysis §4.6) — distinct from
    /// <see cref="Aetherium.Server.Narrative.Social.RelationshipMatrix"/>, which stays the
    /// per-NPC-pair personal layer. A plain data class in Phase 1, not an Orleans grain — see
    /// openspec/changes/add-factions design.md for why grain persistence is deferred.
    /// </summary>
    public class Faction
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> Tags { get; }
        public FactionDoctrine Doctrine { get; }

        private readonly HashSet<string> _memberIds = new();
        public IReadOnlyCollection<string> MemberIds => _memberIds;

        public Faction(string id, string name, FactionDoctrine doctrine, IReadOnlyList<string>? tags = null)
        {
            Id = id;
            Name = name;
            Doctrine = doctrine;
            Tags = tags ?? System.Array.Empty<string>();
        }

        public void AddMember(string actorId) => _memberIds.Add(actorId);
        public bool IsMember(string actorId) => _memberIds.Contains(actorId);
    }

    /// <summary>Registry of <see cref="Faction"/>s by id.</summary>
    public class FactionRegistry
    {
        private readonly Dictionary<string, Faction> _factions = new();

        public bool Add(Faction faction) => _factions.TryAdd(faction.Id, faction);

        public bool TryGet(string id, out Faction? faction) => _factions.TryGetValue(id, out faction);
    }
}
