using System;
using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Server.Factions
{
    /// <summary>One actor's standing with one faction (engine gap-analysis §4.6): `{faction, standing
    /// -1000..+1000, ranks, flags}`.</summary>
    public class Reputation
    {
        public const double MinStanding = -1000;
        public const double MaxStanding = 1000;

        public string FactionId { get; }
        public double Standing { get; internal set; }
        public List<string> Ranks { get; } = new();
        public HashSet<string> Flags { get; } = new();

        public Reputation(string factionId, double standing = 0)
        {
            FactionId = factionId;
            Standing = Math.Clamp(standing, MinStanding, MaxStanding);
        }
    }

    /// <summary>An actor's reputation ledger across every faction it has standing with.</summary>
    public class ReputationLedger : Component
    {
        private readonly Dictionary<string, Reputation> _byFaction = new();

        public IReadOnlyDictionary<string, Reputation> ByFaction => _byFaction;

        /// <summary>Seeds a pre-built reputation (used at join to stamp a faction's configured
        /// starting standing — see wire-factions-live), replacing any existing entry for its faction.</summary>
        public void Add(Reputation reputation) => _byFaction[reputation.FactionId] = reputation;

        public Reputation GetOrCreate(string factionId)
        {
            if (!_byFaction.TryGetValue(factionId, out var reputation))
            {
                reputation = new Reputation(factionId);
                _byFaction[factionId] = reputation;
            }
            return reputation;
        }

        /// <summary>Applies <paramref name="faction"/>'s doctrine-derived delta for
        /// <paramref name="actionTag"/> to this actor's standing with that faction, clamped to
        /// [<see cref="Reputation.MinStanding"/>, <see cref="Reputation.MaxStanding"/>].</summary>
        public Reputation ApplyAction(Faction faction, string actionTag)
        {
            var reputation = GetOrCreate(faction.Id);
            double delta = faction.Doctrine.DeltaFor(actionTag);
            reputation.Standing = Math.Clamp(reputation.Standing + delta, Reputation.MinStanding, Reputation.MaxStanding);
            return reputation;
        }
    }
}
