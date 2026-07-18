using Orleans;
using Orleans.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;
using Aetherium.Server.Ai;
using Aetherium.Server.Combat;
using Aetherium.Server.Abilities;
using Aetherium.Server.Progression;
using Aetherium.Server.Factions;
using Aetherium.Model.Combat;
using Aetherium.Model.Abilities;
using Aetherium.Model.Progression;
using Aetherium.Model.Factions;
using Aetherium.WorldGen;
using Passes = Aetherium.WorldGen.Passes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain managing a single game map within a world.
    /// </summary>
    public class GameMapGrain : Grain, IGameMapGrain
    {
        private readonly IPersistentState<MapState> _mapState;
        private readonly MapGeneratorRegistry _generatorRegistry;
        private readonly IGrainFactory _grainFactory;
        private readonly Microsoft.Extensions.Options.IOptions<Aetherium.Server.Simulation.SimulationOptions> _simulationOptions;
        private World? _world;
        private Dictionary<string, IMapRegionGrain>? _regions; // Cache of region grains
        private int _regionSize;

        // Tracks spawn locations currently in use by joined players so JoinPlayerAsync
        // can hand out distinct cells. Not persisted — phase 1 player sessions are
        // ephemeral; clients reconnect with fresh sessions if the silo restarts.
        private readonly HashSet<Aetherium.Components.WorldLocation> _spawnsInUse = new();
        private readonly Dictionary<string, Aetherium.Components.WorldLocation> _playerSpawns = new();

        // Monotonic sequence number for emitted MapDeltas. Lets clients detect
        // gaps in delivery and trigger a resync (phase 2 doesn't implement the
        // resync mechanism yet; the sequence number is the wire-format hook).
        private long _nextSequence = 1;

        // Counts map ticks so NPC movement can run at a sub-multiple of the tick
        // rate (SimulationOptions.NpcMoveIntervalTicks) without a wall-clock timer.
        private long _tickCounter;

        // One BehaviorTree instance per live monster, keyed by EntityId, so each NPC's
        // blackboard/composite-node running-state persists across ticks (the "Per-NPC
        // Behavior Tree Instance" requirement in specs/npc-behavior-trees/spec.md).
        // Pruned in StepNpcsAsync whenever a monster leaves _world.Entities.
        private readonly Dictionary<string, BehaviorTree> _monsterTrees = new();

        // --- opt-in perf instrumentation (set AETHERIUM_PERF=1) ---
        // Counts NPC steps and the deltas they fan out per window, so we can see how much the
        // roaming-monster tick is driving the perception-flush load (each moved monster dirties
        // every co-located session and schedules a fresh FOV perception).
        private static readonly bool NpcPerfLog = Environment.GetEnvironmentVariable("AETHERIUM_PERF") == "1";
        private long _npcPerfWindowStartMs = -1;
        private long _npcPerfTicks;
        private long _npcPerfMoves;
        private int _npcPerfLastMonsterCount;

        // AP a monster spends per behavior-tree tick in the continuous action pipeline
        // (engine gap-analysis §4.1). A default Monster carries Speed == MaxBudget == this,
        // so it affords an action every eligible tick (parity with pre-pipeline behavior);
        // a creature with a lower Speed acts on a sub-cadence. See StepNpcsAsync and
        // openspec/changes/wire-npc-action-budget-live.
        private const double NpcActionCost = 1.0;

        // Highest WorldSnapshot.SnapshotVersion this binary knows how to hydrate.
        // Bumping requires a deliberate schema migration; cold-start refuses to
        // load snapshots written at a higher version. Phase F wire-stability hook.
        private const int SupportedSnapshotVersion = 1;

        // Heat trail tracker — grain-authoritative per design D9. Heat is "objective
        // reality of the world" — a recently-walked cell is hot regardless of who's
        // looking. Sessions maintain a delta-driven local mirror.
        private readonly Aetherium.Server.Perception.HeatTrailTracker _heatTracker = new();

        // Subscription handle for _world.WorldEvents. Captured so OnDeactivate can detach.
        private Action<WorldEvent>? _worldEventsSubscriber;

        // Cached server-side session manager for delta application. Phase 2c routes
        // deltas through the host (which iterates affected sessions, applies the
        // delta to each local mirror, and ships fresh perceptions) rather than
        // direct SignalR-group fan-out — this preserves the perception-pure design
        // by ensuring clients only ever see filtered PerceptionDtos, not raw
        // deltas that could carry cells outside their FOV.
        private Aetherium.Server.GameSessionManager? _sessionManager;
        private bool _sessionManagerResolved;

        private Aetherium.Server.GameSessionManager? GetSessionManager()
        {
            if (!_sessionManagerResolved)
            {
                _sessionManager = this.ServiceProvider.GetService(
                    typeof(Aetherium.Server.GameSessionManager))
                    as Aetherium.Server.GameSessionManager;
                _sessionManagerResolved = true;
            }
            return _sessionManager;
        }

        // Lazy-resolved snapshot store. Optional so existing test fixtures that don't
        // register one (and the "memory" default) still work; absence yields a silent
        // no-op append. Production wiring (Program.cs) provides SqliteWorldSnapshotStore
        // when ORLEANS_STORAGE=sqlite, MemoryWorldSnapshotStore otherwise.
        private Aetherium.Server.Persistence.IWorldSnapshotStore? _snapshotStore;
        private bool _snapshotStoreResolved;

        // Compaction state. Counter increments per persisted delta; when it crosses
        // CompactionOptions.DeltaCountThreshold OR the periodic timer fires, we capture
        // a fresh snapshot and reset the counter (ForceSnapshotAsync also compacts the
        // log). Setting Enabled=false short-circuits both triggers.
        private IDisposable? _compactionTimer;
        private long _deltasSinceLastSnapshot;
        private int _compactionInFlight; // CAS guard so concurrent triggers don't double-fire

        // Persistence-health tracking (P3-8). Delta-append failures are recorded rather than
        // silently swallowed: on failure we mark persistence "dirty"; on the next successful
        // append we force a healing snapshot (a full snapshot supersedes deltas that never made
        // it to the log) and clear the flag. Exposed via GetPersistenceHealthAsync.
        private long _persistDeltaFailureCount;
        private string? _lastPersistError;
        private DateTime? _lastPersistFailureAtUtc;
        private int _persistenceDirty; // 1 => at least one append failed and hasn't been healed yet

        // Lazily-resolved structured logger. The grain historically wrote persistence errors to
        // Console (which also corrupts the Spectre TUI); resolve ILogger the same lazy way as the
        // other services so failures land in real logs.
        private Microsoft.Extensions.Logging.ILogger? _logger;
        private bool _loggerResolved;

        private Microsoft.Extensions.Logging.ILogger? GetLogger()
        {
            if (!_loggerResolved)
            {
                _logger = this.ServiceProvider.GetService(
                    typeof(Microsoft.Extensions.Logging.ILogger<GameMapGrain>))
                    as Microsoft.Extensions.Logging.ILogger;
                _loggerResolved = true;
            }
            return _logger;
        }

        private Aetherium.Server.Persistence.IWorldSnapshotStore? GetSnapshotStore()
        {
            if (!_snapshotStoreResolved)
            {
                _snapshotStore = this.ServiceProvider.GetService(
                    typeof(Aetherium.Server.Persistence.IWorldSnapshotStore))
                    as Aetherium.Server.Persistence.IWorldSnapshotStore;
                _snapshotStoreResolved = true;
            }
            return _snapshotStore;
        }

        /// <summary>
        /// Persists a sequence-stamped delta to the append-only log via
        /// <see cref="Aetherium.Server.Persistence.IWorldSnapshotStore.AppendMapDeltaAsync"/>. Failures
        /// are logged but do not stall game logic — see the failure-handling requirement in
        /// the world-persistence spec.
        /// </summary>
        private async Task PersistDeltaAsync(MapDelta delta)
        {
            var store = GetSnapshotStore();
            if (store is null || _mapState.State is null) return;
            try
            {
                await store.AppendMapDeltaAsync(_mapState.State.WorldId, _mapState.State.MapId, delta);
                System.Threading.Interlocked.Increment(ref _deltasSinceLastSnapshot);

                // If a prior append failed, the durable log is missing those deltas. Now that
                // persistence has recovered, force a full snapshot — it supersedes the lost
                // deltas — and clear the dirty flag.
                if (System.Threading.Interlocked.Exchange(ref _persistenceDirty, 0) == 1)
                    TriggerHealSnapshot();

                MaybeTriggerThresholdCompaction();
            }
            catch (Exception ex)
            {
                System.Threading.Interlocked.Increment(ref _persistDeltaFailureCount);
                _lastPersistError = ex.Message;
                _lastPersistFailureAtUtc = DateTime.UtcNow;
                System.Threading.Interlocked.Exchange(ref _persistenceDirty, 1);
                var logger = GetLogger();
                if (logger is not null)
                    logger.LogError(ex, "AppendMapDelta failed seq={Sequence} type={DeltaType} for {WorldId}/{MapId}",
                        delta.Sequence, delta.GetType().Name, _mapState.State.WorldId, _mapState.State.MapId);
                else
                    Console.WriteLine($"[GameMapGrain] AppendMapDelta failed seq={delta.Sequence} type={delta.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Forces a healing snapshot (used after persistence recovers from a failure). Guarded by
        /// the same in-flight CAS as compaction so it can't double-fire with a timer/threshold trigger.
        /// </summary>
        private void TriggerHealSnapshot()
        {
            if (System.Threading.Interlocked.CompareExchange(ref _compactionInFlight, 1, 0) != 0) return;
            _ = RunCompactionAsync();
        }

        /// <summary>
        /// If the count of deltas appended since the last snapshot has crossed the
        /// configured threshold, kick off a compaction in the background. CAS guard
        /// prevents two concurrent triggers (timer + threshold racing). The check is
        /// cheap; the snapshot capture itself runs on the grain's own scheduler.
        /// </summary>
        private void MaybeTriggerThresholdCompaction()
        {
            var opts = GetCompactionOptions();
            if (opts is null || !opts.Enabled || opts.DeltaCountThreshold <= 0) return;
            if (_deltasSinceLastSnapshot < opts.DeltaCountThreshold) return;
            if (System.Threading.Interlocked.CompareExchange(ref _compactionInFlight, 1, 0) != 0) return;
            _ = RunCompactionAsync();
        }

        private async Task RunCompactionAsync()
        {
            try
            {
                await ForceSnapshotAsync();
                System.Threading.Interlocked.Exchange(ref _deltasSinceLastSnapshot, 0);
            }
            catch (Exception ex)
            {
                var logger = GetLogger();
                if (logger is not null) logger.LogError(ex, "Compaction failed");
                else Console.WriteLine($"[GameMapGrain] Compaction failed: {ex.Message}");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _compactionInFlight, 0);
            }
        }

        private Aetherium.Server.Persistence.CompactionOptions? GetCompactionOptions()
        {
            var optsAccessor = ServiceProvider.GetService(typeof(Microsoft.Extensions.Options.IOptions<Aetherium.Server.Persistence.PersistenceOptions>))
                as Microsoft.Extensions.Options.IOptions<Aetherium.Server.Persistence.PersistenceOptions>;
            return optsAccessor?.Value.Compaction;
        }

        public GameMapGrain(
            [PersistentState("map", "mapStore")] IPersistentState<MapState> mapState,
            MapGeneratorRegistry generatorRegistry,
            IGrainFactory grainFactory,
            Microsoft.Extensions.Options.IOptions<Aetherium.Server.Simulation.SimulationOptions> simulationOptions)
        {
            _mapState = mapState;
            _generatorRegistry = generatorRegistry;
            _grainFactory = grainFactory;
            _simulationOptions = simulationOptions;
            _regionSize = _simulationOptions.Value.RegionSize;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            // Rehydrate the death policy from persisted state on reactivation — InitializeAsync
            // (which sets it initially) is not called again after a silo restart. A brand-new grain
            // has no MapState.DeathPolicy yet, so this leaves the field-initializer Default in place.
            if (_mapState.State?.DeathPolicy is not null)
                _deathPolicy = _mapState.State.DeathPolicy;

            // Recompile the ability catalog + re-stamp resource-pool definitions from persisted config
            // (engine gap-analysis §4.3 — see wire-abilities-live), so abilities survive reactivation
            // without re-running InitializeAsync. Null config yields an empty catalog.
            ApplyAbilityConfig(_mapState.State?.AbilityConfig);

            // Likewise recompile progression (engine gap-analysis §4.4 — see wire-progression-live).
            ApplyProgressionConfig(_mapState.State?.ProgressionConfig);

            // And factions (engine gap-analysis §4.6 — see wire-factions-live).
            ApplyFactionConfig(_mapState.State?.FactionConfig);

            // And content (add-content-definitions).
            ApplyContentConfig(_mapState.State?.ContentConfig);

            // And reactive rules (add-eca-scripting), seeded from the persisted recipe so the rule RNG
            // resumes deterministically. Recipe is null on a brand-new grain — the runtime stays absent
            // until InitializeAsync applies it with the fresh seed.
            ApplyEcaConfig(_mapState.State?.EcaConfig, _mapState.State?.Recipe?.Seed ?? 0);

            if (_mapState.State != null && _mapState.State.Recipe != null && _world == null)
            {
                // Reactivation after silo restart. Prefer a persisted snapshot when
                // available — it captures mid-game mutations (doors opened, items moved,
                // component fields decremented) that recipe regeneration alone cannot.
                // Fall back to recipe-only regen on first-ever activation, when the
                // snapshot store isn't wired, or when no snapshot has been captured.
                _world = await TryHydrateFromSnapshotAsync()
                    ?? RegenerateFromRecipe(_mapState.State.Recipe);

                // Re-resolve the world's tiling from persisted state (docs/grid-topologies.md).
                // Null/empty (any pre-topology map) → square, so old persisted state reactivates
                // byte-identically.
                _world.Topology = Aetherium.Topology.GridTopologyRegistry.Get(_mapState.State.Topology);

                // Re-skin data-driven creatures (add-content-definitions): snapshot-hydrated
                // entities re-bind their definition via CreatureTypeTag (damage preserved);
                // recipe-regenerated monsters get the same deterministic draw as first creation.
                ApplyContentPopulation(_world, _mapState.State.Recipe.Seed);

                await PartitionIntoRegionsAsync();
                AttachWorldEventSubscriber();
            }

            // Start the periodic compaction timer when persistence + compaction are
            // both wired. Falls back to a no-op when either the snapshot store or the
            // options binding is absent (e.g., tests that don't configure them).
            StartCompactionTimer();

            await base.OnActivateAsync(cancellationToken);
        }

        public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _compactionTimer?.Dispose();
            _compactionTimer = null;
            return base.OnDeactivateAsync(reason, cancellationToken);
        }

        private void StartCompactionTimer()
        {
            if (_compactionTimer is not null) return;
            var opts = GetCompactionOptions();
            if (opts is null || !opts.Enabled || opts.IntervalMinutes <= 0) return;
            if (GetSnapshotStore() is null) return;

            var interval = TimeSpan.FromMinutes(opts.IntervalMinutes);
            _compactionTimer = this.RegisterGrainTimer(
                async _ =>
                {
                    if (_deltasSinceLastSnapshot == 0) return; // nothing to compact
                    if (System.Threading.Interlocked.CompareExchange(ref _compactionInFlight, 1, 0) != 0) return;
                    try
                    {
                        await ForceSnapshotAsync();
                        System.Threading.Interlocked.Exchange(ref _deltasSinceLastSnapshot, 0);
                    }
                    catch (Exception ex)
                    {
                        var logger = GetLogger();
                        if (logger is not null) logger.LogError(ex, "Timer compaction failed");
                        else Console.WriteLine($"[GameMapGrain] Timer compaction failed: {ex.Message}");
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _compactionInFlight, 0);
                    }
                },
                state: (object?)null,
                new GrainTimerCreationOptions { DueTime = interval, Period = interval, Interleave = true });
        }

        /// <summary>
        /// Attempts to rebuild <see cref="_world"/> from a persisted snapshot at
        /// <c>(WorldId, MapId)</c>. Returns null when no snapshot is available so the
        /// caller can fall back to recipe-only regeneration. When a snapshot is found,
        /// post-snapshot deltas are replayed in sequence order via <see cref="MapDeltaReplayer"/>,
        /// and <see cref="_nextSequence"/> is advanced past the highest replayed sequence.
        /// </summary>
        private async Task<World?> TryHydrateFromSnapshotAsync()
        {
            var store = GetSnapshotStore();
            if (store is null || _mapState.State is null) return null;

            var regionSnapshot = await store.LoadSnapshotAsync(_mapState.State.WorldId, _mapState.State.MapId);
            if (regionSnapshot is null || regionSnapshot.SerializedEntities is null || regionSnapshot.SerializedEntities.Length == 0)
                return null;

            var serializer = ServiceProvider.GetService(typeof(Orleans.Serialization.Serializer))
                as Orleans.Serialization.Serializer;
            if (serializer is null) return null;

            // Snapshot blob is a full self-contained WorldSnapshot (recipe + entities).
            var worldSnapshot = serializer.Deserialize<WorldSnapshot>(regionSnapshot.SerializedEntities);

            // Version guard: if the stored snapshot was written by a binary that
            // understands a higher schema version, refuse to load rather than silently
            // mis-applying state. Operators can downgrade by restoring an older snapshot.
            if (worldSnapshot.SnapshotVersion > SupportedSnapshotVersion)
            {
                throw new Aetherium.Server.Persistence.PersistenceVersionMismatchException(
                    $"Snapshot for {_mapState.State.WorldId}/{_mapState.State.MapId} has SnapshotVersion={worldSnapshot.SnapshotVersion}, " +
                    $"but this binary only supports up to {SupportedSnapshotVersion}. " +
                    "Upgrade the server or restore a compatible snapshot.");
            }

            var builder = new Aetherium.WorldBuilders.SnapshotWorldBuilder(worldSnapshot, _generatorRegistry);
            var world = builder.Build();

            // Replay deltas appended after the snapshot's sequence so mid-snapshot mutations
            // are preserved. Each delta is idempotent; replay order is by Sequence ascending.
            var postDeltas = await store.GetMapDeltasSinceSequenceAsync(
                _mapState.State.WorldId, _mapState.State.MapId, regionSnapshot.LastSequence);

            long highestReplayed = regionSnapshot.LastSequence;
            foreach (var delta in postDeltas)
            {
                try
                {
                    MapDeltaReplayer.Apply(world, delta);
                    if (delta.Sequence > highestReplayed) highestReplayed = delta.Sequence;
                }
                catch (Exception ex)
                {
                    var logger = GetLogger();
                    if (logger is not null)
                        logger.LogError(ex, "Delta replay failed seq={Sequence} type={DeltaType}", delta.Sequence, delta.GetType().Name);
                    else
                        Console.WriteLine($"[GameMapGrain] Delta replay failed seq={delta.Sequence} type={delta.GetType().Name}: {ex.Message}");
                }
            }

            // Restore grain-authoritative heat trails captured in the snapshot (P3-8). Null on
            // snapshots written before heat persistence existed — the map simply starts with no
            // trails, as before.
            if (regionSnapshot.HeatTrails is { Count: > 0 })
            {
                _heatTracker.ImportTrails(regionSnapshot.HeatTrails.Select(t =>
                    new Aetherium.Server.Perception.HeatTrailTracker.HeatTrailEntry(
                        new Aetherium.Components.WorldLocation(t.X, t.Y, t.Z),
                        t.EntityId, t.Timestamp, t.BaseIntensity, t.Duration)));
            }

            // _nextSequence starts at 1 (default). After hydration, advance it past
            // whatever the snapshot+log covered so future appends don't collide.
            System.Threading.Interlocked.Exchange(ref _nextSequence, highestReplayed);

            GetLogger()?.LogInformation("Hydrated {MapId} from snapshot at seq={LastSequence}, replayed {DeltaCount} delta(s), {HeatCount} heat trail(s), next seq={NextSeq}",
                _mapState.State.MapId, regionSnapshot.LastSequence, postDeltas.Length, regionSnapshot.HeatTrails?.Count ?? 0, highestReplayed + 1);
            return world;
        }

        /// <summary>
        /// Subscribes the grain to its <c>_world.WorldEvents</c> stream. Currently the
        /// subscriber translates <see cref="WorldEventType.EntityMoved"/> events for
        /// entities carrying a <see cref="Aetherium.Components.HeatSignature"/> into
        /// heat trail records and emits <see cref="HeatRecordedDelta"/>. Other event
        /// types pass through silently — their corresponding deltas (door state, item
        /// transfers, etc.) are emitted directly from the mutation methods that have
        /// the necessary context.
        /// </summary>
        private void AttachWorldEventSubscriber()
        {
            if (_world is null || _worldEventsSubscriber is not null)
                return;

            _worldEventsSubscriber = OnWorldEvent;
            _world.WorldEvents += _worldEventsSubscriber;
        }

        /// <summary>
        /// The time base heat trails are stamped and aged against: Unix-epoch + accumulated game
        /// hours (so fade tracks game time). Falls back to wall-clock when no clock is available
        /// (e.g. minimal test fixtures). Used by both trail recording and snapshot export so the
        /// two stay consistent.
        /// </summary>
        private DateTime CurrentHeatTime()
        {
            var clock = ServiceProvider.GetService(typeof(Aetherium.Server.Simulation.WorldClock))
                as Aetherium.Server.Simulation.WorldClock;
            return clock is not null
                ? DateTime.UnixEpoch.AddHours(clock.GetTotalGameTimeHours())
                : DateTime.UtcNow;
        }

        private void OnWorldEvent(WorldEvent evt)
        {
            try
            {
                if (evt.EventType != WorldEventType.EntityMoved || evt.Entity is null)
                    return;

                var heatSig = evt.Entity.Get<Aetherium.Components.HeatSignature>();
                if (heatSig is null || heatSig.Intensity <= 0.0)
                    return;

                var location = evt.Location;
                var clock = ServiceProvider.GetService(typeof(Aetherium.Server.Simulation.WorldClock))
                    as Aetherium.Server.Simulation.WorldClock;
                var gameTime = CurrentHeatTime();

                _heatTracker.RecordEntityPosition(evt.Entity, location, gameTime);

                // Fire-and-forget delta emission — we're inside a synchronous event
                // callback so we can't await. The host-side broker doesn't depend on
                // ordering between deltas; each session reconciles independently.
                _ = FanOutAsync(new HeatRecordedDelta
                {
                    EntityId = evt.Entity.EntityId,
                    X = location.X,
                    Y = location.Y,
                    Z = location.Z,
                    GameTimeHours = clock?.GetTotalGameTimeHours() ?? 0,
                    Intensity = heatSig.Intensity,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameMapGrain] OnWorldEvent failed: {ex.Message}");
            }
        }

        private World RegenerateFromRecipe(WorldRecipe recipe)
        {
            var request = new WorldGenerationRequest
            {
                LayoutGenerator = recipe.GeneratorType,
                Width = recipe.Width,
                Height = recipe.Height,
                Levels = recipe.Levels,
                Seed = recipe.Seed,
                GeneratorVersion = recipe.GeneratorVersion,
                Template = recipe.Template,
                Parameters = new Dictionary<string, string>(recipe.Parameters, System.StringComparer.OrdinalIgnoreCase),
                Topology = recipe.Topology,
            };
            var orchestrator = new WorldGenerationOrchestrator(_generatorRegistry, BuildPasses(request.Template));
            var result = orchestrator.Generate(request);
            if (!result.Success || result.World is null)
                throw new InvalidOperationException("Failed to regenerate world from recipe on reactivation");
            return result.World;
        }

        public async Task InitializeAsync(string worldId, string mapName, WorldSize size, string generatorType, Dictionary<string, object> parameters, DeathPolicy? deathPolicy = null, AbilityConfig? abilityConfig = null, ProgressionConfig? progressionConfig = null, FactionConfig? factionConfig = null, Aetherium.Model.Content.ContentConfig? contentConfig = null, Aetherium.Model.Eca.EcaConfig? ecaConfig = null, string? topology = null)
        {
            var mapId = this.GetPrimaryKeyString();

            _deathPolicy = deathPolicy ?? DeathPolicy.Default;
            ApplyAbilityConfig(abilityConfig);
            ApplyProgressionConfig(progressionConfig);
            ApplyFactionConfig(factionConfig);
            ApplyContentConfig(contentConfig);

            parameters ??= new Dictionary<string, object>();
            // (ECA runtime is applied below, once the world seed is resolved.)

            var seed = parameters.TryGetValue("seed", out var seedObj) && seedObj is int seedInt
                ? seedInt
                : (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF);
            var context = new GeneratorContext(size.Width, size.Height, seed)
            {
                ZLevel = 0,
                Levels = size.Depth
            };

            var parameterStrings = parameters?.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty)
                ?? new Dictionary<string, string>();

            var request = new WorldGenerationRequest
            {
                LayoutGenerator = generatorType,
                Width = size.Width,
                Height = size.Height,
                Levels = size.Depth,
                Seed = seed,
                GeneratorVersion = parameters.TryGetValue("version", out var versionObj) ? versionObj?.ToString() ?? "1.0.0" : "1.0.0",
                Template = ResolveTemplate(generatorType),
                Parameters = parameterStrings,
                Topology = topology
            };

            var passes = BuildPasses(request.Template);
            var orchestrator = new WorldGenerationOrchestrator(_generatorRegistry, passes);
            var result = orchestrator.Generate(request);

            if (!result.Success || result.World == null)
            {
                var details = new List<string>();
                details.AddRange(result.Errors);
                if (result.Validation?.Errors != null)
                    details.AddRange(result.Validation.Errors);
                throw new InvalidOperationException($"Generation failed: {string.Join(", ", details)}");
            }

            _world = result.World;

            // Resolve the world's tiling once (docs/grid-topologies.md) — every adjacency/
            // distance/line/facing consumer reads World.Topology. Null/empty → square.
            _world.Topology = Aetherium.Topology.GridTopologyRegistry.Get(topology);

            // Data-driven population (add-content-definitions): the passes placed generic
            // monsters; the content catalog decides what they are. Same seed → same mix.
            ApplyContentPopulation(_world, seed);

            // Reactive logic (add-eca-scripting): compile the rule runtime, seeding its RNG from the
            // world seed so a given (seed, event order) reproduces the same firings.
            ApplyEcaConfig(ecaConfig, seed);

            // Runtime map validation (P1-22). Diagnostic, not gating: generation already
            // ran the pipeline's Validation-phase pass, so a MapValidator failure here
            // means the standards checker and the pipeline disagree — log it loudly
            // rather than refusing a world players could still explore.
            ValidateGeneratedWorld(_world, mapId);

            // Per-world memory policy overrides (see OpenSpec change add-character-memory).
            ApplyMemoryPolicy(_world, parameters);

            // Per-world recognition policy overrides (see OpenSpec change add-identity-recognition).
            ApplyRecognitionPolicy(_world, parameters);

            // Publish the live world to the in-process registry so operator/debug tooling
            // (headless sessions, world snapshots) can read/drive it directly. Published after
            // the setup above so consumers never observe a half-configured world.
            var worldRegistry = ServiceProvider.GetService<Aetherium.Server.Services.WorldRegistry>();
            worldRegistry?.Register(worldId, mapId, _world);

            // Partition map into regions and initialize region grains
            await PartitionIntoRegionsAsync();

            // Attach the WorldEvents subscriber that translates EntityMoved (for
            // heat-signature entities) into HeatRecordedDelta fan-out. Must happen
            // after _world is assigned but before any other code triggers movement.
            AttachWorldEventSubscriber();

            // Register map and portals with cluster if world belongs to a cluster
            // Get cluster ID from world grain
            var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);
            var worldInfo = await worldGrain.GetInfoAsync();
            
            if (worldInfo?.ClusterId != null && !string.IsNullOrEmpty(worldInfo.ClusterId))
            {
                var clusterGrain = _grainFactory.GetGrain<IClusterGrain>(worldInfo.ClusterId);
                
                // Register map with cluster (creates market)
                await clusterGrain.RegisterMapAsync(worldId, mapId);
                
                // Find and register portals from the generated world
                await RegisterPortalsWithClusterAsync(clusterGrain, worldId, mapId);
            }

            // Capture the recipe that produced this world so we can serve deterministic
            // snapshots later (GetWorldSnapshotAsync) and rehydrate on grain reactivation
            // (OnActivateAsync). Phase 1 persistence story; phase 2 will replace this
            // with real World serialization.
            var recipe = new WorldRecipe
            {
                GeneratorType = generatorType,
                Seed = seed,
                Parameters = new Dictionary<string, string>(parameterStrings, System.StringComparer.OrdinalIgnoreCase),
                Template = request.Template,
                GeneratorVersion = request.GeneratorVersion,
                Width = size.Width,
                Height = size.Height,
                Levels = size.Depth,
                Topology = topology,
            };

            _mapState.State = new MapState
            {
                MapId = mapId,
                WorldId = worldId,
                MapName = mapName,
                Size = size,
                GeneratorType = generatorType,
                PlayerIds = new HashSet<string>(),
                CreatedAt = DateTime.UtcNow,
                Recipe = recipe,
                DeathPolicy = deathPolicy,
                AbilityConfig = abilityConfig,
                ProgressionConfig = progressionConfig,
                FactionConfig = factionConfig,
                ContentConfig = contentConfig,
                EcaConfig = ecaConfig,
                Topology = topology,
            };

            await _mapState.WriteStateAsync();
        }

        private static void ValidateGeneratedWorld(World world, string mapId)
        {
            var report = new Aetherium.WorldBuilders.Validation.MapValidator().Validate(
                world,
                new Aetherium.WorldBuilders.Validation.MapValidationOptions
                {
                    ZLevel = 0,
                    // Boundary/lighting standards aren't guaranteed by every generator
                    // yet; terrain-type registration is, and an unregistered terrain
                    // breaks passability checks silently downstream.
                    RequireExplicitBoundary = false,
                    RequireLightSource = false,
                });

            if (!report.IsValid)
            {
                foreach (var error in report.Errors)
                    Console.WriteLine($"[GameMapGrain] Map validation error ({mapId}): {error}");
            }
        }

        /// <summary>
        /// Applies memory-policy overrides from world generator parameters
        /// (MemoryEnabled, MemoryMaxLocations, MemoryDecayHalfLifeSeconds).
        /// </summary>
        private static void ApplyMemoryPolicy(World world, Dictionary<string, object> parameters)
        {
            if (parameters == null)
                return;

            if (parameters.TryGetValue("MemoryEnabled", out var enabledObj)
                && bool.TryParse(enabledObj?.ToString(), out var enabled))
                world.MemoryPolicy.Enabled = enabled;

            if (parameters.TryGetValue("MemoryMaxLocations", out var maxObj)
                && int.TryParse(maxObj?.ToString(), out var max) && max > 0)
                world.MemoryPolicy.MaxLocations = max;

            if (parameters.TryGetValue("MemoryDecayHalfLifeSeconds", out var halfObj)
                && double.TryParse(halfObj?.ToString(), out var half))
                world.MemoryPolicy.DecayHalfLifeSeconds = half;

            // Dynamics overrides (add-memory-dynamics): opt-in reinforcement/permanence/forgetting.
            if (parameters.TryGetValue("MemoryDynamicsEnabled", out var dynObj)
                && bool.TryParse(dynObj?.ToString(), out var dyn))
                world.MemoryPolicy.DynamicsEnabled = dyn;

            if (parameters.TryGetValue("MemoryStabilityGrowthFactor", out var growthObj)
                && double.TryParse(growthObj?.ToString(), out var growth) && growth > 0)
                world.MemoryPolicy.StabilityGrowthFactor = growth;

            if (parameters.TryGetValue("MemoryMinReinforcementIntervalSeconds", out var minObj)
                && double.TryParse(minObj?.ToString(), out var min) && min >= 0)
                world.MemoryPolicy.MinReinforcementIntervalSeconds = min;

            if (parameters.TryGetValue("MemoryPermanenceThresholdSeconds", out var permObj)
                && double.TryParse(permObj?.ToString(), out var perm) && perm > 0)
                world.MemoryPolicy.PermanenceThresholdSeconds = perm;

            if (parameters.TryGetValue("MemoryForgetThreshold", out var forgetObj)
                && double.TryParse(forgetObj?.ToString(), out var forget))
                world.MemoryPolicy.ForgetThreshold = forget;
        }

        /// <summary>
        /// Applies recognition-policy overrides from world generator parameters
        /// (add-identity-recognition): Recognition{Enabled,RangeTiles,OwnKindAcuity,OtherKindAcuity,
        /// Threshold,EncounterTimeoutSeconds,FamiliarityHalfLifeSeconds,MeetStrength,MaxIndividuals}.
        /// </summary>
        private static void ApplyRecognitionPolicy(World world, Dictionary<string, object> parameters)
        {
            if (parameters == null)
                return;

            var p = world.RecognitionPolicy;

            if (parameters.TryGetValue("RecognitionEnabled", out var enObj)
                && bool.TryParse(enObj?.ToString(), out var en))
                p.Enabled = en;
            if (parameters.TryGetValue("RecognitionRangeTiles", out var rtObj)
                && int.TryParse(rtObj?.ToString(), out var rt) && rt > 0)
                p.RangeTiles = rt;
            if (parameters.TryGetValue("RecognitionOwnKindAcuity", out var okObj)
                && double.TryParse(okObj?.ToString(), out var ok))
                p.OwnKindAcuity = ok;
            if (parameters.TryGetValue("RecognitionOtherKindAcuity", out var otObj)
                && double.TryParse(otObj?.ToString(), out var ot))
                p.OtherKindAcuity = ot;
            if (parameters.TryGetValue("RecognitionThreshold", out var thObj)
                && double.TryParse(thObj?.ToString(), out var th))
                p.RecognitionThreshold = th;
            if (parameters.TryGetValue("RecognitionEncounterTimeoutSeconds", out var etObj)
                && double.TryParse(etObj?.ToString(), out var et) && et >= 0)
                p.EncounterTimeoutSeconds = et;
            if (parameters.TryGetValue("RecognitionFamiliarityHalfLifeSeconds", out var fhObj)
                && double.TryParse(fhObj?.ToString(), out var fh) && fh > 0)
                p.FamiliarityHalfLifeSeconds = fh;
            if (parameters.TryGetValue("RecognitionMeetStrength", out var msObj)
                && double.TryParse(msObj?.ToString(), out var ms))
                p.MeetStrength = ms;
            if (parameters.TryGetValue("RecognitionMaxIndividuals", out var miObj)
                && int.TryParse(miObj?.ToString(), out var mi) && mi > 0)
                p.MaxIndividuals = mi;
        }

        /// <summary>
        /// Resolves an entity's "kind" for recognition (add-identity-recognition): its
        /// <see cref="Aetherium.Components.CreatureTypeTag"/> value when present (so "wolf"/"bandit"
        /// don't collapse to "monster"), else its CLR type name lowercased. Same rule the
        /// <c>creature_died</c> path uses for the victim.
        /// </summary>
        internal static string ResolveKind(Entity entity) =>
            Aetherium.Components.RecognitionKind.Resolve(entity);

        /// <summary>
        /// Sweeps the canonical world for characters within recognition range (add-identity-recognition)
        /// and raises <c>character_recognized</c> rules once per encounter. Runs on the grain turn after
        /// <see cref="StepNpcsAsync"/>, so PC canonical bodies (gateway-first) and NPCs are both at their
        /// settled positions. Proximity uses the world topology on the same z-level. Familiarity state is
        /// updated for every in-range pair regardless of whether rules exist; only recognized, new-
        /// encounter observations dispatch through the rule runtime. No-op when the policy is disabled.
        /// </summary>
        private async Task RunRecognitionSweepAsync()
        {
            if (_world is null)
                return;
            var policy = _world.RecognitionPolicy;
            if (!policy.Enabled)
                return;

            var characters = _world.Characters.Values
                .Where(c => c.Has<Aetherium.Components.WorldLocation>()
                            && !c.Has<Dying>() && !c.Has<Corpse>() && !c.Has<Downed>())
                .ToList();
            if (characters.Count < 2)
                return;

            var now = DateTime.UtcNow;
            var events = new List<Aetherium.Server.Eca.EcaEventContext>();

            foreach (var recognizer in characters)
            {
                var profile = recognizer.Has<Aetherium.Components.RecognitionProfile>()
                    ? recognizer.Get<Aetherium.Components.RecognitionProfile>()
                    : null;
                // A character can opt out of being a recognizer without disabling the whole world.
                if (profile?.EnabledOverride == false)
                    continue;

                var range = profile?.RangeTilesOverride ?? policy.RangeTiles;
                var recLoc = recognizer.Get<Aetherium.Components.WorldLocation>();
                var recCoord = Aetherium.Topology.GridCoord.From(recLoc);
                var recKind = ResolveKind(recognizer);

                Aetherium.Components.IndividualRecognition rec;
                if (recognizer.Has<Aetherium.Components.IndividualRecognition>())
                    rec = recognizer.Get<Aetherium.Components.IndividualRecognition>();
                else
                {
                    rec = new Aetherium.Components.IndividualRecognition();
                    recognizer.Set(rec);
                }

                foreach (var target in characters)
                {
                    if (target.EntityId == recognizer.EntityId)
                        continue;
                    var tgtLoc = target.Get<Aetherium.Components.WorldLocation>();
                    if (tgtLoc.Z != recLoc.Z)
                        continue; // same z-level only this slice

                    var distance = _world.Topology.Distance(recCoord, Aetherium.Topology.GridCoord.From(tgtLoc));
                    if (distance > range)
                        continue;

                    var tgtKind = ResolveKind(target);
                    var obs = rec.Observe(target.EntityId, tgtKind, now, policy);

                    var acuity = profile != null
                        ? profile.AcuityFor(recKind, tgtKind, policy)
                        : policy.AcuityFor(recKind, tgtKind);

                    if (policy.Recognizes(acuity, obs.EffectiveFamiliarity) && obs.NewEncounter)
                    {
                        events.Add(new Aetherium.Server.Eca.EcaEventContext
                        {
                            TriggerKind = Aetherium.Server.Eca.CharacterRecognizedTrigger.Id,
                            RecognizerEntityId = recognizer.EntityId,
                            RecognizerKind = recKind,
                            RecognizedEntityId = target.EntityId,
                            RecognizedKind = tgtKind,
                            Familiarity = obs.EffectiveFamiliarity,
                            FirstMeeting = obs.FirstMeeting,
                            EventX = recLoc.X,
                            EventY = recLoc.Y,
                            EventZ = recLoc.Z,
                        });
                    }
                }
            }

            // Dispatch through the rule runtime (mirrors RunCreatureDiedRulesAsync). Familiarity state
            // above is updated regardless; this only executes rules for recognized new encounters.
            if (_ecaRuntime is not null)
            {
                foreach (var evt in events)
                    foreach (var request in _ecaRuntime.Evaluate(evt))
                        await ExecuteEcaRequestAsync(request);
            }
        }

        private static WorldGenerationTemplate ResolveTemplate(string generatorType)
        {
            var normalized = generatorType.ToLowerInvariant();
            if (normalized.Contains("outdoor") || normalized.Contains("terrain"))
                return WorldGenerationTemplate.Outdoor;
                return WorldGenerationTemplate.Dungeon;
        }

        private static IWorldGenerationPass[] BuildPasses(WorldGenerationTemplate template)
            => WorldGenerationPassCatalog.BuildPasses(template);

        public Task<string?> GetWorldAsync()
        {
            if (_world == null || _mapState.State == null)
                return Task.FromResult<string?>(null);

            // Serialize an omniscient snapshot (tiles + entities) of this map's live world.
            var snapshot = Aetherium.Server.Management.WorldSnapshotBuilder.Build(
                _world, _mapState.State.WorldId, _mapState.State.MapId);
            return Task.FromResult<string?>(System.Text.Json.JsonSerializer.Serialize(snapshot));
        }

        public Task<MapMetadata?> GetMetadataAsync()
        {
            if (_mapState.State == null)
                return Task.FromResult<MapMetadata?>(null);

            var metadata = new MapMetadata
            {
                MapId = _mapState.State.MapId,
                WorldId = _mapState.State.WorldId,
                MapName = _mapState.State.MapName,
                Size = _mapState.State.Size,
                GeneratorType = _mapState.State.GeneratorType,
                PlayerCount = _mapState.State.PlayerIds.Count,
                CreatedAt = _mapState.State.CreatedAt
            };

            return Task.FromResult<MapMetadata?>(metadata);
        }

        public async Task<bool> AddPlayerAsync(string playerId)
        {
            if (_mapState.State == null)
                return false;

            bool added = _mapState.State.PlayerIds.Add(playerId);
            if (added)
            {
                await _mapState.WriteStateAsync();
            }

            return added;
        }

        public async Task<JoinMapResult> JoinPlayerAsync(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
                return JoinMapResult.Fail("playerId required");

            if (_mapState.State is null || _world is null)
                return JoinMapResult.Fail("Map not initialized");

            if (_mapState.State.PlayerIds.Contains(playerId))
                return JoinMapResult.Fail("Player already on map");

            var spawn = SelectUnusedSpawn();
            if (spawn is null)
                return JoinMapResult.Fail("No passable spawn locations available");

            _spawnsInUse.Add(spawn);
            _playerSpawns[playerId] = spawn;
            _mapState.State.PlayerIds.Add(playerId);
            await _mapState.WriteStateAsync();

            // Phase 2: add the player's Character to _world so other joiners see them
            // and the grain owns canonical heading/inventory state. EntityId == playerId
            // keeps the mapping from sessionId to in-world entity trivially direct.
            var character = new Character { EntityId = playerId };
            character.Set(new Aetherium.Components.WorldLocation(spawn.X, spawn.Y, spawn.Z));
            character.Set(new Aetherium.Components.Inventory());
            character.Set(new Aetherium.Components.HasHeading { Heading = 0 });

            // Stamp the world's configured resource pools onto the joining character (engine
            // gap-analysis §4.3 — see wire-abilities-live). Fresh instances per character so each
            // owns its own mutable pool state. A world that declares no pools leaves the character
            // with none — the engine never invents a genre-specific pool.
            if (_characterResourcePools.Count > 0)
                character.Set(CreateAbilityCompiler().BuildResourcePools(_characterResourcePools));

            // Stamp the world's progression components (engine gap-analysis §4.4 — see
            // wire-progression-live): fresh XP pools, attributes, role affinity, unlocked-skills and
            // granted-abilities sets, then apply any attribute→stat derivations from the starting
            // attributes. A world declaring no progression leaves the character with none, keeping
            // Character genre-neutral (same posture as resource pools).
            if (_progressionConfig is not null)
            {
                var pc = new ProgressionCompiler();
                character.Set(pc.BuildProgressPools(_progressionConfig.Pools));
                character.Set(pc.BuildAttributes(_progressionConfig.StartingAttributes));
                character.Set(pc.BuildRoleAffinity(_progressionConfig.StartingRoleAffinity));
                character.Set(new UnlockedSkills());
                character.Set(new GrantedAbilities());
                ApplyAttributeDerivations(character);
            }

            // Stamp a fresh reputation ledger seeded with each faction's starting standing (engine
            // gap-analysis §4.6 — see wire-factions-live). A world declaring no factions leaves the
            // character without a ledger, same posture as pools/progression.
            if (_factionConfig is not null)
            {
                var ledger = new ReputationLedger();
                foreach (var def in _factionConfig.Factions)
                {
                    var reputation = new Reputation(def.Id, def.StartingStanding);
                    RankEvaluator.Apply(reputation, def.RankRules);
                    ledger.Add(reputation);
                }
                character.Set(ledger);
            }

            _world.AddEntity(character);

            // Fan out an EntityAddedDelta so other sessions in the map group see the
            // joiner appear. The joiner themselves will skip this when reconciling
            // because their snapshot omitted their own Character — but it's harmless
            // either way (AddEntity on their mirror would reject duplicate ID).
            await FanOutAsync(new EntityAddedDelta
            {
                MapId = _mapState.State.MapId,
                Placement = EntityPlacement.FromLocation(playerId, nameof(Character), spawn),
            });

            return JoinMapResult.Ok(_mapState.State.MapId, spawn, playerId);
        }

        public async Task LeavePlayerAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId) || _world is null || _mapState.State is null)
                return;

            // Remove from grain bookkeeping (mirrors RemovePlayerAsync but also
            // strips the Character entity and emits a delta for observers).
            _mapState.State.PlayerIds.Remove(sessionId);
            if (_playerSpawns.Remove(sessionId, out var spawn))
                _spawnsInUse.Remove(spawn);

            // Take the character's last known location for the delta before removing.
            int x = 0, y = 0, z = 0;
            if (_world.Entities.TryGetValue(sessionId, out var entity))
            {
                var loc = entity.Get<Aetherium.Components.WorldLocation>();
                if (loc is not null) { x = loc.X; y = loc.Y; z = loc.Z; }
                _world.TryRemoveEntity(sessionId);
            }

            await _mapState.WriteStateAsync();

            await FanOutAsync(new EntityRemovedDelta
            {
                MapId = _mapState.State.MapId,
                EntityId = sessionId,
                LastX = x, LastY = y, LastZ = z,
            });
        }

        public Task<WorldSnapshot> GetWorldSnapshotForJoinerAsync(string joinerPlayerId)
        {
            if (_mapState.State is null || _world is null || _mapState.State.Recipe is null)
                throw new InvalidOperationException("Map not initialized");

            var snapshot = WorldSnapshotBuilder.SnapshotOf(
                _world,
                _mapState.State.Recipe,
                _mapState.State.WorldId,
                _mapState.State.MapId,
                _mapState.State.Size,
                excludePlayerEntityId: joinerPlayerId);

            // Ghost forensics: the session mirror can only ever know what this snapshot
            // carries — log canonical vs captured so a canonical→snapshot leak is visible.
            var canonicalMonsters = _world.Entities.Values.OfType<Aetherium.Monster>().Count();
            var snapshotMonsters = snapshot.Entities.Count(p => p.TypeName == nameof(Aetherium.Monster));
            Console.WriteLine(
                $"[GameMapGrain] joiner snapshot for {joinerPlayerId}: {snapshot.Entities.Count} placements " +
                $"({snapshotMonsters} monsters) from canonical {_world.Entities.Count} entities ({canonicalMonsters} monsters)");

            return Task.FromResult(snapshot);
        }

        public Task<WorldSnapshot> GetWorldSnapshotAsync()
        {
            if (_mapState.State is null || _world is null || _mapState.State.Recipe is null)
                throw new InvalidOperationException("Map not initialized");

            var snapshot = WorldSnapshotBuilder.SnapshotOf(
                _world,
                _mapState.State.Recipe,
                _mapState.State.WorldId,
                _mapState.State.MapId,
                _mapState.State.Size);

            return Task.FromResult(snapshot);
        }

        /// <summary>
        /// Picks a passable location not currently held by another joined player.
        /// Walks the passable set once rather than rejection-sampling, so worlds with
        /// nearly-saturated spawn space don't loop forever.
        /// </summary>
        private Aetherium.Components.WorldLocation? SelectUnusedSpawn()
        {
            if (_world is null)
                return null;

            // World.SelectRandomPassableLocation builds its candidate list internally.
            // For up to a handful of joiners we can just retry; for many joiners we'd
            // prefer to enumerate once. Phase 1 keeps it simple.
            for (int attempt = 0; attempt < 32; attempt++)
            {
                var candidate = _world.SelectRandomPassableLocation();
                if (candidate is null)
                    return null;
                // Passable terrain isn't enough: population passes place monsters on
                // passable cells, and spawning a player inside one creates a stacked
                // state that movement validation would never allow.
                if (!_spawnsInUse.Contains(candidate) && _world.IsOpenForOccupancy(candidate))
                    return candidate;
            }

            // Fall back to exhaustive scan if random retries can't find a free cell.
            foreach (var loc in _world.EntitiesByLocation.Keys)
            {
                if (_world.IsOpenForOccupancy(loc) && !_spawnsInUse.Contains(loc))
                    return loc;
            }

            return null;
        }

        public async Task RemovePlayerAsync(string playerId)
        {
            if (_mapState.State == null)
                return;

            bool removed = _mapState.State.PlayerIds.Remove(playerId);

            // Free the spawn so a future joiner can reuse it.
            if (_playerSpawns.Remove(playerId, out var spawn))
                _spawnsInUse.Remove(spawn);

            if (removed)
            {
                await _mapState.WriteStateAsync();
            }
        }

        public Task<List<string>> GetPlayersAsync()
        {
            if (_mapState.State == null)
                return Task.FromResult(new List<string>());

            return Task.FromResult(new List<string>(_mapState.State.PlayerIds));
        }

        /// <summary>
        /// Computes a perception snapshot (serialized <c>PerceptionDto</c>) for an
        /// in-world entity, from the canonical <see cref="_world"/>. Used by
        /// autonomous agents that occupy the map as a Character (via
        /// <see cref="JoinPlayerAsync"/>) but have no SignalR session to hydrate a
        /// local mirror — they pull perception each step instead. Returns null if the
        /// map isn't initialized or the entity isn't present.
        /// </summary>
        public Task<string?> ComputeAgentPerceptionAsync(string entityId)
        {
            if (_world is null)
                return Task.FromResult<string?>(null);

            var character = GetPlayerCharacter(entityId);
            var location = character?.Get<Aetherium.Components.WorldLocation>();
            if (character is null || location is null)
                return Task.FromResult<string?>(null);

            // Nearest-cardinal bearing for the perception view — same boundaries the
            // deleted DegreesToCardinal helper used (WorldDirection is square-legacy
            // cosmetics; degrees are the facing source of truth).
            var bearing = character.Get<Aetherium.Components.HasHeading>()?.ToWorldDirection()
                ?? Aetherium.WorldDirection.North;

            // Honor the creature's per-type vision (content.yaml `vision:`): a directional
            // creature only perceives its forward cone, exactly as the human player does when
            // directional vision is on — so an AI hunter's field of view is a real constraint,
            // not just a client presentation detail.
            var heading = character.Get<Aetherium.Components.HasHeading>();
            bool directional = heading?.IsDirectional ?? false;
            int? headingDegrees = directional ? heading!.Heading : (int?)null;
            int? fovDegrees = directional ? heading!.FieldOfViewDegrees : (int?)null;

            // Passing the character as `self` populates the interoception channel — the
            // player's own health/statuses/pools/cooldowns (add-interoception-channel).
            var perception = new Aetherium.Server.PerceptionService()
                .ComputePerception(
                    _world, location, bearing, new System.Drawing.Size(40, 40),
                    Aetherium.Model.LightingMode.Torch, Aetherium.Model.VisionMode.Normal,
                    heatTracker: null, currentTime: System.DateTime.UtcNow,
                    directionalVision: directional, headingDegrees: headingDegrees, fovDegrees: fovDegrees,
                    self: character);
            var json = System.Text.Json.JsonSerializer.Serialize(perception);
            return Task.FromResult<string?>(json);
        }

        public async Task TickAsync(TimeSpan gameTimeElapsed)
        {
            if (_mapState.State == null || _regions == null)
                return;

            var tickTs0 = NpcPerfLog ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

            // Advance autonomous flyers (flight plans) on this map's world.
            if (_world != null)
                Aetherium.Server.Flying.FlightPlanSystem.Step(_world);

            // Tick all regions in parallel with game time
            var tickTasks = _regions.Values
                .Select(region => region.TickAsync(gameTimeElapsed))
                .ToList();

            await Task.WhenAll(tickTasks);

            var msRegions = NpcPerfLog ? System.Diagnostics.Stopwatch.GetElapsedTime(tickTs0).TotalMilliseconds : 0.0;
            var tickTs1 = NpcPerfLog ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

            // Drive NPC/monster behavior. Runs on the grain's activation turn, so it
            // is serialized with player mutations on the same _world — no cross-thread
            // races. Movement + its delta fan-out happen here rather than in the
            // Monster entity so co-located players actually see monsters move.
            _tickCounter++;
            await StepNpcsAsync();

            var msNpc = NpcPerfLog ? System.Diagnostics.Stopwatch.GetElapsedTime(tickTs1).TotalMilliseconds : 0.0;
            var tickTsRec = NpcPerfLog ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

            // Individual recognition (add-identity-recognition): after positions settle, sweep the
            // canonical world for characters within recognition range and raise character_recognized
            // rules. No-op when the world's RecognitionPolicy is disabled (the default).
            await RunRecognitionSweepAsync();

            var msRecognition = NpcPerfLog ? System.Diagnostics.Stopwatch.GetElapsedTime(tickTsRec).TotalMilliseconds : 0.0;
            var tickTs2 = NpcPerfLog ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

            // Death lifecycle bookkeeping (engine gap-analysis §4.2/§4.11): advance every Dying
            // entity's countdown to Corpse, then age/expire corpses per DeathPolicy. Both are
            // silent, canonical-state-only ticks — no delta is emitted for a Dying→Corpse
            // transition or a corpse's removal, since nothing renders that distinction yet (a
            // future content-atlas/UI slice would add it). DeathPolicy.Default's
            // CorpseRetentionTicks is int.MaxValue, so CorpseExpirySystem is a no-op today —
            // wired for correctness/completeness, ready for a future per-world death policy.
            if (_world is not null)
            {
                _deathSystem.Tick(_world);
                _corpseExpirySystem.Tick(_world, _deathPolicy);

                // Ability upkeep (engine gap-analysis §4.3 — see wire-abilities-live): count down
                // every actor's ability cooldowns and regenerate their resource pools. Silent,
                // canonical-state-only — the read accessors expose the new values on demand.
                TickAbilityUpkeep();
            }

            var msDeathAbility = NpcPerfLog ? System.Diagnostics.Stopwatch.GetElapsedTime(tickTs2).TotalMilliseconds : 0.0;
            var tickTs3 = NpcPerfLog ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

            // Player death lifecycle (engine gap-analysis §4.11, Phase 2 — see
            // wire-death-respawn-live): advance every Downed player's countdown and any
            // post-respawn invulnerability window.
            await TickDownedAndInvulnerablePlayersAsync();

            var msDowned = NpcPerfLog ? System.Diagnostics.Stopwatch.GetElapsedTime(tickTs3).TotalMilliseconds : 0.0;
            var tickTs4 = NpcPerfLog ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

            // Heat trail cleanup. Capture which cells had trails before cleanup, run
            // cleanup, then diff to find cells that just lost their last trail. Emit
            // HeatExpiredDelta only for those — don't spam expiries every tick for
            // already-empty cells.
            try
            {
                var clock = ServiceProvider.GetService(typeof(Aetherium.Server.Simulation.WorldClock))
                    as Aetherium.Server.Simulation.WorldClock;
                var cutoff = DateTime.UtcNow.AddSeconds(-60);

                var beforeCells = new HashSet<Aetherium.Components.WorldLocation>(_heatTracker.SnapshotCounts().Keys);
                _heatTracker.CleanupOldTrails(cutoff);
                var afterCells = new HashSet<Aetherium.Components.WorldLocation>(_heatTracker.SnapshotCounts().Keys);

                var expired = beforeCells.Except(afterCells).ToList();
                foreach (var loc in expired)
                {
                    await FanOutAsync(new HeatExpiredDelta
                    {
                        X = loc.X,
                        Y = loc.Y,
                        Z = loc.Z,
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameMapGrain] Heat cleanup failed: {ex.Message}");
            }

            if (NpcPerfLog)
            {
                var msHeat = System.Diagnostics.Stopwatch.GetElapsedTime(tickTs4).TotalMilliseconds;
                var msTotal = System.Diagnostics.Stopwatch.GetElapsedTime(tickTs0).TotalMilliseconds;
                var entityCount = _world?.Entities.Count ?? 0;
                Console.WriteLine(
                    $"[PERF] tick total={msTotal:F0}ms | regions({_regions.Count})={msRegions:F0} stepNpcs={msNpc:F0} " +
                    $"recognition={msRecognition:F0} deathAbility={msDeathAbility:F0} downed={msDowned:F0} heat={msHeat:F0} " +
                    $"| entities={entityCount} chars={_world?.Characters.Count ?? 0}");
            }
        }

        /// <summary>
        /// Advances every monster on this map by one behavior-tree tick (engine gap-analysis
        /// §4.5 Phase 2 — see openspec/changes/add-npc-behavior-trees), when NPC behavior is
        /// enabled and this tick falls on the configured interval. Each monster owns its own
        /// <see cref="BehaviorTree"/> instance (cached in <see cref="_monsterTrees"/>) built from
        /// <see cref="MonsterBehaviors.BuildWanderAndMeleeTree"/> — attack an adjacent player if
        /// one is in reach, else wander — reproducing the decision this method made inline before
        /// the tree took over. Each monster's tree tick is gated on its <see cref="ActionSpeed"/>
        /// budget (engine gap-analysis §4.1): it acts only on ticks where it can afford
        /// <see cref="NpcActionCost"/>, so speed differentiates action cadence with no global turn
        /// order. World mutations happen synchronously (no awaits between them) so a reentrant
        /// player move cannot interleave mid-sweep; the resulting deltas are broadcast afterward.
        /// Each landed move also lays a heat trail via the world-event subscriber, exactly as a
        /// player move does.
        /// </summary>
        private async Task StepNpcsAsync()
        {
            if (_world is null) return;

            var options = _simulationOptions?.Value;
            if (options is not null && !options.EnableNpcBehavior)
                return;

            var interval = Math.Max(1, options?.NpcMoveIntervalTicks ?? 1);
            if (_tickCounter % interval != 0)
                return;

            // Snapshot the monster set first — TryMoveSteps mutates the location
            // index we would otherwise be enumerating.
            var monsters = _world.Entities.Values.OfType<Aetherium.Monster>().ToList();

            // A monster that died/was removed since the last tick no longer needs a tree —
            // drop it so _monsterTrees doesn't grow unbounded over a map's lifetime.
            if (_monsterTrees.Count > 0)
            {
                var liveIds = monsters.Select(m => m.EntityId).ToHashSet();
                foreach (var staleId in _monsterTrees.Keys.Where(id => !liveIds.Contains(id)).ToList())
                    _monsterTrees.Remove(staleId);
            }

            if (monsters.Count == 0)
                return;

            // Snapshot the player characters too — retaliation targets them. Players are
            // exactly the entities whose EntityId is a joined playerId (monsters are never
            // in PlayerIds), so this is unambiguous even though Monster : Character. Scoping
            // the tree's target list to this list (rather than every Health-bearing entity)
            // keeps monsters from attacking each other.
            var players = new List<Entity>();
            if (_mapState.State is not null)
            {
                foreach (var playerId in _mapState.State.PlayerIds)
                {
                    if (_world.Entities.TryGetValue(playerId, out var pe)
                        && pe is Character pc
                        && pc.Has<Aetherium.Components.WorldLocation>()
                        // A downed/permadead player has already been dealt with; a freshly-
                        // respawned one is briefly untargetable (engine gap-analysis §4.11 —
                        // see wire-death-respawn-live).
                        && !pc.Has<Downed>() && !pc.Has<Corpse>() && !pc.Has<RespawnInvulnerable>())
                        players.Add(pc);
                }
            }

            // Phase 1 (synchronous): mutate the world, collecting the moves and combat
            // hits that landed. No awaits here, so the sweep is atomic w.r.t. any
            // reentrant player turn.
            var moves = new List<EntityMovedDelta>();
            var combatDeltas = new List<MapDelta>();
            var playerEvents = new List<(string SessionId, string EventName, Aetherium.Model.PlayerVitalsDto Vitals)>();
            foreach (var monster in monsters)
            {
                // A monster killed by a player's attack (engine gap-analysis §4.2/§4.11) is no
                // longer removed from _world — it persists as Dying, then Corpse, for a future
                // loot/harvest/revive affordance. It must not keep acting: skip its tree tick (and
                // don't spend its action budget) while in either state.
                if (monster.Has<Dying>() || monster.Has<Corpse>())
                    continue;

                // Continuous action pipeline (engine gap-analysis §4.1): a monster acts only when
                // it has accrued enough AP this tick. TrySpend refills its budget and, if the cost
                // is covered, deducts it and returns true; otherwise the monster keeps accruing and
                // skips this tick. A default Monster (Speed == MaxBudget == NpcActionCost) affords
                // every eligible tick — parity with pre-pipeline behavior — while a slower creature
                // acts on a sub-cadence. Monsters without an ActionSpeed (none today) always act.
                if (monster.Has<Aetherium.Components.ActionSpeed>()
                    && !monster.Get<Aetherium.Components.ActionSpeed>().TrySpend(NpcActionCost))
                    continue;

                if (!_monsterTrees.TryGetValue(monster.EntityId, out var tree))
                {
                    tree = BuildBehaviorTreeFor(monster);
                    _monsterTrees[monster.EntityId] = tree;
                }

                tree.Blackboard.Set<IReadOnlyList<Entity>>(MonsterBehaviors.TargetsKey, players);
                tree.Blackboard.Clear(MonsterBehaviors.AttackOutcomeKey);
                tree.Blackboard.Clear(MonsterBehaviors.MoveOutcomeKey);

                tree.Tick(_world, monster);

                if (tree.Blackboard.TryGet<AttackOutcome>(MonsterBehaviors.AttackOutcomeKey, out var attack))
                {
                    // A lethal hit against a player (engine gap-analysis §4.11 — see
                    // wire-death-respawn-live) routes through the active DeathPolicy instead of
                    // just reporting HP=0: instant respawn, entering Downed, or (permadeath, no
                    // down state) an instant Corpse transition. Non-lethal hits, and any hit
                    // against a monster, keep reporting a plain health delta as before.
                    if (attack.RemainingHealth <= 0
                        && _mapState.State is not null && _mapState.State.PlayerIds.Contains(attack.TargetEntityId)
                        && _world.Entities.TryGetValue(attack.TargetEntityId, out var victimEntity)
                        && victimEntity is Character victim)
                    {
                        ResolvePlayerLethalHit(victim, moves, combatDeltas, playerEvents);
                    }
                    else
                    {
                        combatDeltas.Add(IntFieldDelta(attack.TargetEntityId, "Health", "Level", attack.RemainingHealth));
                    }
                }
                else if (tree.Blackboard.TryGet<WanderOutcome>(MonsterBehaviors.MoveOutcomeKey, out var move))
                {
                    moves.Add(new EntityMovedDelta
                    {
                        EntityId = monster.EntityId,
                        OldX = move.From.X, OldY = move.From.Y, OldZ = move.From.Z,
                        NewX = move.To.X, NewY = move.To.Y, NewZ = move.To.Z,
                    });
                }
            }

            if (NpcPerfLog)
            {
                var now = Environment.TickCount64;
                if (_npcPerfWindowStartMs < 0) _npcPerfWindowStartMs = now;
                _npcPerfTicks++;
                _npcPerfMoves += moves.Count;
                _npcPerfLastMonsterCount = monsters.Count;
                var elapsed = now - _npcPerfWindowStartMs;
                if (elapsed >= 2000)
                {
                    Console.WriteLine(
                        $"[PERF] npc {this.GetPrimaryKeyString()}: monsters={_npcPerfLastMonsterCount}, " +
                        $"{_npcPerfTicks} steps in {elapsed}ms, {_npcPerfMoves} moves fanned out " +
                        $"({_npcPerfMoves * 1000.0 / elapsed:F1}/s)");
                    _npcPerfTicks = 0; _npcPerfMoves = 0; _npcPerfWindowStartMs = now;
                }
            }

            // Phase 2 (async): broadcast the whole tick as ONE batch. The session manager applies
            // every delta under a single per-session lock and schedules a single perception flush —
            // instead of one lock acquisition + flush schedule per monster, which contended with the
            // flush and pinned this single-threaded grain (starving player commands). Moves first,
            // then combat, preserving the order observers would have seen per-delta.
            var tickDeltas = new List<MapDelta>(moves.Count + combatDeltas.Count);
            tickDeltas.AddRange(moves);
            tickDeltas.AddRange(combatDeltas);
            await FanOutBatchAsync(tickDeltas);
            foreach (var evt in playerEvents)
                await SendPlayerEventAsync(evt.SessionId, evt.EventName, evt.Vitals);
        }

        /// <summary>
        /// Applies the active <see cref="DeathPolicy"/>'s outcome to a player reduced to 0 HP by a
        /// monster's attack (engine gap-analysis §4.11, Phase 2 — see wire-death-respawn-live):
        /// instant respawn, entering <see cref="Downed"/>, or (permadeath, no down state) an
        /// instant <see cref="Corpse"/> transition. Mutates world state synchronously and appends
        /// to the caller's delta/event lists — <see cref="StepNpcsAsync"/> and
        /// <see cref="TickDownedAndInvulnerablePlayersAsync"/> both fan them out after their
        /// synchronous sweep, the same pattern every other outcome they collect already uses.
        /// </summary>
        private void ResolvePlayerLethalHit(Character player, List<EntityMovedDelta> moves, List<MapDelta> combatDeltas,
            List<(string SessionId, string EventName, Aetherium.Model.PlayerVitalsDto Vitals)> playerEvents)
        {
            switch (PlayerDeathResolver.ResolveLethalHitOutcome(_deathPolicy))
            {
                case PlayerDeathOutcome.InstantRespawn:
                    RespawnPlayer(player, moves, combatDeltas);
                    playerEvents.Add((player.EntityId, "ReceiveRespawn", BuildVitalsDto(player)));
                    break;

                case PlayerDeathOutcome.InstantPermadeath:
                    player.Set(new Corpse());
                    combatDeltas.Add(IntFieldDelta(player.EntityId, "Health", "Level", 0));
                    playerEvents.Add((player.EntityId, "ReceiveDied", BuildVitalsDto(player)));
                    break;

                case PlayerDeathOutcome.EnterDowned:
                default:
                    player.Set(new Downed(_deathPolicy.ReviveWindowTicks));
                    combatDeltas.Add(IntFieldDelta(player.EntityId, "Health", "Level", 0));
                    playerEvents.Add((player.EntityId, "ReceiveDowned", BuildVitalsDto(player)));
                    break;
            }
        }

        /// <summary>
        /// Each world tick, advances every player's <see cref="Downed"/> countdown and
        /// <see cref="RespawnInvulnerable"/> window (engine gap-analysis §4.11, Phase 2 — see
        /// wire-death-respawn-live). A Downed countdown reaching zero resolves per the active
        /// <see cref="DeathPolicy"/>: respawn, or (permadeath) become a <see cref="Corpse"/>. The
        /// invulnerability countdown is silent (no delta — it's a defensive flag, not visible
        /// state); a Downed expiry emits the same delta shapes <see cref="ResolvePlayerLethalHit"/>
        /// does, plus the matching player-scoped event.
        /// </summary>
        private async Task TickDownedAndInvulnerablePlayersAsync()
        {
            if (_world is null) return;

            var downedPlayers = _world.Characters.Values.Where(c => c.Has<Downed>()).ToList();
            var invulnerablePlayers = _world.Characters.Values.Where(c => c.Has<RespawnInvulnerable>()).ToList();
            if (downedPlayers.Count == 0 && invulnerablePlayers.Count == 0)
                return;

            var moves = new List<EntityMovedDelta>();
            var combatDeltas = new List<MapDelta>();
            var playerEvents = new List<(string SessionId, string EventName, Aetherium.Model.PlayerVitalsDto Vitals)>();

            foreach (var player in invulnerablePlayers)
            {
                var invulnerable = player.Get<RespawnInvulnerable>();
                invulnerable.TicksRemaining--;
                if (invulnerable.TicksRemaining <= 0)
                    player.Clear<RespawnInvulnerable>();
            }

            foreach (var player in downedPlayers)
            {
                var downed = player.Get<Downed>();
                downed.TicksRemaining--;
                if (downed.TicksRemaining > 0)
                    continue;

                if (PlayerDeathResolver.ResolveDownedOutcome(_deathPolicy) == DownedExpiryOutcome.Respawn)
                {
                    RespawnPlayer(player, moves, combatDeltas);
                    playerEvents.Add((player.EntityId, "ReceiveRespawn", BuildVitalsDto(player)));
                }
                else
                {
                    player.Clear<Downed>();
                    player.Set(new Corpse());
                    playerEvents.Add((player.EntityId, "ReceiveDied", BuildVitalsDto(player)));
                }
            }

            foreach (var move in moves)
                await FanOutAsync(move);
            foreach (var delta in combatDeltas)
                await FanOutAsync(delta);
            foreach (var evt in playerEvents)
                await SendPlayerEventAsync(evt.SessionId, evt.EventName, evt.Vitals);
        }

        /// <summary>
        /// Resets a player back to playable state in place: full Health, teleported per the
        /// active <see cref="DeathPolicy"/>'s <c>RespawnLocation</c>, <see cref="Downed"/> cleared,
        /// a fresh <see cref="RespawnInvulnerable"/> window applied. Reuses the player's existing
        /// entity id (== SessionId — see wire-death-respawn-live design.md D3) rather than
        /// allocating a new one, since the session has no channel to learn about an entity-id
        /// change; appends the resulting deltas to the caller's lists rather than fanning them out
        /// itself, so both call sites (a lethal hit's instant-respawn branch, and a Downed
        /// countdown's expiry) share one synchronous, awaitless mutation.
        /// </summary>
        private void RespawnPlayer(Character player, List<EntityMovedDelta> moves, List<MapDelta> combatDeltas)
        {
            player.Clear<Downed>();

            if (player.Has<Aetherium.Components.Health>())
            {
                var health = player.Get<Aetherium.Components.Health>();
                health.Level = health.MaxLevel;
                combatDeltas.Add(IntFieldDelta(player.EntityId, "Health", "Level", health.MaxLevel));
            }

            var oldLoc = player.Get<Aetherium.Components.WorldLocation>();
            var newLoc = ResolveRespawnLocation(player) ?? oldLoc;
            if (newLoc is not null)
            {
                // Reconcile spawn bookkeeping so a later LeavePlayerAsync frees the cell the
                // player actually ends up standing on, not a stale pre-respawn location.
                if (_playerSpawns.Remove(player.EntityId, out var previousSpawn))
                    _spawnsInUse.Remove(previousSpawn);
                _spawnsInUse.Add(newLoc);
                _playerSpawns[player.EntityId] = newLoc;

                if (oldLoc is null || newLoc != oldLoc)
                {
                    player.Set(new Aetherium.Components.WorldLocation(newLoc.X, newLoc.Y, newLoc.Z));
                    if (oldLoc is not null)
                    {
                        moves.Add(new EntityMovedDelta
                        {
                            EntityId = player.EntityId,
                            OldX = oldLoc.X, OldY = oldLoc.Y, OldZ = oldLoc.Z,
                            NewX = newLoc.X, NewY = newLoc.Y, NewZ = newLoc.Z,
                        });
                    }
                }
            }

            if (_deathPolicy.RespawnInvulnerabilityTicks > 0)
                player.Set(new RespawnInvulnerable(_deathPolicy.RespawnInvulnerabilityTicks));
            else
                player.Clear<RespawnInvulnerable>();
        }

        /// <summary>
        /// Resolves a respawn destination per the active <see cref="DeathPolicy"/>'s
        /// <c>RespawnLocation</c> mode (engine gap-analysis §4.11 — see wire-death-respawn-live).
        /// <c>DeathLocation</c>/<c>EntryLocation</c>/<c>FixedCoordinates</c>/
        /// <c>OffsetFromCoordinates</c> resolve directly; every other mode
        /// (<c>NamedLocation</c>/<c>OffsetFromNamedLocation</c>/<c>LastSafeLocation</c>/
        /// <c>PartyLeader</c>, and <c>WorldSpawn</c> itself) falls back to re-running the map's
        /// normal spawn selection — the first four need a location-tag registry or party system
        /// that doesn't exist yet (see tasks.md L.2/L.4). Always returns a freshly-constructed
        /// <see cref="Aetherium.Components.WorldLocation"/> (never a live component reference), so
        /// it's always safe for the caller to store or mutate independently.
        /// </summary>
        private Aetherium.Components.WorldLocation? ResolveRespawnLocation(Character player)
        {
            var locationPolicy = _deathPolicy.RespawnLocation ?? DeathPolicy.Default.RespawnLocation;
            var current = player.Get<Aetherium.Components.WorldLocation>();

            Aetherium.Components.WorldLocation? candidate = locationPolicy.Mode switch
            {
                RespawnLocationMode.DeathLocation => current,
                RespawnLocationMode.EntryLocation => _playerSpawns.TryGetValue(player.EntityId, out var entry) ? entry : null,
                RespawnLocationMode.FixedCoordinates => new Aetherium.Components.WorldLocation(locationPolicy.X, locationPolicy.Y, locationPolicy.Z),
                RespawnLocationMode.OffsetFromCoordinates => new Aetherium.Components.WorldLocation(
                    locationPolicy.X + locationPolicy.OffsetX, locationPolicy.Y + locationPolicy.OffsetY, locationPolicy.Z + locationPolicy.OffsetZ),
                _ => null,
            };

            // A candidate the player already occupies (DeathLocation, or EntryLocation/coordinates
            // that happen to match) must be accepted without an occupancy check — IsOpenForOccupancy
            // would otherwise reject it solely because the player's own Character entity is there.
            if (candidate is not null && candidate != current && _world is not null && !_world.IsOpenForOccupancy(candidate))
                candidate = null;

            candidate ??= SelectUnusedSpawn();

            return candidate is null ? null : new Aetherium.Components.WorldLocation(candidate.X, candidate.Y, candidate.Z);
        }

        /// <summary>Builds the client-facing snapshot of a player's life-state (engine gap-analysis
        /// §4.11 — see wire-death-respawn-live), sent via <see cref="SendPlayerEventAsync"/>.</summary>
        private static Aetherium.Model.PlayerVitalsDto BuildVitalsDto(Character player)
        {
            var health = player.Has<Aetherium.Components.Health>() ? player.Get<Aetherium.Components.Health>() : null;
            var downed = player.Has<Downed>() ? player.Get<Downed>() : null;
            return new Aetherium.Model.PlayerVitalsDto
            {
                Health = health?.Level ?? 0,
                MaxHealth = health?.MaxLevel ?? 0,
                IsDowned = downed is not null,
                DownedTicksRemaining = downed?.TicksRemaining ?? 0,
                IsInvulnerable = player.Has<RespawnInvulnerable>(),
            };
        }

        /// <summary>
        /// Partitions the map into regions (64×64 chunks) and initializes region grains.
        /// </summary>
        private async Task PartitionIntoRegionsAsync()
        {
            if (_mapState.State == null || _world == null)
                return;

            var mapId = _mapState.State.MapId;
            var size = _mapState.State.Size;
            
            _regions = new Dictionary<string, IMapRegionGrain>();

            // Calculate number of regions in each dimension
            var regionsX = (int)Math.Ceiling((double)size.Width / _regionSize);
            var regionsY = (int)Math.Ceiling((double)size.Height / _regionSize);

            // Initialize region grains for each Z level
            for (int z = 0; z < size.Depth; z++)
            {
                for (int regionY = 0; regionY < regionsY; regionY++)
                {
                    for (int regionX = 0; regionX < regionsX; regionX++)
                    {
                        var regionKey = GetRegionKey(mapId, regionX, regionY, z);
                        var regionGrain = _grainFactory.GetGrain<IMapRegionGrain>(regionKey);
                        
                        await regionGrain.InitializeAsync(mapId, regionX, regionY, z, _regionSize);
                        
                        _regions[regionKey] = regionGrain;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the region key for a given map, region coordinates, and Z level.
        /// </summary>
        private static string GetRegionKey(string mapId, int regionX, int regionY, int zLevel)
        {
            return $"{mapId}:region:{regionX},{regionY},{zLevel}";
        }

        /// <summary>
        /// Finds portals in the generated world and registers them with the cluster.
        /// </summary>
        private async Task RegisterPortalsWithClusterAsync(IClusterGrain clusterGrain, string worldId, string mapId)
        {
            if (_world == null)
                return;

            // Find all entities with PortalComponent
            foreach (var entity in _world.Entities.Values)
            {
                if (!entity.Has<PortalComponent>())
                    continue;

                var portalComponent = entity.Get<PortalComponent>();
                if (portalComponent == null)
                    continue;

                // Create portal link for registration
                var portalLink = new PortalLink
                {
                    PortalId = portalComponent.PortalId,
                    SourceWorldId = worldId,
                    SourceMapId = mapId,
                    TargetWorldId = portalComponent.TargetWorldId,
                    TargetMapId = portalComponent.TargetMapId,
                    TargetTag = portalComponent.TargetTag,
                    IsResolved = portalComponent.TargetWorldId != null && portalComponent.TargetMapId != null
                };

                await clusterGrain.RegisterPortalAsync(portalLink);
            }
        }

        /// <summary>
        /// Gets the region key for a given world location.
        /// </summary>
        private string GetRegionKeyForLocation(int x, int y, int z)
        {
            if (_mapState.State == null)
                throw new InvalidOperationException("Map not initialized");

            var mapId = _mapState.State.MapId;
            var regionX = x / _regionSize;
            var regionY = y / _regionSize;
            return GetRegionKey(mapId, regionX, regionY, z);
        }

        /// <summary>
        /// Gets the region grain for a given world location.
        /// </summary>
        public IMapRegionGrain? GetRegionForLocation(int x, int y, int z)
        {
            if (_regions == null)
                return null;

            var regionKey = GetRegionKeyForLocation(x, y, z);
            return _regions.TryGetValue(regionKey, out var region) ? region : null;
        }

        /// <summary>
        /// Captures the grain's live <see cref="World"/> into a <see cref="Aetherium.Server.Persistence.RegionStateSnapshot"/>,
        /// persists it via <see cref="Aetherium.Server.Persistence.IWorldSnapshotStore.SaveSnapshotAsync"/>, and compacts the
        /// map's delta log up through the captured sequence. Bounded recovery: on the
        /// next activation, only deltas with <c>Sequence &gt; LastSequence</c> need to be
        /// replayed atop the snapshot.
        /// </summary>
        public async Task<long> ForceSnapshotAsync()
        {
            var store = GetSnapshotStore();
            if (store is null || _world is null || _mapState.State is null || _mapState.State.Recipe is null)
                return 0;

            // Capture the highest sequence that has been emitted. Increment is the
            // sequence number ALREADY assigned to the most recent delta; new deltas
            // get _nextSequence + 1.
            var capturedSequence = System.Threading.Interlocked.Read(ref _nextSequence);

            var worldSnapshot = WorldSnapshotBuilder.SnapshotOf(
                _world,
                _mapState.State.Recipe,
                _mapState.State.WorldId,
                _mapState.State.MapId,
                _mapState.State.Size);

            // Serialize the full WorldSnapshot (recipe + entities) so the persisted snapshot
            // is self-contained — recovery does not require MapState's recipe to also be
            // durable. Trades a few extra bytes for operational simplicity (a backup is one row).
            var serializer = ServiceProvider.GetService(typeof(Orleans.Serialization.Serializer))
                as Orleans.Serialization.Serializer;
            byte[]? entityBytes = null;
            if (serializer is not null)
                entityBytes = serializer.SerializeToArray(worldSnapshot);

            // Capture grain-authoritative heat trails so they survive a cold start (P3-8).
            var heatTrails = _heatTracker.ExportTrails(CurrentHeatTime())
                .Select(t => new Aetherium.Server.Persistence.PersistedHeatTrail
                {
                    X = t.Location.X,
                    Y = t.Location.Y,
                    Z = t.Location.Z,
                    EntityId = t.EntityId,
                    Timestamp = t.Timestamp,
                    BaseIntensity = t.BaseIntensity,
                    Duration = t.Duration,
                })
                .ToList();

            var regionSnapshot = new Aetherium.Server.Persistence.RegionStateSnapshot
            {
                RegionId = _mapState.State.MapId,
                MapId = _mapState.State.MapId,
                RegionSize = _regionSize,
                SavedAt = DateTime.UtcNow,
                SerializedEntities = entityBytes,
                LastSequence = capturedSequence,
                HeatTrails = heatTrails,
            };

            await store.SaveSnapshotAsync(_mapState.State.WorldId, regionSnapshot);
            await store.CompactMapDeltasAsync(_mapState.State.WorldId, _mapState.State.MapId, capturedSequence);
            return capturedSequence;
        }

        /// <summary>
        /// Reports persistence health for this map (P3-8): whether the append-only delta log is
        /// keeping up, the cumulative append-failure count, and the last error. Lets operators and
        /// tests observe delta-persistence failures instead of them being silently swallowed.
        /// </summary>
        public Task<Aetherium.Model.PersistenceHealthDto> GetPersistenceHealthAsync()
        {
            return Task.FromResult(new Aetherium.Model.PersistenceHealthDto
            {
                Healthy = System.Threading.Interlocked.CompareExchange(ref _persistenceDirty, 0, 0) == 0,
                DeltaAppendFailureCount = System.Threading.Interlocked.Read(ref _persistDeltaFailureCount),
                LastError = _lastPersistError,
                LastFailureAtUtc = _lastPersistFailureAtUtc,
            });
        }

        public async Task SaveMapAsync()
        {
            if (_regions == null || _mapState.State == null)
                return;

            // Save all regions
            var saveTasks = _regions.Values
                .Select(async region =>
                {
                    var snapshot = await region.GetSnapshotAsync();
                    // Regions persist automatically via Orleans persistent state
                    // This ensures all regions are persisted
                })
                .ToList();

            await Task.WhenAll(saveTasks);

            // Save map metadata
            await _mapState.WriteStateAsync();
        }

        public async Task<bool> LoadMapAsync()
        {
            if (_mapState.State == null)
                return false;

            // Regions are loaded automatically on activation via Orleans persistent state
            // We just need to rebuild the region cache
            await PartitionIntoRegionsAsync();

            return true;
        }

        public async Task<SpawnEntityResult> SpawnEntityAsync(SpawnEntityRequest request)
        {
            if (_world == null)
                return new SpawnEntityResult { Success = false, ErrorMessage = "World not initialized for this map" };

            try
            {
                var location = new WorldLocation(request.X, request.Y, request.Z);

                var flies = request.Flies || IsFlyingCreatureType(request.CreatureType);

                // Validate placement. Flyers spawn airborne and are validated against altitude bands and
                // per-band obstruction rather than ground passability, so they may occupy open air.
                if (flies)
                {
                    if (location.Z < request.MinBand || location.Z > request.MaxBand)
                    {
                        return new SpawnEntityResult { Success = false, ErrorMessage = "Spawn band is outside the flyer's range" };
                    }
                    if (_world.ColumnObstructsMovement(location.X, location.Y, location.Z))
                    {
                        return new SpawnEntityResult { Success = false, ErrorMessage = "Air location is obstructed" };
                    }
                }
                else if (!_world.PassableTerrain(location))
                {
                    return new SpawnEntityResult { Success = false, ErrorMessage = "Location is not passable" };
                }

                // Check if location is already occupied
                if (_world.EntitiesByLocation.TryGetValue(location, out var entitiesAtLoc))
                {
                    foreach (var existingEntity in entitiesAtLoc.Values)
                    {
                        if (existingEntity is Character)
                        {
                            return new SpawnEntityResult { Success = false, ErrorMessage = "Location is already occupied" };
                        }
                    }
                }

                // Ensure required tile type exists
                if (!_world.TileTypes.ContainsKey("Monster"))
                {
                    _world.TileTypes["Monster"] = new TileType
                    {
                        Name = "Monster",
                        Settings = new Dictionary<string, string>
                        {
                            { "MapCharacter", "M" },
                            { "BackgroundColor", System.ConsoleColor.Black.ToString() },
                            { "ForegroundColor", System.ConsoleColor.DarkRed.ToString() }
                        }
                    };
                }

                // Create the entity based on creature type. The world's content catalog wins
                // (add-content-definitions): a defined creature id materializes its definition;
                // anything else falls back to the legacy hardcoded switch.
                var creatureType = request.CreatureType.ToLowerInvariant();
                Character? entity;
                if (_contentCatalog is not null
                    && _contentCatalog.CreaturesById.TryGetValue(creatureType, out var creatureDefinition))
                {
                    var defined = new Aetherium.Monster(_world);
                    Aetherium.Server.Content.ContentCompiler.ApplyCreature(defined, creatureDefinition, _world);
                    entity = defined;
                }
                else
                {
                    entity = creatureType switch
                    {
                        "monster" => new Aetherium.Monster(_world),
                        "wolf" => new Aetherium.Monster(_world),
                        "bear" => new Aetherium.Monster(_world),
                        "bandit" => new Aetherium.Monster(_world),
                        "snake" => new Snake(),
                        "zombie" => new Zombie(_world),
                        _ => new Aetherium.Monster(_world)
                    };
                }

                if (entity == null)
                {
                    return new SpawnEntityResult { Success = false, ErrorMessage = "Could not create entity" };
                }

                // Preserve the request's creature-type string on the entity — several types map
                // onto the same C# class above, and downstream consumers (first: the faction
                // standing loop's kill:<creature-type> tags, wire-factions-live) need "wolf" vs
                // "bandit" to survive past this switch. (ApplyCreature already stamped the tag on
                // the defined path; Set replaces, so this is idempotent there.)
                entity.Set(new Aetherium.Components.CreatureTypeTag(creatureType));

                // Set location and add to world
                entity.Set(location);

                if (flies)
                {
                    entity.Set(new Flight
                    {
                        State = FlightState.Airborne,
                        MinBand = request.MinBand,
                        MaxBand = request.MaxBand,
                        CruiseBand = location.Z,
                        CanLand = request.CanLand
                    });

                    // Attach the stock interaction profile for this flyer kind (hackable satellite, summonable
                    // taxi, attackable drone, …) so it can be hacked/summoned/attacked/inspected.
                    var profile = Aetherium.Server.Flying.FlyerProfiles.ForCreatureType(request.CreatureType);
                    if (profile != null)
                        entity.Set(profile);
                }

                _world.AddEntity(entity);

                return new SpawnEntityResult { Success = true, EntityId = entity.EntityId };
            }
            catch (Exception ex)
            {
                return new SpawnEntityResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Whether a creature-type name denotes a flyer that should spawn airborne with a Flight component.
        /// </summary>
        public static bool IsFlyingCreatureType(string creatureType)
        {
            switch ((creatureType ?? string.Empty).ToLowerInvariant())
            {
                case "bird":
                case "drone":
                case "satellite":
                case "aircraft":
                case "airplane":
                case "helicopter":
                case "airship":
                case "dropship":
                case "spaceship":
                    return true;
                default:
                    return false;
            }
        }

        public async Task<BuildStructureResult> BuildStructureAsync(BuildStructureRequest request)
        {
            if (_world == null)
                return new BuildStructureResult { Success = false, ErrorMessage = "World not initialized for this map" };

            try
            {
                // This is a placeholder - full implementation would require access to PrefabLibrary
                // For now, return success but log that it's not fully implemented
                Console.WriteLine($"[GameMapGrain] BuildStructureAsync called for {request.PrefabId} at ({request.X}, {request.Y}, {request.Z}), but requires PrefabLibrary integration");
                return new BuildStructureResult { Success = false, ErrorMessage = "BuildStructureAsync not fully implemented - requires PrefabLibrary" };
            }
            catch (Exception ex)
            {
                return new BuildStructureResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        // ==================================================================
        // Phase 2c — grain mutation methods.
        // Each method mutates _world directly and emits a MapDelta to the map's
        // SignalR group for fan-out to all sessions.
        // ==================================================================

        /// <summary>
        /// Looks up the player Character entity for a given session id (which is
        /// the player's entity id in our model). Returns null with reason if not
        /// found — caller maps to an appropriate failure result.
        /// </summary>
        private Character? GetPlayerCharacter(string sessionId)
        {
            if (_world is null) return null;
            return _world.Entities.TryGetValue(sessionId, out var entity)
                ? entity as Character
                : null;
        }

        /// <summary>True when a player entity can act — not <see cref="Downed"/>, not a
        /// <see cref="Corpse"/> (engine gap-analysis §4.11, Phase 2 — see
        /// wire-death-respawn-live). Checked at the top of every player command; a downed or
        /// permadead player is frozen until it respawns.</summary>
        private static bool IsActionable(Character player) => !player.Has<Downed>() && !player.Has<Corpse>();

        private const string DownedFailureReason = "You are downed and cannot act.";

        /// <summary>
        /// Routes a MapDelta through the host-side session manager. The manager
        /// applies the delta to every session bound to this map, then pushes a
        /// fresh perception update over SignalR. Clients only ever see the
        /// resulting PerceptionDto — never raw deltas — so cells outside their
        /// FOV never reach the wire (perception-pure principle).
        /// </summary>
        private async Task FanOutAsync(MapDelta delta)
        {
            if (_mapState.State is null) return;
            delta.MapId = _mapState.State.MapId;
            delta.Sequence = System.Threading.Interlocked.Increment(ref _nextSequence);

            // Persist before fan-out: a session that observes a delta must never see one
            // that was lost on a subsequent restart. Append failures log + proceed so the
            // live game does not stall on transient persistence errors.
            await PersistDeltaAsync(delta);

            var mgr = GetSessionManager();
            if (mgr is null) return; // not wired (TestingHost path) — no consumers, no problem

            try
            {
                await mgr.NotifyMapMutationAsync(_mapState.State.MapId, delta);
            }
            catch (Exception ex)
            {
                // Mutation has already happened; a host-side failure must not roll it back.
                Console.WriteLine($"[GameMapGrain] FanOut failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Fans out a whole tick's worth of deltas as one batch: sequences and persists each, then
        /// hands the batch to the session manager, which applies them all under a single per-session
        /// lock and schedules ONE perception flush. The per-delta <see cref="FanOutAsync"/> path took
        /// the session lock once per delta — so a 20-monster tick took the lock 20 times, each
        /// contending with the perception flush, stretching the tick to seconds and starving player
        /// commands on this single-threaded grain. Used by <see cref="StepNpcsAsync"/>.
        /// </summary>
        private async Task FanOutBatchAsync(IReadOnlyList<MapDelta> deltas)
        {
            if (_mapState.State is null || deltas is null || deltas.Count == 0) return;

            foreach (var delta in deltas)
            {
                delta.MapId = _mapState.State.MapId;
                delta.Sequence = System.Threading.Interlocked.Increment(ref _nextSequence);
                await PersistDeltaAsync(delta);
            }

            var mgr = GetSessionManager();
            if (mgr is null) return; // not wired (TestingHost path) — no consumers, no problem

            try
            {
                await mgr.NotifyMapMutationsAsync(_mapState.State.MapId, deltas);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameMapGrain] Batch FanOut failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies an actor-only delta — used for state changes that observers
        /// don't perceive (e.g. heading changes). Goes only to the originating
        /// session's local mirror via the session manager's targeted path.
        /// </summary>
        private async Task SendToActorAsync(string sessionId, MapDelta delta)
        {
            if (_mapState.State is null) return;
            delta.MapId = _mapState.State.MapId;
            delta.Sequence = System.Threading.Interlocked.Increment(ref _nextSequence);

            // Persist before send: same durability contract as FanOutAsync.
            await PersistDeltaAsync(delta);

            var mgr = GetSessionManager();
            if (mgr is null) return;

            try
            {
                await mgr.NotifySessionMutationAsync(sessionId, delta);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameMapGrain] SendToActor failed for {sessionId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a player-lifecycle signal (engine gap-analysis §4.11, Phase 2 — see
        /// wire-death-respawn-live) — "ReceiveDowned"/"ReceiveRespawn"/"ReceiveDied" — directly to
        /// one session's client. Not a <see cref="MapDelta"/>: this describes what's happening to
        /// the player, not a change to world state, so it bypasses persistence/mirror
        /// reconciliation entirely (unlike <see cref="FanOutAsync"/>/<see cref="SendToActorAsync"/>).
        /// </summary>
        private async Task SendPlayerEventAsync(string sessionId, string methodName, Aetherium.Model.PlayerVitalsDto vitals)
        {
            var mgr = GetSessionManager();
            if (mgr is null) return;

            try
            {
                await mgr.NotifyPlayerEventAsync(sessionId, methodName, vitals);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameMapGrain] Player event '{methodName}' failed for {sessionId}: {ex.Message}");
            }
        }

        public async Task<MoveResult> MoveAsync(string sessionId, Aetherium.Model.RelativeDirection direction, int distance)
        {
            if (_world is null || _mapState.State is null) return MoveResult.Fail("Map not initialized");
            var player = GetPlayerCharacter(sessionId);
            if (player is null) return MoveResult.Fail("Player not on map");
            if (!IsActionable(player)) return MoveResult.Fail(DownedFailureReason);

            var current = player.Get<Aetherium.Components.WorldLocation>();
            if (current is null) return MoveResult.Fail("Player has no location");

            // Enforce the same distance bounds MoveTool advertises (1–100), so a
            // caller reaching the grain directly can't request an arbitrary jump.
            if (distance < 1 || distance > 100)
                return MoveResult.Fail("Distance must be between 1 and 100");

            // Relative direction resolves against the player's heading per cell, in
            // the world's topology (docs/grid-topologies.md Rule 2) — on square,
            // byte-identical to the old cardinalize-then-rotate pair.
            var heading = player.Get<Aetherium.Components.HasHeading>()?.Heading ?? 0;

            // Preserve old position for the delta before movement mutates the entity.
            var oldX = current.X; var oldY = current.Y; var oldZ = current.Z;

            // Validated, per-step movement: stops at the first wall/closed door/
            // occupied cell/map edge, so this path can no longer walk through
            // geometry (it previously applied the full delta unchecked).
            var outcome = _world.TryMoveSteps(player, heading, direction, distance);
            if (!outcome.Success)
                return MoveResult.Fail(outcome.BlockedReason ?? "Blocked");

            var final = outcome.FinalLocation!;
            await FanOutAsync(new EntityMovedDelta
            {
                EntityId = player.EntityId,
                OldX = oldX, OldY = oldY, OldZ = oldZ,
                NewX = final.X, NewY = final.Y, NewZ = final.Z,
            });

            return MoveResult.Ok();
        }

        public async Task<RotateResult> RotateAsync(string sessionId, int degrees)
        {
            if (_world is null) return RotateResult.Fail("Map not initialized");
            var player = GetPlayerCharacter(sessionId);
            if (player is null) return RotateResult.Fail("Player not on map");
            if (!IsActionable(player)) return RotateResult.Fail(DownedFailureReason);

            var heading = player.Get<Aetherium.Components.HasHeading>();
            if (heading is null) return RotateResult.Fail("Player has no heading component");

            heading.Heading += degrees;

            // Heading is private information per the perception-pure design — send
            // the delta only to the actor's own session. Other players' clients
            // never see another character's facing direction (until a future change
            // adds compass-style perception filters).
            await SendToActorAsync(sessionId, new EntityHeadingChangedDelta
            {
                EntityId = player.EntityId,
                Degrees = heading.Heading,
            });

            return RotateResult.Ok(heading.Heading);
        }

        public async Task<ChangeLevelResult> ChangeLevelAsync(string sessionId, int deltaZ)
        {
            if (_world is null) return ChangeLevelResult.Fail("Map not initialized");
            var player = GetPlayerCharacter(sessionId);
            if (player is null) return ChangeLevelResult.Fail("Player not on map");
            if (!IsActionable(player)) return ChangeLevelResult.Fail(DownedFailureReason);

            var current = player.Get<Aetherium.Components.WorldLocation>();
            if (current is null) return ChangeLevelResult.Fail("Player has no location");

            var oldX = current.X; var oldY = current.Y; var oldZ = current.Z;

            // Validated level change: requires standing on a stair cell and a
            // passable landing (it previously teleported the player to any Z).
            var outcome = _world.TryChangeLevel(player, deltaZ);
            if (!outcome.Success)
                return ChangeLevelResult.Fail(outcome.BlockedReason ?? "Blocked");

            var final = outcome.FinalLocation!;
            await FanOutAsync(new EntityMovedDelta
            {
                EntityId = player.EntityId,
                OldX = oldX, OldY = oldY, OldZ = oldZ,
                NewX = final.X, NewY = final.Y, NewZ = final.Z,
            });

            return ChangeLevelResult.Ok(final.Z);
        }

        // ------------------------------------------------------------------
        // Pickup / Drop / Open / Close — delegate to InteractionSystem's
        // ActionContext overloads (refactor-interaction-system-stateless).
        // The grain captures the post-condition context needed to emit the
        // appropriate delta after a successful interaction. The mutation
        // logic itself lives in InteractionSystem, shared with the legacy
        // session-based code path used by LocalMutationGateway.
        // ------------------------------------------------------------------

        private readonly InteractionSystem _interactionSystem = new InteractionSystem();
        private readonly CombatSystem _combatSystem = new CombatSystem();

        // Deep combat model (engine gap-analysis §4.2/§4.11), wired for the player-attacks-monster
        // path only (AttackAsync) — see wire-combat-pipeline-live. AlwaysHitResolver reproduces the
        // pre-pipeline always-hits MVP exactly. Monster-attacks-player retaliation in StepNpcsAsync
        // deliberately stays on the old CombatSystem — a downed player entering the Dying/Corpse
        // creature-death lifecycle needs its own design pass, see wire-death-respawn-live.
        private readonly DamagePipeline _damagePipeline = new DamagePipeline();
        private readonly IHitResolver _hitResolver = new AlwaysHitResolver();

        // Per-world death/respawn rules (engine gap-analysis §4.11 — see wire-death-respawn-live),
        // sourced from InitializeAsync's deathPolicy argument (persisted on MapState so it survives
        // reactivation) rather than hardcoded, so different worlds can pick different death models
        // as data. Defaults to DeathPolicy.Default until InitializeAsync/OnActivateAsync sets it.
        private DeathPolicy _deathPolicy = DeathPolicy.Default;
        private readonly DeathSystem _deathSystem = new DeathSystem();
        private readonly CorpseExpirySystem _corpseExpirySystem = new CorpseExpirySystem();

        // Per-world ability content (engine gap-analysis §4.3 — see wire-abilities-live), compiled from
        // the world's AbilityConfig. The catalog is the runtime (compiled) form of the config's
        // AbilityDefinitions; _characterResourcePools are the pool definitions stamped onto each joining
        // character. Both default to empty (no abilities) until InitializeAsync/OnActivateAsync applies a
        // config — the engine ships no abilities, they are entirely per-world data.
        private AbilityCatalog _abilityCatalog = new AbilityCatalog();
        private IReadOnlyList<ResourcePoolDefinition> _characterResourcePools = Array.Empty<ResourcePoolDefinition>();

        /// <summary>Flat action-point cost of any ability cast this slice (mirrors <c>NpcActionCost</c>).
        /// Per-ability AP cost co-designs with phased charge/cast/recover timing — a later slice.</summary>
        private const double AbilityActionCost = 1.0;

        private AbilityCompiler CreateAbilityCompiler() => new AbilityCompiler(_damagePipeline, _hitResolver);

        /// <summary>Compiles a world's <see cref="AbilityConfig"/> into this map's runtime catalog and
        /// remembers its character resource-pool definitions. Called from InitializeAsync (fresh) and
        /// OnActivateAsync (rehydrate); null yields an empty catalog and no pools.</summary>
        private void ApplyAbilityConfig(AbilityConfig? config)
        {
            _abilityCatalog = CreateAbilityCompiler().CompileCatalog(config?.Abilities);
            _characterResourcePools = config?.CharacterResourcePools is { } pools
                ? pools
                : Array.Empty<ResourcePoolDefinition>();
        }

        // Per-world progression content (engine gap-analysis §4.4 — see wire-progression-live),
        // compiled from the world's ProgressionConfig. _skillCatalog + _levelCurvesByPool are the
        // runtime (compiled) forms; the raw config drives XP-award rules, attribute derivations, and
        // the optional skill-gated-casting flag. All default to empty (no progression) until a config
        // is applied — the engine ships no progression content, it is entirely per-world data.
        private ProgressionConfig? _progressionConfig;
        private SkillCatalog _skillCatalog = new SkillCatalog();
        private IReadOnlyDictionary<string, ILevelCurve> _levelCurvesByPool = new Dictionary<string, ILevelCurve>();
        private readonly SkillUnlockService _skillUnlockService = new SkillUnlockService();

        /// <summary>Compiles a world's <see cref="ProgressionConfig"/> into this map's runtime skill
        /// catalog + per-pool level curves and remembers the config (for award rules / derivations /
        /// the cast-gate flag). Called from InitializeAsync (fresh) and OnActivateAsync (rehydrate);
        /// null yields an empty catalog and no progression behavior.</summary>
        private void ApplyProgressionConfig(ProgressionConfig? config)
        {
            _progressionConfig = config;
            var compiler = new ProgressionCompiler();
            _skillCatalog = compiler.CompileSkillCatalog(config?.Skills);
            _levelCurvesByPool = compiler.CompileCurvesByPool(config?.Pools);
        }

        // Per-world faction content (engine gap-analysis §4.6 — see wire-factions-live and
        // docs/factions-reputation.md), compiled from the world's FactionConfig. _factionRegistry /
        // _factionRelations are the runtime (compiled) forms; the raw config supplies rank rules,
        // standing bands, and starting standings. All default to empty (no factions) until a config
        // is applied — the engine ships no factions, they are entirely per-world data.
        private FactionConfig? _factionConfig;
        private FactionRegistry _factionRegistry = new FactionRegistry();
        private FactionRelations _factionRelations = new FactionRelations();

        /// <summary>Compiles a world's <see cref="FactionConfig"/> into this map's runtime faction
        /// registry + relations and remembers the config (rank rules / bands / starting standings).
        /// Called from InitializeAsync (fresh) and OnActivateAsync (rehydrate); null yields an empty
        /// registry and no faction behavior.</summary>
        private void ApplyFactionConfig(FactionConfig? config)
        {
            _factionConfig = config;
            var compiler = new FactionCompiler();
            _factionRegistry = compiler.CompileRegistry(config?.Factions);
            _factionRelations = compiler.CompileRelations(config?.Relations);
        }

        // Per-world content vocabulary (add-content-definitions), compiled from the world's
        // ContentConfig. Null means legacy hardcoded content (generic Monster, SwordItem loot) —
        // the engine ships no bestiary, creatures and items are entirely per-world data.
        private Aetherium.Server.Content.ContentCatalog? _contentCatalog;

        /// <summary>Compiles a world's <see cref="Aetherium.Model.Content.ContentConfig"/> into this
        /// map's runtime content catalog. Called from InitializeAsync (fresh) and OnActivateAsync
        /// (rehydrate); null leaves the catalog absent → legacy content everywhere.</summary>
        private void ApplyContentConfig(Aetherium.Model.Content.ContentConfig? config)
        {
            _contentCatalog = config is null ? null : Aetherium.Server.Content.ContentCompiler.Compile(config);
        }

        /// <summary>
        /// Re-materializes pass-placed monsters from the content catalog's spawn table
        /// (add-content-definitions): the population passes decide <em>where and how many</em>,
        /// this decides <em>what</em>. Entities already carrying a <see cref="Aetherium.Components.CreatureTypeTag"/>
        /// that resolves in the catalog (snapshot re-hydration) are re-skinned in place with their
        /// current damage preserved; untagged monsters (fresh generation or recipe regen) get a
        /// weighted draw seeded from <paramref name="seed"/> in deterministic location order, so a
        /// given (seed, table) always yields the same creature mix. No-op without a spawn table.
        /// </summary>
        private void ApplyContentPopulation(World world, int seed)
        {
            if (_contentCatalog is null || !_contentCatalog.HasSpawnTable)
                return;

            var rng = new Random(seed);
            var monsters = world.Entities.Values.OfType<Aetherium.Monster>()
                .Where(m => m.Has<Aetherium.Components.WorldLocation>())
                .OrderBy(m => m.Get<Aetherium.Components.WorldLocation>().Z)
                .ThenBy(m => m.Get<Aetherium.Components.WorldLocation>().Y)
                .ThenBy(m => m.Get<Aetherium.Components.WorldLocation>().X)
                .ToList();

            foreach (var monster in monsters)
            {
                if (monster.Has<Aetherium.Components.CreatureTypeTag>()
                    && _contentCatalog.CreaturesById.TryGetValue(monster.Get<Aetherium.Components.CreatureTypeTag>().Value, out var existing))
                {
                    Aetherium.Server.Content.ContentCompiler.ApplyCreature(monster, existing, world, preserveHealthLevel: true);
                }
                else
                {
                    var drawn = _contentCatalog.DrawSpawn(rng);
                    Aetherium.Server.Content.ContentCompiler.ApplyCreature(monster, drawn, world);
                }
            }
        }

        // Per-world reactive logic (add-eca-scripting), compiled from the world's EcaConfig. Null means
        // no rules fire — the kill path behaves exactly as before. The runtime is pure (event → resolved
        // requests); this grain executes the requests below.
        private Aetherium.Server.Eca.EcaRuntime? _ecaRuntime;

        /// <summary>Compiles a world's <see cref="Aetherium.Model.Eca.EcaConfig"/> into this map's rule
        /// runtime, seeding its `chance` RNG from the world seed. Called from InitializeAsync (fresh) and
        /// OnActivateAsync (rehydrate); null leaves the runtime absent → no rules fire.</summary>
        private void ApplyEcaConfig(Aetherium.Model.Eca.EcaConfig? config, int seed)
        {
            _ecaRuntime = config is null ? null : new Aetherium.Server.Eca.EcaRuntime(config, seed);
        }

        /// <summary>
        /// Raises the ECA <c>creature_died</c> event (add-eca-scripting) for a defeated monster and
        /// executes whatever the rules return. Called from the shared monster-defeat branch of both
        /// <see cref="AttackAsync"/> and <see cref="UseAbilityAsync"/>, after the engine's built-in kill
        /// reactions (XP, faction, loot), so rules augment the defaults rather than race them. No-op
        /// without a rule runtime. A rule-spawned creature or a rule-dealt kill does not re-enter ECA in
        /// this event (single-level, no same-tick cascade this slice).
        /// </summary>
        private async Task RunCreatureDiedRulesAsync(Entity victim, Character killer, int x, int y, int z)
        {
            if (_ecaRuntime is null || _world is null)
                return;

            var ctx = new Aetherium.Server.Eca.EcaEventContext
            {
                TriggerKind = Aetherium.Server.Eca.CreatureDiedTrigger.Id,
                VictimCreatureType = victim.Has<Aetherium.Components.CreatureTypeTag>()
                    ? victim.Get<Aetherium.Components.CreatureTypeTag>().Value
                    : victim.GetType().Name.ToLowerInvariant(),
                VictimEntityId = victim.EntityId,
                KillerEntityId = killer.EntityId,
                EventX = x,
                EventY = y,
                EventZ = z,
            };

            foreach (var request in _ecaRuntime.Evaluate(ctx))
                await ExecuteEcaRequestAsync(request);
        }

        private async Task ExecuteEcaRequestAsync(Aetherium.Server.Eca.EcaActionRequest request)
        {
            if (_world is null)
                return;

            switch (request.Kind)
            {
                case Aetherium.Server.Eca.SpawnCreatureAction.Id:
                    await ExecuteEcaSpawnAsync(request);
                    break;
                case Aetherium.Server.Eca.DealDamageAction.Id:
                    await ExecuteEcaDamageAsync(request);
                    break;
                case Aetherium.Server.Eca.ApplyStatusAction.Id:
                    ExecuteEcaStatus(request);
                    break;
            }
        }

        /// <summary>Spawns a rule's creature from the content catalog at a passable, unoccupied cell near
        /// the requested location, and fans out its placement — the same path a spawn-table draw uses.</summary>
        private async Task ExecuteEcaSpawnAsync(Aetherium.Server.Eca.EcaActionRequest request)
        {
            if (_contentCatalog is null || request.CreatureId is null
                || !_contentCatalog.CreaturesById.TryGetValue(request.CreatureId, out var definition))
                return;

            var cell = ResolveEcaSpawnCell(request.X, request.Y, request.Z);
            if (cell is null)
                return;

            var creature = new Aetherium.Monster(_world!);
            Aetherium.Server.Content.ContentCompiler.ApplyCreature(creature, definition, _world!);
            creature.Set(cell);
            _world!.AddEntity(creature);

            var placement = EntityPlacement.FromLocation(creature.EntityId, nameof(Aetherium.Monster), cell);
            EntityFactory.ExtractProperties(creature, placement);
            await FanOutAsync(new EntityPlacedDelta { Placement = placement });
        }

        /// <summary>Deals a rule's damage to the resolved target through the shared damage pipeline,
        /// fanning out the resulting health change exactly as the melee/ability paths do.</summary>
        private async Task ExecuteEcaDamageAsync(Aetherium.Server.Eca.EcaActionRequest request)
        {
            if (request.TargetEntityId is null
                || !_world!.Entities.TryGetValue(request.TargetEntityId, out var target)
                || !target.Has<Aetherium.Components.Health>())
                return;

            var packet = DamagePacket.Single(request.DamageType, request.Amount,
                sourceEntityId: null, delivery: DamageDelivery.Ranged);
            var result = _damagePipeline.Resolve(target, target, packet, _hitResolver, _deathPolicy.ResolveDyingTicks());
            if (!result.Hit)
                return;

            int remaining = target.Has<Aetherium.Components.Health>() ? target.Get<Aetherium.Components.Health>().Level : 0;
            await FanOutAsync(IntFieldDelta(request.TargetEntityId, "Health", "Level", remaining));
        }

        /// <summary>Attaches a rule's status to the resolved target (the shipped burning/slowed/prone
        /// effects). Per-tick status processing is a separate engine concern; this establishes the
        /// canonical state the effect operates on.</summary>
        private void ExecuteEcaStatus(Aetherium.Server.Eca.EcaActionRequest request)
        {
            if (request.TargetEntityId is null
                || !_world!.Entities.TryGetValue(request.TargetEntityId, out var target))
                return;

            var status = BuildEcaStatus(request);
            if (status is null)
                return;

            if (!target.Has<StatusEffects>())
                target.Set(new StatusEffects());
            target.Get<StatusEffects>().Apply(status);
        }

        private static StatusEffect? BuildEcaStatus(Aetherium.Server.Eca.EcaActionRequest request) => request.StatusId switch
        {
            "burning" => new BurningEffect(request.DurationTicks, request.Magnitude),
            "slowed" => new SlowedEffect(request.DurationTicks, request.Magnitude),
            "prone" => new ProneEffect(request.DurationTicks),
            _ => null,
        };

        /// <summary>Finds a passable, unoccupied cell for a rule-spawned creature: the requested cell if
        /// free, else an adjacent free cell, else null (the spawn is skipped rather than stacking).</summary>
        private Aetherium.Components.WorldLocation? ResolveEcaSpawnCell(int x, int y, int z)
        {
            var target = new Aetherium.Components.WorldLocation(x, y, z);
            if (_world!.PassableTerrain(target) && _world.IsOpenForOccupancy(target))
                return target;

            foreach (var adjacent in _world.Topology.Neighbors(new Aetherium.Topology.GridCoord(x, y, z)))
            {
                var neighbor = adjacent.ToWorldLocation();
                if (_world.PassableTerrain(neighbor) && _world.IsOpenForOccupancy(neighbor))
                    return neighbor;
            }
            return null;
        }

        /// <summary>Builds the behavior tree for one monster from its creature definition's
        /// behavior preset (add-content-definitions). Every preset resolves to the wander-melee
        /// tree today — this switch is the seam new presets (and, later, ECA-scripted behaviors)
        /// plug into; the validator already rejects unknown preset names at bundle load.</summary>
        private BehaviorTree BuildBehaviorTreeFor(Aetherium.Monster monster)
        {
            string preset = "wander-melee";
            if (_contentCatalog is not null
                && monster.Has<Aetherium.Components.CreatureTypeTag>()
                && _contentCatalog.CreaturesById.TryGetValue(monster.Get<Aetherium.Components.CreatureTypeTag>().Value, out var definition))
            {
                preset = definition.Behavior;
            }

            return preset switch
            {
                _ => MonsterBehaviors.BuildWanderAndMeleeTree(_combatSystem),
            };
        }

        private ActionContext? TryBuildActionContext(string sessionId)
        {
            if (_world is null) return null;
            var player = GetPlayerCharacter(sessionId);
            if (player is null) return null;
            var loc = player.Get<Aetherium.Components.WorldLocation>();
            if (loc is null) return null;
            return new ActionContext(_world, player, loc);
        }

        public async Task<Aetherium.Model.InteractionResultDto> PickupAsync(string sessionId, string targetEntityId)
        {
            var ctx = TryBuildActionContext(sessionId);
            if (ctx is null) return Fail("Map not initialized or player not on map");
            if (!IsActionable(ctx.Player)) return Fail(DownedFailureReason);

            // Capture target metadata BEFORE the interaction removes the entity from
            // _world. Needed to build the post-success ItemTransferredDelta.
            _world!.Entities.TryGetValue(targetEntityId, out var target);
            var targetType = target?.GetType().Name ?? string.Empty;

            var result = _interactionSystem.TryPickup(ctx, targetEntityId);
            if (!result.Success || target is null)
                return new Aetherium.Model.InteractionResultDto { Success = result.Success, Reason = result.Reason };

            var placement = EntityPlacement.FromLocation(targetEntityId, targetType, ctx.ViewLocation);
            EntityFactory.ExtractProperties(target, placement);

            await FanOutAsync(new ItemTransferredDelta
            {
                ItemEntityId = targetEntityId,
                IntoInventory = true,
                OwnerEntityId = ctx.Player.EntityId,
                X = ctx.ViewLocation.X, Y = ctx.ViewLocation.Y, Z = ctx.ViewLocation.Z,
                ItemPlacement = placement,
            });

            return Ok();
        }

        public async Task<Aetherium.Model.InteractionResultDto> DropAsync(string sessionId, string itemEntityId)
        {
            var ctx = TryBuildActionContext(sessionId);
            if (ctx is null) return Fail("Map not initialized or player not on map");
            if (!IsActionable(ctx.Player)) return Fail(DownedFailureReason);

            var result = _interactionSystem.TryDrop(ctx, itemEntityId);
            if (!result.Success)
                return new Aetherium.Model.InteractionResultDto { Success = false, Reason = result.Reason };

            await FanOutAsync(new ItemTransferredDelta
            {
                ItemEntityId = itemEntityId,
                IntoInventory = false,
                OwnerEntityId = ctx.Player.EntityId,
                X = ctx.ViewLocation.X, Y = ctx.ViewLocation.Y, Z = ctx.ViewLocation.Z,
                ItemPlacement = null,
            });

            return Ok();
        }

        /// <summary>
        /// Resolves a player's attack through <see cref="DamagePipeline"/> (engine gap-analysis
        /// §4.2, Phase 2 — see openspec/changes/wire-combat-pipeline-live), replacing the old
        /// instant-delete-on-kill <see cref="CombatSystem.TryAttack"/> path. Reach/existence/self-
        /// attack checks stay here (the pipeline is deliberately reach-agnostic); damage amount is
        /// still <see cref="CombatSystem.ComputeAttackDamage"/> (base <c>AttackPower</c> + best
        /// weapon bonus) so the numbers are unchanged, only how a lethal hit is handled changes: the
        /// target enters <see cref="Dying"/> (not removed) and is never deleted here — <see
        /// cref="DeathSystem"/>/<see cref="CorpseExpirySystem"/>, ticked from <see cref="TickAsync"/>,
        /// own its eventual Corpse conversion and (per <see cref="DeathPolicy"/>) removal. Clients
        /// see this as an ordinary health-changed delta, the same as any non-lethal hit — no
        /// EntityRemovedDelta is emitted for a killed monster anymore, since it is still present
        /// (as Dying, then Corpse) for a future loot/harvest/revive affordance.
        /// </summary>
        public async Task<Aetherium.Model.AttackResultDto> AttackAsync(string sessionId, string targetEntityId)
        {
            var ctx = TryBuildActionContext(sessionId);
            if (ctx is null)
                return Aetherium.Model.AttackResultDto.Fail("Map not initialized or player not on map");
            if (!IsActionable(ctx.Player))
                return Aetherium.Model.AttackResultDto.Fail(DownedFailureReason);

            if (string.IsNullOrEmpty(targetEntityId))
                return Aetherium.Model.AttackResultDto.Fail("No target");
            if (targetEntityId == ctx.Player.EntityId)
                return Aetherium.Model.AttackResultDto.Fail("Cannot attack yourself");
            if (!_world!.Entities.TryGetValue(targetEntityId, out var target) || target is null)
                return Aetherium.Model.AttackResultDto.Fail("Target not found");
            if (!target.Has<Aetherium.Components.WorldLocation>())
                return Aetherium.Model.AttackResultDto.Fail("Attacker or target has no location");

            var attackerLoc = ctx.ViewLocation;
            var targetLoc = target.Get<Aetherium.Components.WorldLocation>();
            // Melee reach: topology metric on the plane + vertical steps (on square,
            // the Manhattan distance this gate has always used).
            int distance = _world.Topology.Distance(
                               Aetherium.Topology.GridCoord.From(attackerLoc),
                               Aetherium.Topology.GridCoord.From(targetLoc))
                         + Math.Abs(targetLoc.Z - attackerLoc.Z);
            if (distance > 1)
                return Aetherium.Model.AttackResultDto.Fail("Target is not in reach");

            bool targetWasMonster = target is Aetherium.Monster;
            var (lx, ly, lz) = (targetLoc.X, targetLoc.Y, targetLoc.Z);
            var targetType = target.GetType().Name;

            int damage = CombatSystem.ComputeAttackDamage(ctx.Player);
            var packet = DamagePacket.Single("physical", damage, ctx.Player.EntityId);
            var result = _damagePipeline.Resolve(ctx.Player, target, packet, _hitResolver, _deathPolicy.ResolveDyingTicks());
            if (!result.Hit)
                return new Aetherium.Model.AttackResultDto { Success = false, Reason = result.Reason ?? "Miss" };

            int remainingHealth = target.Has<Aetherium.Components.Health>() ? target.Get<Aetherium.Components.Health>().Level : 0;
            int roundedDamage = (int)Math.Round(result.Damage);

            // Combat analytics (persisted with the map): total damage, and kills on defeat.
            _mapState.State.TotalDamageDealt += roundedDamage;
            if (result.TargetEnteredDying && targetWasMonster)
            {
                _mapState.State.MonstersDefeated++;
                AwardKillXp(ctx.Player, targetType);
                ApplyFactionAction(ctx.Player, KillActionTagFor(target));
            }

            // Death loot: a slain monster drops a weapon where it fell, at the same moment it
            // enters Dying (not later, on its eventual Corpse conversion) — preserves the
            // "kill → pick up sword → hit harder" timing from before this pipeline swap.
            // Deterministic (always drops) so it stays testable.
            string? lootId = null;
            string? lootType = null;
            if (result.TargetEnteredDying && targetWasMonster)
            {
                (lootId, lootType) = SpawnMonsterLoot(target, lx, ly, lz);
            }

            await _mapState.WriteStateAsync();

            await FanOutAsync(IntFieldDelta(targetEntityId, "Health", "Level", remainingHealth));

            if (lootId is not null)
            {
                var placement = EntityPlacement.FromLocation(lootId, lootType!,
                    new Aetherium.Components.WorldLocation(lx, ly, lz));
                await FanOutAsync(new EntityPlacedDelta { Placement = placement });
            }

            // Reactive rules (add-eca-scripting): a monster defeat raises creature_died, after the
            // built-in kill reactions above, so rules augment rather than race them.
            if (result.TargetEnteredDying && targetWasMonster)
                await RunCreatureDiedRulesAsync(target, ctx.Player, lx, ly, lz);

            return new Aetherium.Model.AttackResultDto
            {
                Success = true,
                Damage = roundedDamage,
                RemainingHealth = remainingHealth,
                TargetDefeated = result.TargetEnteredDying,
                TargetType = targetType,
                TargetEntityId = targetEntityId,
                DroppedLootEntityId = lootId,
                DroppedLootType = lootType,
            };
        }

        public Task<Aetherium.Model.CombatStatsDto> GetCombatStatsAsync()
        {
            var state = _mapState.State;
            return Task.FromResult(new Aetherium.Model.CombatStatsDto
            {
                MonstersDefeated = state?.MonstersDefeated ?? 0,
                TotalDamageDealt = state?.TotalDamageDealt ?? 0,
            });
        }

        public Task<DeathPolicy> GetDeathPolicyAsync() => Task.FromResult(_deathPolicy);

        /// <summary>
        /// Casts a player's ability (engine gap-analysis §4.3, Phase 2 — see wire-abilities-live)
        /// from the map's per-world compiled <see cref="AbilityCatalog"/>. Gated in order by:
        /// actionable (not Downed/Corpse), catalog membership, per-caster cooldown, resource
        /// affordability, single-target reach (when targeted), and the caster's <see cref="ActionSpeed"/>
        /// budget — nothing is committed until every gate passes. On success it applies the ability's
        /// effects (which reuse the live <see cref="DamagePipeline"/>/<see cref="StatusEffects"/>/pool
        /// systems), then derives world deltas by diffing the target's Health/Dying state around the
        /// effects — so a damaging cast that defeats a monster drops loot and records analytics
        /// identically to a melee <see cref="AttackAsync"/>. Cast execution is instant this slice; the
        /// definition's charge/cast/recover fields are unconsumed until phased casting ships.
        /// </summary>
        public async Task<AbilityResultDto> UseAbilityAsync(string sessionId, string abilityId, string? targetEntityId)
        {
            var ctx = TryBuildActionContext(sessionId);
            if (ctx is null) return AbilityResultDto.Fail("Map not initialized or player not on map");
            if (!IsActionable(ctx.Player)) return AbilityResultDto.Fail(DownedFailureReason);

            if (string.IsNullOrEmpty(abilityId))
                return AbilityResultDto.Fail("No ability specified");
            if (!_abilityCatalog.TryGet(abilityId, out var ability) || ability is null)
                return AbilityResultDto.Fail("Unknown ability");

            // Skill-gated casting (engine gap-analysis §4.4 — see wire-progression-live): enforced
            // only when the world opts in via RequireSkillToCastAbilities. When false (the default)
            // catalog membership remains the sole ability gate, preserving pre-progression behavior.
            if (_progressionConfig?.RequireSkillToCastAbilities == true
                && (!ctx.Player.Has<GrantedAbilities>() || !ctx.Player.Get<GrantedAbilities>().Has(abilityId)))
                return AbilityResultDto.Fail("Ability not yet learned");

            // Cooldown gate.
            if (ctx.Player.Has<AbilityCooldowns>() && ctx.Player.Get<AbilityCooldowns>().IsOnCooldown(abilityId))
                return AbilityResultDto.Fail("Ability is on cooldown");

            // Resource gate (check only — committed after every gate passes).
            ResourcePool? pool = null;
            if (!string.IsNullOrEmpty(ability.ResourcePoolTag) && ability.ResourceCost > 0)
            {
                if (!ctx.Player.Has<ResourcePools>()
                    || !ctx.Player.Get<ResourcePools>().TryGet(ability.ResourcePoolTag!, out pool)
                    || pool is null)
                    return AbilityResultDto.Fail("Required resource pool is unavailable");
                if (!pool.CanAfford(ability.ResourceCost))
                    return AbilityResultDto.Fail("Insufficient resource");
            }

            // Target resolution + reach gate (single-entity target only this slice).
            Entity? target = null;
            int tx = 0, ty = 0, tz = 0;
            bool targetWasMonster = false;
            if (!string.IsNullOrEmpty(targetEntityId))
            {
                if (!_world!.Entities.TryGetValue(targetEntityId!, out target) || target is null)
                    return AbilityResultDto.Fail("Target not found");
                if (!target.Has<Aetherium.Components.WorldLocation>())
                    return AbilityResultDto.Fail("Target has no location");
                var tl = target.Get<Aetherium.Components.WorldLocation>();
                tx = tl.X; ty = tl.Y; tz = tl.Z;
                int distance = _world!.Topology.Distance(
                                   Aetherium.Topology.GridCoord.From(ctx.ViewLocation),
                                   Aetherium.Topology.GridCoord.From(tl))
                             + Math.Abs(tl.Z - ctx.ViewLocation.Z);
                if (distance > (int)Math.Ceiling(ability.Range))
                    return AbilityResultDto.Fail("Target is out of range");
                targetWasMonster = target is Aetherium.Monster;
            }

            // Action-budget gate (commits AP). Flat cost this slice.
            if (ctx.Player.Has<Aetherium.Components.ActionSpeed>()
                && !ctx.Player.Get<Aetherium.Components.ActionSpeed>().TrySpend(AbilityActionCost))
                return AbilityResultDto.Fail("Not enough action budget");

            // Commit resource cost now that the cast is going ahead.
            pool?.TrySpend(ability.ResourceCost);

            // Snapshot the target's vitals so post-effect deltas can be derived generically — effects
            // are opaque (void), so the grain diffs Health/Dying around them rather than the effects
            // reporting what they did.
            int? preHealth = target is not null && target.Has<Aetherium.Components.Health>()
                ? target.Get<Aetherium.Components.Health>().Level
                : (int?)null;
            bool preDying = target is not null && target.Has<Dying>();

            // Apply the ability's effects in order.
            var effectContext = new AbilityEffectContext(_world!, ctx.Player, target);
            foreach (var effect in ability.Effects)
                effect.Apply(effectContext);

            // Put the ability on cooldown for the caster.
            if (ability.Cooldown > 0)
            {
                if (!ctx.Player.Has<AbilityCooldowns>())
                    ctx.Player.Set(new AbilityCooldowns());
                ctx.Player.Get<AbilityCooldowns>().SetCooldown(abilityId, (int)Math.Ceiling(ability.Cooldown));
            }

            // Derive + fan out world deltas for a target whose Health changed.
            double damageDealt = 0;
            bool targetDefeated = false;
            if (target is not null && preHealth is not null && target.Has<Aetherium.Components.Health>())
            {
                int postHealth = target.Get<Aetherium.Components.Health>().Level;
                if (postHealth != preHealth.Value)
                {
                    damageDealt = preHealth.Value - postHealth;
                    if (damageDealt > 0)
                        _mapState.State.TotalDamageDealt += (long)Math.Round(damageDealt);

                    bool enteredDying = !preDying && target.Has<Dying>();
                    if (enteredDying && targetWasMonster)
                    {
                        _mapState.State.MonstersDefeated++;
                        targetDefeated = true;
                        AwardKillXp(ctx.Player, target.GetType().Name);
                        ApplyFactionAction(ctx.Player, KillActionTagFor(target));
                    }

                    await _mapState.WriteStateAsync();
                    await FanOutAsync(IntFieldDelta(targetEntityId!, "Health", "Level", postHealth));

                    if (targetDefeated)
                    {
                        var (lootId, lootType) = SpawnMonsterLoot(target, tx, ty, tz);
                        if (lootId is not null)
                        {
                            var placement = EntityPlacement.FromLocation(lootId, lootType!,
                                new Aetherium.Components.WorldLocation(tx, ty, tz));
                            await FanOutAsync(new EntityPlacedDelta { Placement = placement });
                        }

                        // Reactive rules (add-eca-scripting): the ability kill raises creature_died,
                        // the same event the melee path raises, through the same runtime.
                        await RunCreatureDiedRulesAsync(target, ctx.Player, tx, ty, tz);
                    }
                }
            }

            return new AbilityResultDto
            {
                Success = true,
                AbilityId = abilityId,
                TargetEntityId = targetEntityId,
                TargetDefeated = targetDefeated,
                DamageDealt = damageDealt,
            };
        }

        /// <summary>Spawns a monster's death-drop at the fall location — the shared drop used by
        /// both a melee kill (<see cref="AttackAsync"/>) and an ability kill so the two never
        /// diverge. When the victim's <see cref="Aetherium.Components.CreatureTypeTag"/> resolves
        /// to a content-catalog definition (add-content-definitions), the drop is that definition's
        /// loot item — or nothing, when the definition declares none. A victim with no resolvable
        /// definition keeps the legacy <see cref="Aetherium.Entities.SwordItem"/> drop. Adds to
        /// <c>_world</c> only; the caller fans out the placement delta when LootId is non-null.</summary>
        private (string? LootId, string? LootType) SpawnMonsterLoot(Entity victim, int lx, int ly, int lz)
        {
            Aetherium.Entities.Item loot;
            string lootType;
            if (_contentCatalog is not null
                && victim.Has<Aetherium.Components.CreatureTypeTag>()
                && _contentCatalog.CreaturesById.TryGetValue(victim.Get<Aetherium.Components.CreatureTypeTag>().Value, out var definition))
            {
                if (definition.LootItemId is null
                    || !_contentCatalog.ItemsById.TryGetValue(definition.LootItemId, out var itemDefinition))
                    return (null, null); // defined creature, no drop
                loot = Aetherium.Server.Content.ContentCompiler.MaterializeItem(itemDefinition);
                lootType = itemDefinition.Id;
            }
            else
            {
                loot = new Aetherium.Entities.SwordItem();
                lootType = nameof(Aetherium.Entities.SwordItem);
            }

            loot.Set(new Aetherium.Components.WorldLocation(lx, ly, lz));
            _world!.AddEntity(loot);
            return (loot.EntityId, lootType);
        }

        /// <summary>Per-tick ability upkeep (engine gap-analysis §4.3 — see wire-abilities-live): counts
        /// down every actor's <see cref="AbilityCooldowns"/> and regenerates every actor's
        /// <see cref="ResourcePools"/>. A non-empty <see cref="ThreatTable"/> is the in-combat signal
        /// that gates <c>OutOfCombat</c>-policy regen (the engine has no dedicated combat-state flag).</summary>
        private void TickAbilityUpkeep()
        {
            if (_world is null) return;

            // Ability cooldowns and resource pools live only on characters (players/monsters).
            // Iterate the Characters index, not world.Entities — on a large outdoor map the latter
            // is ~150k tile entities, and scanning them all every tick cost ~2.6s and starved player
            // input on the single-threaded grain (see the death/corpse systems, same fix).
            foreach (var entity in _world.Characters.Values)
            {
                if (entity.Has<AbilityCooldowns>())
                    entity.Get<AbilityCooldowns>().Tick();

                if (entity.Has<ResourcePools>())
                {
                    bool inCombat = entity.Has<ThreatTable>()
                        && entity.Get<ThreatTable>().ThreatByAttacker.Count > 0;
                    foreach (var poolEntry in entity.Get<ResourcePools>().All)
                        poolEntry.Regen(inCombat);
                }
            }
        }

        public Task<ResourcePoolsDto> GetResourcePoolsAsync(string sessionId)
        {
            var dto = new ResourcePoolsDto();
            var player = GetPlayerCharacter(sessionId);
            if (player is not null && player.Has<ResourcePools>())
            {
                foreach (var poolEntry in player.Get<ResourcePools>().All)
                    dto.Pools.Add(new ResourcePoolDto
                    {
                        Tag = poolEntry.Tag,
                        Current = poolEntry.Current,
                        Max = poolEntry.Max,
                        IsInverse = poolEntry.IsInverse,
                    });
            }
            return Task.FromResult(dto);
        }

        public Task<Dictionary<string, int>> GetAbilityCooldownsAsync(string sessionId)
        {
            var result = new Dictionary<string, int>();
            var player = GetPlayerCharacter(sessionId);
            if (player is not null && player.Has<AbilityCooldowns>())
                foreach (var kv in player.Get<AbilityCooldowns>().Snapshot)
                    result[kv.Key] = kv.Value;
            return Task.FromResult(result);
        }

        /// <summary>Awards XP to <paramref name="killer"/>'s progress pools for defeating an entity of
        /// type <paramref name="defeatedType"/> (engine gap-analysis §4.4 — see wire-progression-live),
        /// per the world's declarative <see cref="XpAwardRule"/>s. Called from the shared monster-defeat
        /// branch of both <see cref="AttackAsync"/> and <see cref="UseAbilityAsync"/> so melee and
        /// ability kills award identically. A rule targeting an undefined pool (no compiled curve) is
        /// skipped. No-op when the world declares no progression.</summary>
        private void AwardKillXp(Character killer, string defeatedType)
        {
            if (_progressionConfig is null || _progressionConfig.XpAwardRules.Count == 0)
                return;
            if (!killer.Has<ProgressPools>())
                return;

            var pools = killer.Get<ProgressPools>();
            foreach (var rule in _progressionConfig.XpAwardRules)
            {
                if (rule.OnEvent != XpAwardEvent.MonsterDefeated)
                    continue;
                if (!string.IsNullOrEmpty(rule.EnemyTypeFilter)
                    && !string.Equals(rule.EnemyTypeFilter, defeatedType, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (_levelCurvesByPool.TryGetValue(rule.PoolId, out var curve) && curve is not null)
                    pools.AddXp(rule.PoolId, rule.Amount, curve);
            }
        }

        /// <summary>The engine-emitted faction action tag for killing <paramref name="victim"/>:
        /// <c>kill:&lt;creature-type&gt;</c>, preferring the spawn-time <see cref="Aetherium.Components.CreatureTypeTag"/>
        /// (so "wolf" and "bandit" stay distinct despite sharing the Monster class) and falling back
        /// to the lowercased C# type name for entities spawned outside SpawnEntityAsync.</summary>
        private static string KillActionTagFor(Entity victim)
        {
            string creatureType = victim.Has<Aetherium.Components.CreatureTypeTag>()
                ? victim.Get<Aetherium.Components.CreatureTypeTag>().Value
                : victim.GetType().Name.ToLowerInvariant();
            return $"kill:{creatureType}";
        }

        /// <summary>
        /// The single standing-mutation chokepoint (engine gap-analysis §4.6 — see wire-factions-live
        /// and docs/factions-reputation.md §3): applies <paramref name="actionTag"/> against every
        /// configured faction's doctrine — each judges the act by its own values, unknown tags mean
        /// no effect — then re-evaluates declarative rank grants for each changed standing. Kill-site
        /// code is just the first caller; future quest/trade/trespass emitters call this with their
        /// own tag families, and the ECA generalization replaces this method's inside, not its call
        /// sites. No-op when the world declares no factions or the actor carries no ledger.
        /// </summary>
        private void ApplyFactionAction(Character actor, string actionTag)
        {
            if (_factionConfig is null || !actor.Has<ReputationLedger>())
                return;

            var ledger = actor.Get<ReputationLedger>();
            foreach (var faction in _factionRegistry.All)
            {
                if (faction.Doctrine.DeltaFor(actionTag) == 0)
                    continue;

                var reputation = ledger.ApplyAction(faction, actionTag);
                var def = _factionConfig.Factions.FirstOrDefault(f => f.Id == faction.Id);
                RankEvaluator.Apply(reputation, def?.RankRules);
            }
        }

        public Task<ReputationLedgerDto> GetReputationAsync(string sessionId)
        {
            var dto = new ReputationLedgerDto();
            var player = GetPlayerCharacter(sessionId);
            if (player is not null && player.Has<ReputationLedger>())
            {
                foreach (var kv in player.Get<ReputationLedger>().ByFaction)
                {
                    dto.Reputations.Add(new ReputationDto
                    {
                        FactionId = kv.Key,
                        Standing = kv.Value.Standing,
                        Band = BandResolver.Resolve(kv.Value.Standing, _factionConfig?.Bands),
                        Ranks = new List<string>(kv.Value.Ranks),
                    });
                }
            }
            return Task.FromResult(dto);
        }

        public Task<FactionsStateDto> GetFactionsAsync()
        {
            var dto = new FactionsStateDto();
            if (_factionConfig is not null)
            {
                foreach (var faction in _factionRegistry.All)
                    dto.Factions.Add(new FactionInfoDto
                    {
                        Id = faction.Id,
                        Name = faction.Name,
                        Tags = new List<string>(faction.Tags),
                    });

                // Report relations from the source definitions (the compiled matrix is sparse and
                // unenumerable by design); Mutual definitions expand to both directions.
                foreach (var rel in _factionConfig.Relations)
                {
                    dto.Relations.Add(new FactionRelationDto
                    {
                        FromFactionId = rel.FromFactionId,
                        ToFactionId = rel.ToFactionId,
                        Disposition = rel.Disposition,
                    });
                    if (rel.Mutual)
                        dto.Relations.Add(new FactionRelationDto
                        {
                            FromFactionId = rel.ToFactionId,
                            ToFactionId = rel.FromFactionId,
                            Disposition = rel.Disposition,
                        });
                }

                dto.Bands.AddRange(_factionConfig.Bands);
            }
            return Task.FromResult(dto);
        }

        /// <summary>Applies the world's <see cref="AttributeDerivation"/>s to <paramref name="player"/>
        /// (engine gap-analysis §4.4): writes each derived stat (<c>Base + PerPoint × attribute</c>)
        /// onto its component. A rise in a derived max (e.g. vitality → max health) also raises the
        /// current value by the same delta, so joining at higher vitality spawns at full derived
        /// health and a mid-run vitality gain heals proportionally. Called at join and after any
        /// attribute change; no-op when the world declares no derivations.</summary>
        private void ApplyAttributeDerivations(Character player)
        {
            if (_progressionConfig is null || _progressionConfig.AttributeDerivations.Count == 0)
                return;
            if (!player.Has<Aetherium.Server.Progression.Attributes>())
                return;

            var attrs = player.Get<Aetherium.Server.Progression.Attributes>();
            foreach (var d in _progressionConfig.AttributeDerivations)
            {
                double value = d.Base + d.PerPoint * attrs.Get(d.AttributeId, 0);
                switch (d.DerivedStat)
                {
                    case DerivedStat.HealthMax when player.Has<Aetherium.Components.Health>():
                        var health = player.Get<Aetherium.Components.Health>();
                        int newMax = (int)Math.Round(value);
                        int delta = newMax - health.MaxLevel;
                        health.MaxLevel = newMax;
                        if (delta > 0)
                            health.Level += delta;
                        health.Level = Math.Clamp(health.Level, 0, newMax);
                        break;
                    case DerivedStat.ActionSpeed when player.Has<Aetherium.Components.ActionSpeed>():
                        player.Get<Aetherium.Components.ActionSpeed>().Speed = value;
                        break;
                }
            }
        }

        /// <summary>Unlocks a skill for the session's player (engine gap-analysis §4.4 — see
        /// wire-progression-live): gated by <see cref="SkillUnlockService"/> (prerequisites + optional
        /// pool-level requirement), then applies the skill's effects — <c>ModifiesAttributeId</c>
        /// adjusts an attribute (re-deriving dependent stats) and <c>UnlocksAbilityId</c> adds to the
        /// player's granted abilities.</summary>
        public Task<UnlockSkillResultDto> UnlockSkillAsync(string sessionId, string skillId)
        {
            var player = GetPlayerCharacter(sessionId);
            if (player is null)
                return Task.FromResult(UnlockSkillResultDto.Fail("Map not initialized or player not on map"));
            if (!IsActionable(player))
                return Task.FromResult(UnlockSkillResultDto.Fail(DownedFailureReason));
            if (!player.Has<UnlockedSkills>())
                return Task.FromResult(UnlockSkillResultDto.Fail("This world has no progression"));

            var unlocked = player.Get<UnlockedSkills>();
            var pools = player.Has<ProgressPools>() ? player.Get<ProgressPools>() : null;
            var outcome = _skillUnlockService.TryUnlock(unlocked, _skillCatalog, skillId, pools);
            if (outcome != SkillUnlockResult.Unlocked)
                return Task.FromResult(new UnlockSkillResultDto
                {
                    Success = false,
                    SkillId = skillId,
                    Result = outcome.ToString(),
                    Reason = outcome switch
                    {
                        SkillUnlockResult.AlreadyUnlocked => "Skill already unlocked",
                        SkillUnlockResult.UnknownSkill => "Unknown skill",
                        SkillUnlockResult.PrerequisitesNotMet => "Prerequisites not met",
                        SkillUnlockResult.PoolLevelTooLow => "Required pool level not reached",
                        _ => "Skill could not be unlocked",
                    },
                });

            // Apply the unlocked skill's effects.
            if (_skillCatalog.TryGet(skillId, out var skill) && skill is not null)
            {
                if (!string.IsNullOrEmpty(skill.ModifiesAttributeId)
                    && player.Has<Aetherium.Server.Progression.Attributes>())
                {
                    var attrs = player.Get<Aetherium.Server.Progression.Attributes>();
                    attrs.Set(skill.ModifiesAttributeId!, attrs.Get(skill.ModifiesAttributeId!) + skill.ModifierAmount);
                    ApplyAttributeDerivations(player);
                }
                if (!string.IsNullOrEmpty(skill.UnlocksAbilityId) && player.Has<GrantedAbilities>())
                    player.Get<GrantedAbilities>().Grant(skill.UnlocksAbilityId!);
            }

            return Task.FromResult(new UnlockSkillResultDto
            {
                Success = true,
                SkillId = skillId,
                Result = SkillUnlockResult.Unlocked.ToString(),
            });
        }

        public Task<ProgressionStateDto> GetProgressionAsync(string sessionId)
        {
            var dto = new ProgressionStateDto();
            var player = GetPlayerCharacter(sessionId);
            if (player is not null)
            {
                if (player.Has<ProgressPools>())
                    foreach (var kv in player.Get<ProgressPools>().Pools)
                        dto.Pools.Add(new ProgressPoolDto { Id = kv.Key, Xp = kv.Value.Xp, Level = kv.Value.Level });

                if (player.Has<Aetherium.Server.Progression.Attributes>())
                    foreach (var kv in player.Get<Aetherium.Server.Progression.Attributes>().Values)
                        dto.Attributes[kv.Key] = kv.Value;

                if (player.Has<UnlockedSkills>())
                    dto.UnlockedSkills.AddRange(player.Get<UnlockedSkills>().Ids);

                if (player.Has<GrantedAbilities>())
                    dto.GrantedAbilities.AddRange(player.Get<GrantedAbilities>().Ids);
            }
            return Task.FromResult(dto);
        }

        public async Task<Aetherium.Model.InteractionResultDto> OpenAsync(string sessionId, string targetEntityId)
            => await ToggleDoorAsync(sessionId, targetEntityId, wantOpen: true);

        public async Task<Aetherium.Model.InteractionResultDto> CloseAsync(string sessionId, string targetEntityId)
            => await ToggleDoorAsync(sessionId, targetEntityId, wantOpen: false);

        private async Task<Aetherium.Model.InteractionResultDto> ToggleDoorAsync(string sessionId, string targetEntityId, bool wantOpen)
        {
            var ctx = TryBuildActionContext(sessionId);
            if (ctx is null) return Fail("Map not initialized or player not on map");
            if (!IsActionable(ctx.Player)) return Fail(DownedFailureReason);

            var result = wantOpen
                ? _interactionSystem.TryOpen(ctx, targetEntityId)
                : _interactionSystem.TryClose(ctx, targetEntityId);

            if (!result.Success)
                return new Aetherium.Model.InteractionResultDto { Success = false, Reason = result.Reason };

            // The InteractionSystem mutated the OpensAndCloses component; read it back
            // for the delta. We already know the target exists because TryOpen/TryClose
            // returned success.
            var target = ctx.World.Entities[targetEntityId];
            var oc = target.Get<Aetherium.Components.OpensAndCloses>()!;

            await FanOutAsync(new DoorStateChangedDelta
            {
                EntityId = targetEntityId,
                IsOpen = oc.IsOpen,
                IsLocked = oc.IsLocked,
            });

            return Ok();
        }

        public async Task<Aetherium.Model.InteractionResultDto> UseAsync(string sessionId, string itemEntityId, string onEntityId, string? usageId)
        {
            // Full-fidelity Use via InteractionSystem. Snapshot the mutable fields
            // we care about, delegate the mutation, then diff and emit deltas.
            // See openspec/changes/extend-delta-vocabulary-for-use-disambiguation.
            var ctx = TryBuildActionContext(sessionId);
            if (ctx is null) return Fail("Map not initialized or player not on map");
            if (!IsActionable(ctx.Player)) return Fail(DownedFailureReason);

            // Resolve item (must be in inventory). Target is optional for some
            // modes (consume, place) but required for others (unlock, lockpick,
            // force-open). InteractionSystem handles the per-mode requirement.
            var inv = ctx.Player.Get<Aetherium.Components.Inventory>();
            if (inv is null || !inv.Items.TryGetValue(itemEntityId, out var item))
                return Fail("Item not in inventory");

            ctx.World.Entities.TryGetValue(onEntityId, out var target);

            // Reactive disambiguation when usageId is omitted.
            string resolvedUsageId;
            if (string.IsNullOrEmpty(usageId))
            {
                var options = _interactionSystem.GetUseOptions(ctx, itemEntityId, target is null ? null : onEntityId);
                if (options.Count == 0) return Fail("No effect");
                if (options.Count > 1)
                {
                    // Return options for the client to pick from.
                    return new Aetherium.Model.InteractionResultDto
                    {
                        Success = false,
                        Reason = "Multiple usage options available",
                        Options = options.Select(o => new Aetherium.Model.UsageOptionDto
                        {
                            UsageId = o.UsageId,
                            Label = o.Label,
                            Description = o.Description,
                        }).ToList()
                    };
                }
                resolvedUsageId = options[0].UsageId;
            }
            else
            {
                resolvedUsageId = usageId;
            }

            // Snapshot all fields the dispatcher could mutate. We track them on
            // the item, the target (if a door), and the player. Inventory
            // membership is tracked separately so we can detect destruction
            // (item leaves inventory and is NOT in the world) vs placement
            // (item leaves inventory and IS in the world).
            var snapshot = SnapshotUseFields(ctx, item, target);

            var result = _interactionSystem.TryUseWithMode(ctx, itemEntityId, target is null ? null : onEntityId, resolvedUsageId);
            if (!result.Success)
                return new Aetherium.Model.InteractionResultDto { Success = false, Reason = result.Reason };

            await EmitUseDeltasAsync(ctx, item, target, snapshot);
            return Ok();
        }

        /// <summary>
        /// Component-field values captured before a Use action so the grain can
        /// diff the post-state and emit precise deltas. Single-purpose record;
        /// not used outside UseAsync.
        /// </summary>
        private sealed class UseFieldSnapshot
        {
            public int? ConsumableUses;
            public int? HealthLevel;
            public int? ForcesDoorDurability;
            public int? LockpickDurability;
            public bool? PlaceableLightIsPlaced;
            public bool? LightSourceIsEnabled;
            public bool? LightSourceIsDynamic;
            public bool? ActivatableIsActivated;
            public int? InventoryCapacity;

            public bool? DoorIsOpen;
            public bool? DoorIsLocked;

            public bool ItemWasInInventory;
            public bool ItemWasInWorld;
        }

        private static UseFieldSnapshot SnapshotUseFields(ActionContext ctx, Aetherium.Core.Entity item, Aetherium.Core.Entity? target)
        {
            var s = new UseFieldSnapshot();

            s.ConsumableUses = item.Get<Aetherium.Components.Consumable>()?.Uses;
            s.ForcesDoorDurability = item.Get<Aetherium.Components.ForcesDoor>()?.Durability;
            s.LockpickDurability = item.Get<Aetherium.Components.Lockpick>()?.Durability;
            s.PlaceableLightIsPlaced = item.Get<Aetherium.Components.PlaceableLight>()?.IsPlaced;
            var ls = item.Get<Aetherium.Components.LightSource>();
            s.LightSourceIsEnabled = ls?.IsEnabled;
            s.LightSourceIsDynamic = ls?.IsDynamic;

            s.HealthLevel = ctx.Player.Get<Aetherium.Components.Health>()?.Level;
            s.InventoryCapacity = ctx.Player.Get<Aetherium.Components.Inventory>()?.Capacity;

            if (target is not null)
            {
                var door = target.Get<Aetherium.Components.OpensAndCloses>();
                s.DoorIsOpen = door?.IsOpen;
                s.DoorIsLocked = door?.IsLocked;
                s.ActivatableIsActivated = target.Get<Aetherium.Components.Activatable>()?.IsActivated;
            }

            var inv = ctx.Player.Get<Aetherium.Components.Inventory>();
            s.ItemWasInInventory = inv is not null && inv.Items.ContainsKey(item.EntityId);
            s.ItemWasInWorld = ctx.World.Entities.ContainsKey(item.EntityId);

            return s;
        }

        /// <summary>
        /// Diffs the post-Use state against the pre-call snapshot and emits one
        /// delta per changed field, plus the appropriate inventory/world
        /// transition delta if the item moved.
        /// </summary>
        private async Task EmitUseDeltasAsync(ActionContext ctx, Aetherium.Core.Entity item, Aetherium.Core.Entity? target, UseFieldSnapshot snapshot)
        {
            var inv = ctx.Player.Get<Aetherium.Components.Inventory>();
            var itemIsInInventory = inv is not null && inv.Items.ContainsKey(item.EntityId);
            var itemIsInWorld = ctx.World.Entities.ContainsKey(item.EntityId);

            // Inventory transitions first — the field deltas below need to refer
            // to an item the receiver can still find.
            if (snapshot.ItemWasInInventory && !itemIsInInventory)
            {
                if (itemIsInWorld)
                {
                    // Placement: item went from inventory to world (TryPlace).
                    var placement = EntityPlacement.FromLocation(item.EntityId, item.GetType().Name, ctx.ViewLocation);
                    await FanOutAsync(new EntityPlacedDelta
                    {
                        Placement = placement,
                        SourceOwnerEntityId = ctx.Player.EntityId,
                    });
                }
                else
                {
                    // Destruction: item left inventory and didn't enter the world
                    // (Consumable at zero uses, broken Lockpick, broken ForcesDoor).
                    await FanOutAsync(new ItemDestroyedDelta
                    {
                        EntityId = item.EntityId,
                        OwnerEntityId = ctx.Player.EntityId,
                    });
                }
            }

            // Component-field deltas. Door state has its own delta type because
            // it bundles IsOpen+IsLocked; everything else goes through the
            // generic ComponentFieldChangedDelta.
            if (target is not null)
            {
                var door = target.Get<Aetherium.Components.OpensAndCloses>();
                if (door is not null && (door.IsOpen != snapshot.DoorIsOpen || door.IsLocked != snapshot.DoorIsLocked))
                {
                    await FanOutAsync(new DoorStateChangedDelta
                    {
                        EntityId = target.EntityId,
                        IsOpen = door.IsOpen,
                        IsLocked = door.IsLocked,
                    });
                }

                var activatable = target.Get<Aetherium.Components.Activatable>();
                if (activatable is not null && activatable.IsActivated != snapshot.ActivatableIsActivated)
                {
                    await FanOutAsync(BoolFieldDelta(target.EntityId, "Activatable", "IsActivated", activatable.IsActivated));
                }
            }

            // Item-side fields. Skip if item was destroyed (ItemDestroyedDelta is enough).
            if (itemIsInInventory || itemIsInWorld)
            {
                var consumable = item.Get<Aetherium.Components.Consumable>();
                if (consumable is not null && consumable.Uses != snapshot.ConsumableUses)
                    await FanOutAsync(IntFieldDelta(item.EntityId, "Consumable", "Uses", consumable.Uses));

                var forces = item.Get<Aetherium.Components.ForcesDoor>();
                if (forces is not null && forces.Durability != snapshot.ForcesDoorDurability)
                    await FanOutAsync(IntFieldDelta(item.EntityId, "ForcesDoor", "Durability", forces.Durability));

                var lockpick = item.Get<Aetherium.Components.Lockpick>();
                if (lockpick is not null && lockpick.Durability != snapshot.LockpickDurability)
                    await FanOutAsync(IntFieldDelta(item.EntityId, "Lockpick", "Durability", lockpick.Durability));

                var placeable = item.Get<Aetherium.Components.PlaceableLight>();
                if (placeable is not null && placeable.IsPlaced != snapshot.PlaceableLightIsPlaced)
                    await FanOutAsync(BoolFieldDelta(item.EntityId, "PlaceableLight", "IsPlaced", placeable.IsPlaced));

                var ls = item.Get<Aetherium.Components.LightSource>();
                if (ls is not null)
                {
                    if (ls.IsEnabled != snapshot.LightSourceIsEnabled)
                        await FanOutAsync(BoolFieldDelta(item.EntityId, "LightSource", "IsEnabled", ls.IsEnabled));
                    if (ls.IsDynamic != snapshot.LightSourceIsDynamic)
                        await FanOutAsync(BoolFieldDelta(item.EntityId, "LightSource", "IsDynamic", ls.IsDynamic));
                }
            }

            // Player-side fields.
            var health = ctx.Player.Get<Aetherium.Components.Health>();
            if (health is not null && health.Level != snapshot.HealthLevel)
                await FanOutAsync(IntFieldDelta(ctx.Player.EntityId, "Health", "Level", health.Level));

            var playerInv = ctx.Player.Get<Aetherium.Components.Inventory>();
            if (playerInv is not null && playerInv.Capacity != snapshot.InventoryCapacity)
                await FanOutAsync(IntFieldDelta(ctx.Player.EntityId, "Inventory", "Capacity", playerInv.Capacity));
        }

        private static ComponentFieldChangedDelta IntFieldDelta(string entityId, string componentType, string fieldName, int value)
            => new ComponentFieldChangedDelta
            {
                EntityId = entityId,
                ComponentType = componentType,
                FieldName = fieldName,
                NumericValue = value,
            };

        private static ComponentFieldChangedDelta BoolFieldDelta(string entityId, string componentType, string fieldName, bool value)
            => new ComponentFieldChangedDelta
            {
                EntityId = entityId,
                ComponentType = componentType,
                FieldName = fieldName,
                BoolValue = value,
            };

        // ----- helpers ---------------------------------------------------

        private static Aetherium.Model.InteractionResultDto Ok()
            => new Aetherium.Model.InteractionResultDto { Success = true };

        private static Aetherium.Model.InteractionResultDto Fail(string reason)
            => new Aetherium.Model.InteractionResultDto { Success = false, Reason = reason };

        // The former DegreesToCardinal/RotateRelativeByHeading helpers live on as the
        // reference implementations inside SquareTopologyGoldenTests, which pin
        // IGridTopology.ResolveRelative to their exact behavior on square worlds.
    }

    /// <summary>
    /// Persisted state for a game map.
    /// </summary>
    [GenerateSerializer]
    public class MapState
    {
        [Id(0)] public string MapId { get; set; } = string.Empty;
        [Id(1)] public string WorldId { get; set; } = string.Empty;
        [Id(2)] public string MapName { get; set; } = string.Empty;
        [Id(3)] public WorldSize Size { get; set; } = new WorldSize();
        [Id(4)] public string GeneratorType { get; set; } = string.Empty;
        [Id(5)] public HashSet<string> PlayerIds { get; set; } = new HashSet<string>();
        [Id(6)] public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Reserved for phase 2 real <c>World</c> serialization. Phase 1 uses
        /// <see cref="Recipe"/> + ephemeral mutation loss on restart.
        /// </summary>
        [Id(7)] public byte[]? SerializedWorld { get; set; }

        /// <summary>
        /// Recipe captured at <c>InitializeAsync</c> time so the grain can produce
        /// deterministic snapshots and rehydrate <c>_world</c> on reactivation.
        /// </summary>
        [Id(8)] public WorldRecipe? Recipe { get; set; }

        /// <summary>Rolling combat analytics (P3-7 slice 2): monsters defeated on this map.</summary>
        [Id(9)] public int MonstersDefeated { get; set; }

        /// <summary>Rolling combat analytics: total damage players have dealt on this map.</summary>
        [Id(10)] public long TotalDamageDealt { get; set; }

        /// <summary>Per-world death/respawn rules captured at <c>InitializeAsync</c> time (engine
        /// gap-analysis §4.11 — see wire-death-respawn-live), so the grain can rehydrate
        /// <c>_deathPolicy</c> on reactivation without re-running InitializeAsync. Null means the
        /// world specified none — the grain falls back to <c>DeathPolicy.Default</c>.</summary>
        [Id(11)] public Aetherium.Model.Combat.DeathPolicy? DeathPolicy { get; set; }

        /// <summary>Per-world ability content captured at <c>InitializeAsync</c> time (engine
        /// gap-analysis §4.3 — see wire-abilities-live), so the grain can recompile its ability
        /// catalog and re-stamp resource pools on reactivation without re-running InitializeAsync.
        /// Null means the world specified no abilities.</summary>
        [Id(12)] public Aetherium.Model.Abilities.AbilityConfig? AbilityConfig { get; set; }

        /// <summary>Per-world character-progression content captured at <c>InitializeAsync</c> time
        /// (engine gap-analysis §4.4 — see wire-progression-live), so the grain can recompile its
        /// skill catalog/curves and re-stamp progression components on reactivation. Null means the
        /// world specified no progression.</summary>
        [Id(13)] public Aetherium.Model.Progression.ProgressionConfig? ProgressionConfig { get; set; }

        /// <summary>Per-world faction content captured at <c>InitializeAsync</c> time (engine
        /// gap-analysis §4.6 — see wire-factions-live), so the grain can recompile its faction
        /// registry/relations on reactivation. Null means the world specified no factions.</summary>
        [Id(14)] public Aetherium.Model.Factions.FactionConfig? FactionConfig { get; set; }

        /// <summary>Per-world content vocabulary captured at <c>InitializeAsync</c> time
        /// (add-content-definitions), so the grain can recompile its content catalog and re-skin
        /// data-driven creatures on reactivation. Null means legacy hardcoded content.</summary>
        [Id(15)] public Aetherium.Model.Content.ContentConfig? ContentConfig { get; set; }

        /// <summary>Per-world reactive logic captured at <c>InitializeAsync</c> time (add-eca-scripting),
        /// so the grain can recompile its rule runtime on reactivation. Null means no rules.</summary>
        [Id(16)] public Aetherium.Model.Eca.EcaConfig? EcaConfig { get; set; }

        /// <summary>The world's tiling captured at <c>InitializeAsync</c> time
        /// (docs/grid-topologies.md), so the grain can re-resolve its <c>World.Topology</c> on
        /// reactivation without re-running InitializeAsync. Null/empty means square — so any
        /// map persisted before topologies shipped reactivates as square correctly.</summary>
        [Id(17)] public string? Topology { get; set; }
    }
}


