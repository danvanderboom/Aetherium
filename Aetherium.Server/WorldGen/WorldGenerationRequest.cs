using System;
using System.Collections.Generic;
using Aetherium.WorldGen.Hybrid;
using Aetherium.WorldGen.Training;
using Aetherium.Model.Training;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Describes a single world generation invocation, including target template,
    /// generator identifiers, dimensions, and narrative constraints.
    ///
    /// <para><b>Immutability contract:</b> once a <see cref="WorldGenerationRequest"/> is passed
    /// to <see cref="WorldGenerationOrchestrator.Generate"/>, the orchestrator treats it as
    /// read-only and never mutates any property. Callers may safely reuse the same request
    /// object across retries, batch runs, and tests. The only intended mutating operation is
    /// <see cref="ApplyCurriculumStage"/>, which is meant to be called by the caller
    /// <em>before</em> handing the request to the orchestrator.</para>
    /// </summary>
    public sealed class WorldGenerationRequest
    {
        public string LayoutGenerator { get; set; } = string.Empty;
        public string? OutdoorGenerator { get; set; }
            = null; // Optional override for outdoor template when layout differs.

        /// <summary>
        /// Grid tiling name ("square"/"hex"/"tri"/"h3"); null or empty ⇒ square. Threaded into
        /// <see cref="GeneratorContext.Topology"/> and used to gate passes via
        /// <see cref="IWorldGenerationPass.SupportsTopology"/>, so an H3 (sphere) world runs a
        /// sphere-native layout and skips square-grid-only passes (docs/h3-topology.md).
        /// </summary>
        public string? Topology { get; set; }
        public WorldGenerationTemplate Template { get; set; } = WorldGenerationTemplate.Dungeon;
        public int Width { get; set; } = 80;
        public int Height { get; set; } = 80;
        public int Levels { get; set; } = 1;
        public int? Seed { get; set; }
            = null;
        public string GeneratorVersion { get; set; } = "1.0.0";
        public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public NarrativeGenerationConstraints Narrative { get; set; } = new NarrativeGenerationConstraints();
        // Safety net against a hung pass, not a QoS bound. Passes normally finish in
        // milliseconds, but this is wall-clock time: on a loaded machine (parallel
        // test runs, CI) a healthy pass can stall past 2s on scheduling alone, which
        // failed real generations and snapshot hydrations intermittently. Keep the
        // margin generous; callers with hard latency needs can lower it per-request.
        public TimeSpan PhaseTimeout { get; set; } = TimeSpan.FromSeconds(15);
        public bool EnableMetrics { get; set; } = true;
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// When true, wall-clock phase durations are recorded in <see cref="GenerationMetrics.PhaseDurationsMs"/>
        /// and budget checks in the validation service will fire. Set to false in deterministic tests
        /// to prevent flaky failures caused by wall-clock variation. Defaults to true for production use.
        /// </summary>
        public bool RecordTimings { get; set; } = true;

        /// <summary>
        /// Optional hybrid anchors for mixed authored/procedural content.
        /// </summary>
        public HybridLayout? HybridAnchors { get; set; }

        /// <summary>
        /// Optional curriculum stage for training scenarios.
        /// </summary>
        public CurriculumStage? CurriculumStage { get; set; }

        /// <summary>
        /// Whether this is a training scenario that should enable heatmap collection.
        /// </summary>
        public bool IsTrainingMode { get; set; } = false;

        /// <summary>
        /// Optional agent ID for adaptive content generation.
        /// </summary>
        public string? AdaptiveAgentId { get; set; }

        /// <summary>
        /// Applies curriculum stage parameters to this request if stage is provided.
        /// </summary>
        public void ApplyCurriculumStage()
        {
            if (CurriculumStage == null)
                return;

            var stageParams = CurriculumStage.Parameters;
            Width = stageParams.Width;
            Height = stageParams.Height;
            Levels = stageParams.Levels;

            // Apply stage parameters to generator parameters
            Parameters["trapDensity"] = stageParams.TrapDensity.ToString("0.##");
            Parameters["enemyCount"] = stageParams.EnemyCount.ToString();
            Parameters["puzzleComplexity"] = stageParams.PuzzleComplexity.ToString("0.##");
            Parameters["keyLockChainDepth"] = stageParams.KeyLockChainDepth.ToString();
            Parameters["secretRoomDensity"] = stageParams.SecretRoomDensity.ToString("0.##");
            Parameters["minRooms"] = stageParams.MinRooms.ToString();
            Parameters["maxRooms"] = stageParams.MaxRooms.ToString();
            Parameters["minBranchingFactor"] = stageParams.MinBranchingFactor.ToString("0.##");
            Parameters["maxBranchingFactor"] = stageParams.MaxBranchingFactor.ToString("0.##");
            Parameters["resourceAvailability"] = stageParams.ResourceAvailability.ToString("0.##");
            Parameters["combatDifficulty"] = stageParams.CombatDifficulty.ToString("0.##");

            // Apply any additional parameters
            foreach (var kvp in stageParams.AdditionalParameters)
            {
                Parameters[kvp.Key] = kvp.Value;
            }

            IsTrainingMode = true;
        }
    }
}



