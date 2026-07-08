using Orleans;

namespace Aetherium.Model.Combat
{
    [GenerateSerializer]
    public enum DropOnDeathPolicy
    {
        None,
        All,
        Partial,
    }

    /// <summary>How a respawn's destination is resolved. Several modes reference a named/tagged
    /// world location (<see cref="RespawnLocationPolicy.LocationTag"/>) — no such registry exists in
    /// the engine yet (engine gap-analysis §4.11 live-wiring follow-up), so those modes are schema-
    /// only until one ships; resolving them today falls back to <see cref="WorldSpawn"/>.</summary>
    [GenerateSerializer]
    public enum RespawnLocationMode
    {
        /// <summary>Respawn exactly where the entity died.</summary>
        DeathLocation,
        /// <summary>Respawn at the location this player originally joined the map.</summary>
        EntryLocation,
        /// <summary>Re-run the map's normal spawn-selection algorithm (a fresh passable cell).</summary>
        WorldSpawn,
        /// <summary>Respawn at a named/tagged world location. Schema-only — no location registry exists yet.</summary>
        NamedLocation,
        /// <summary>Respawn at fixed coordinates.</summary>
        FixedCoordinates,
        /// <summary>Respawn at fixed coordinates plus an offset.</summary>
        OffsetFromCoordinates,
        /// <summary>Respawn at a named/tagged location plus an offset. Schema-only — no location registry exists yet.</summary>
        OffsetFromNamedLocation,
        /// <summary>Respawn at the last location this entity was known to be safe. Schema-only — no safe-location tracking exists yet.</summary>
        LastSafeLocation,
        /// <summary>Respawn near the party leader. Schema-only — no party system exists yet.</summary>
        PartyLeader,
    }

    /// <summary>
    /// Where a respawn lands (engine gap-analysis §4.11). A single enum couldn't carry the
    /// coordinates/offset/tag some modes need, so this pairs a <see cref="RespawnLocationMode"/> with
    /// the parameters that mode consumes; modes that don't need a given field simply ignore it.
    /// </summary>
    [GenerateSerializer]
    public class RespawnLocationPolicy
    {
        [Id(0)] public RespawnLocationMode Mode { get; set; } = RespawnLocationMode.WorldSpawn;

        /// <summary>Location tag for <see cref="RespawnLocationMode.NamedLocation"/> and
        /// <see cref="RespawnLocationMode.OffsetFromNamedLocation"/>.</summary>
        [Id(1)] public string? LocationTag { get; set; }

        /// <summary>Base coordinates for <see cref="RespawnLocationMode.FixedCoordinates"/> and
        /// <see cref="RespawnLocationMode.OffsetFromCoordinates"/>.</summary>
        [Id(2)] public int X { get; set; }
        [Id(3)] public int Y { get; set; }
        [Id(4)] public int Z { get; set; }

        /// <summary>Offset applied on top of the base coordinates/tag for
        /// <see cref="RespawnLocationMode.OffsetFromCoordinates"/> and
        /// <see cref="RespawnLocationMode.OffsetFromNamedLocation"/>.</summary>
        [Id(5)] public int OffsetX { get; set; }
        [Id(6)] public int OffsetY { get; set; }
        [Id(7)] public int OffsetZ { get; set; }

        public static RespawnLocationPolicy WorldSpawnDefault => new() { Mode = RespawnLocationMode.WorldSpawn };
    }

    [GenerateSerializer]
    public enum XpLossPolicy
    {
        None,
        PercentOfLevel,
        FlatAmount,
    }

    /// <summary>What happens to a permadeath player's session once their entity becomes a corpse.
    /// The entity-side outcome is the same regardless; this only governs whether the connected
    /// session stays attached in a read-only capacity or is dropped.</summary>
    [GenerateSerializer]
    public enum PermadeathSessionPolicy
    {
        /// <summary>The session stays connected and keeps receiving perception updates (read-only —
        /// see engine gap-analysis §4.11 live-wiring follow-up for actual spectator-mode UX).</summary>
        Spectate,
        /// <summary>The session is force-disconnected.</summary>
        Disconnect,
    }

    /// <summary>
    /// Per-world configuration for what dying means (engine gap-analysis §4.11). Declarative data
    /// only. Aetherium is an engine, not a single game — every field here exists so a specific game
    /// built on the engine can select its own death model as data (see
    /// openspec/changes/wire-death-respawn-live), not so the engine itself picks one. Lives in
    /// Aetherium.Model (not Aetherium.Server) because it must be reachable from both the server-side
    /// per-world config (<c>WorldConfig</c>) and the world-creation contract (<c>WorldTemplate</c>),
    /// which sits below Aetherium.Server in the project graph — mirrors the ContentAtlas split
    /// (data schema in Model, server-side seeding/consumption in Server).
    /// </summary>
    [GenerateSerializer]
    public class DeathPolicy
    {
        [Id(0)] public bool Permadeath { get; set; }
        [Id(1)] public int CorpseRetentionTicks { get; set; }
        [Id(2)] public DropOnDeathPolicy DropOnDeath { get; set; }
        [Id(3)] public RespawnLocationPolicy RespawnLocation { get; set; } = RespawnLocationPolicy.WorldSpawnDefault;
        [Id(4)] public XpLossPolicy XpLossPolicy { get; set; }
        [Id(5)] public double XpLossAmount { get; set; }
        [Id(6)] public bool DownStateEnabled { get; set; }
        [Id(7)] public int ReviveWindowTicks { get; set; }

        /// <summary>Ticks of post-respawn invulnerability, so a respawn can't be instantly re-downed
        /// by whatever killed the player. Zero means no invulnerability window.</summary>
        [Id(8)] public int RespawnInvulnerabilityTicks { get; set; }

        /// <summary>What a permadeath player's session does once their entity becomes a corpse.
        /// Ignored unless <see cref="Permadeath"/> is <c>true</c>.</summary>
        [Id(9)] public PermadeathSessionPolicy PermadeathBehavior { get; set; }

        /// <summary>Reproduces the behavior `deepen-combat-model` shipped before any policy existed:
        /// a down state, a 3-tick revive window, corpses retained forever (no opt-in corpse-aging
        /// component attached), respawn via the map's normal spawn selection, a friendly (spectate,
        /// not disconnect) permadeath session default, and a short respawn-invulnerability window
        /// since players never had that protection before (there was no respawn at all) — no
        /// loot/XP-loss behavior defined.</summary>
        public static DeathPolicy Default => new()
        {
            Permadeath = false,
            CorpseRetentionTicks = int.MaxValue,
            DropOnDeath = DropOnDeathPolicy.Partial,
            RespawnLocation = RespawnLocationPolicy.WorldSpawnDefault,
            XpLossPolicy = XpLossPolicy.None,
            XpLossAmount = 0,
            DownStateEnabled = true,
            ReviveWindowTicks = 3,
            RespawnInvulnerabilityTicks = 3,
            PermadeathBehavior = PermadeathSessionPolicy.Spectate,
        };

        /// <summary>The tick countdown a lethal hit should assign to a target's down-state
        /// component: <see cref="ReviveWindowTicks"/> if <see cref="DownStateEnabled"/>, else 0 (an
        /// instant transition straight to the terminal/corpse state — no down state at all).</summary>
        public int ResolveDyingTicks() => DownStateEnabled ? ReviveWindowTicks : 0;
    }
}
