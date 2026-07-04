using System;
using Orleans;

namespace Aetherium.Model
{
    /// <summary>
    /// Persistence-health snapshot for a single map grain (P3-8). Lets operators/tests observe
    /// whether the append-only delta log is keeping up instead of failures being silently
    /// swallowed. <see cref="Healthy"/> reflects the current state (no un-healed append failure);
    /// <see cref="DeltaAppendFailureCount"/> is the cumulative failure tally since activation.
    /// </summary>
    [GenerateSerializer]
    public class PersistenceHealthDto
    {
        /// <summary>True when there is no outstanding append failure awaiting a healing snapshot.</summary>
        [Id(0)] public bool Healthy { get; set; }

        /// <summary>Cumulative count of delta-append failures since this grain activated.</summary>
        [Id(1)] public long DeltaAppendFailureCount { get; set; }

        /// <summary>Message from the most recent append failure, or null if none has occurred.</summary>
        [Id(2)] public string? LastError { get; set; }

        /// <summary>UTC time of the most recent append failure, or null if none has occurred.</summary>
        [Id(3)] public DateTime? LastFailureAtUtc { get; set; }
    }
}
