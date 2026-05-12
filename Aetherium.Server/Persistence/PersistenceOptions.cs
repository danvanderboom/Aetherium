namespace Aetherium.Server.Persistence
{
    /// <summary>
    /// Configuration for the world-persistence stack (snapshot store + delta log).
    /// Bound from <c>Persistence</c> section of <c>appsettings.json</c> at startup.
    /// </summary>
    public class PersistenceOptions
    {
        /// <summary>Compaction (periodic snapshot + delta-log truncation) settings.</summary>
        public CompactionOptions Compaction { get; set; } = new CompactionOptions();
    }

    /// <summary>
    /// Controls when <c>GameMapGrain</c> automatically captures a snapshot and compacts
    /// the delta log behind it. Compaction bounds both recovery time on cold start and
    /// log growth on disk.
    /// </summary>
    public class CompactionOptions
    {
        /// <summary>
        /// Master switch. When false, no automatic compaction runs; the log grows
        /// until <see cref="Aetherium.Server.MultiWorld.IGameMapGrain.ForceSnapshotAsync"/>
        /// is invoked manually. Useful for tests and for diagnosing replay behavior.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Periodic interval at which the grain timer fires a compaction even if the
        /// delta count threshold has not been reached. Default 10 minutes.
        /// </summary>
        public int IntervalMinutes { get; set; } = 10;

        /// <summary>
        /// Compaction also fires immediately if this many deltas have been appended
        /// since the last snapshot, even before the interval timer fires. Default 1000.
        /// </summary>
        public int DeltaCountThreshold { get; set; } = 1000;
    }
}
