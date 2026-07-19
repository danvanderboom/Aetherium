using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>One trade connection from a settlement to a neighbour: which settlement, whether the
    /// route is a wide highway (higher throughput), and how long the corridor is (longer = more friction,
    /// so distant markets stay less equalised).</summary>
    public sealed class TradeLink
    {
        public string To { get; set; } = string.Empty;   // neighbour settlement entity id
        public bool Highway { get; set; }
        public int Length { get; set; }

        /// <summary>Transport mode: "road", "rail", "subway", … — flavour + a natural capacity tier.</summary>
        public string Mode { get; set; } = "road";

        /// <summary>Throughput multiplier: how much this route can move per step. A highway carries more
        /// than a feeder; rail more than a highway; a subway most of all (grade-separated metro freight).</summary>
        public double Capacity { get; set; } = 1.0;
    }

    /// <summary>
    /// The transport edges out of a settlement — the road (and, later, rail/river/subway) links the
    /// economy moves goods along. Built from the worldgen road graph so the economy rides the exact
    /// network the map shows: goods arbitrage from cheap markets to dear ones across these links,
    /// throttled by each link's throughput and length.
    /// </summary>
    public class TradeLinks : Component
    {
        public List<TradeLink> Links { get; set; } = new();

        public TradeLinks() : base() { }
    }
}
