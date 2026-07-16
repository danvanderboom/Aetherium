using System.Collections.Generic;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Opt-in declaration of which tilings a generator can build (docs/grid-topologies.md).
    /// A generator that does not implement this interface is implicitly square-only, so every
    /// existing generator keeps working with zero edits. The bundle validator (and, later,
    /// <c>CreateWorldAsync</c>) checks a world's <c>topology</c> against the chosen generator's
    /// declared support before creation. Topology math itself never lives here — a generator
    /// only decides tile placement; adjacency/distance/lines come from <c>World.Topology</c>.
    /// </summary>
    public interface ITopologyAwareGenerator
    {
        /// <summary>The topology names this generator can build (e.g. <c>["square"]</c>,
        /// <c>["hex"]</c>, or <c>["square", "hex"]</c> for a topology-agnostic one).</summary>
        IReadOnlyCollection<string> SupportedTopologies { get; }
    }
}
