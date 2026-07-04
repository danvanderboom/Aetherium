namespace Aetherium.Server.Simulation
{
    /// <summary>
    /// Configuration options for world simulation.
    /// </summary>
    public class SimulationOptions
    {
        /// <summary>
        /// Server ticks per second (default: 1).
        /// </summary>
        public double TickHz { get; set; } = 1.0;

        /// <summary>
        /// Real-world minutes for one in-game day (default: 24).
        /// </summary>
        public int DayLengthMinutes { get; set; } = 24;

        /// <summary>
        /// Region size in tiles (default: 64).
        /// </summary>
        public int RegionSize { get; set; } = 64;

        /// <summary>
        /// Enable weather system (default: true).
        /// </summary>
        public bool EnableWeather { get; set; } = true;

        /// <summary>
        /// Enable seasons (default: true).
        /// </summary>
        public bool EnableSeasons { get; set; } = true;

        /// <summary>
        /// Enable agent-driven changes (paths, structures) (default: true).
        /// </summary>
        public bool EnableAgentChanges { get; set; } = true;

        /// <summary>
        /// Enable procedural events (default: true).
        /// </summary>
        public bool EnableProceduralEvents { get; set; } = true;

        /// <summary>
        /// Enable NPC/monster behavior — monsters wander on the tick pipeline
        /// (default: true). Turn off for deterministic tests or a frozen world.
        /// </summary>
        public bool EnableNpcBehavior { get; set; } = true;

        /// <summary>
        /// How many map ticks pass between NPC/monster steps (default: 1 — one
        /// step per tick). At the default 1 Hz tick rate that is roughly one
        /// monster move per second; raise it to slow monsters without changing
        /// <see cref="TickHz"/>. Values below 1 are treated as 1.
        /// </summary>
        public int NpcMoveIntervalTicks { get; set; } = 1;
    }
}

