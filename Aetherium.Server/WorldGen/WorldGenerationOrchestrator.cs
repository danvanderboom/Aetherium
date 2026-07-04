using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Coordinates world generation by executing a deterministic set of passes
    /// across the canonical phases: layout, theming, population, interactions, validation.
    /// </summary>
    public sealed class WorldGenerationOrchestrator
    {
        private readonly MapGeneratorRegistry _registry;
        private readonly List<IWorldGenerationPass> _passes;

        public WorldGenerationOrchestrator(MapGeneratorRegistry registry, IEnumerable<IWorldGenerationPass>? passes = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _passes = passes != null
                ? new List<IWorldGenerationPass>(passes)
                : new List<IWorldGenerationPass>();

            // Stable sort by (Phase, Name) — passes that share a phase execute in name order
            // rather than List.Sort's unstable quicksort order.
            _passes = _passes.OrderBy(p => p.Phase).ThenBy(p => p.Name, StringComparer.Ordinal).ToList();
        }

        public IReadOnlyList<IWorldGenerationPass> Passes => _passes;

        public void AddPass(IWorldGenerationPass pass)
        {
            if (pass == null)
                throw new ArgumentNullException(nameof(pass));

            _passes.Add(pass);
            _passes.Sort((a, b) =>
            {
                int c = a.Phase.CompareTo(b.Phase);
                return c != 0 ? c : StringComparer.Ordinal.Compare(a.Name, b.Name);
            });
        }

        public WorldGenerationResult Generate(WorldGenerationRequest request) =>
            Generate(request, CancellationToken.None);

        public WorldGenerationResult Generate(WorldGenerationRequest request, CancellationToken cancellationToken)
        {
            // Resolve the layout generator into a local — never mutate the caller's request,
            // which is often reused across retries, batch jobs, and tests.
            var resolvedLayout = string.IsNullOrWhiteSpace(request.LayoutGenerator)
                ? (request.Template == WorldGenerationTemplate.Dungeon ? "AdvancedDungeon" : "PerlinTerrain")
                : request.LayoutGenerator;

            var generatorParams = new Dictionary<string, string>(request.Parameters, StringComparer.OrdinalIgnoreCase);
            generatorParams["layoutGenerator"] = resolvedLayout;
            var generatorContext = new GeneratorContext(request.Width, request.Height, request.Seed)
            {
                Levels = Math.Max(1, request.Levels),
                GeneratorParams = generatorParams,
                NarrativeId = request.Narrative.NarrativeId,
                NarrativeConstraints = request.Narrative,
                GeneratorVersion = request.GeneratorVersion,
                FeatureRegistry = _registry
            };

            var pipelineContext = new WorldGenerationContext(request, generatorContext);
            string? abortedByPass = null;

            var phaseTimeout = request.PhaseTimeout;

            foreach (var pass in _passes)
            {
                if (!pass.SupportsTemplate(request.Template))
                    continue;

                if (cancellationToken.IsCancellationRequested)
                {
                    pipelineContext.AddError($"World generation cancelled before pass '{pass.Name}'.");
                    abortedByPass = pass.Name;
                    break;
                }

                int errorsBefore = pipelineContext.Errors.Count;

                var sw = Stopwatch.StartNew();
                bool timedOut = false;
                try
                {
                    if (phaseTimeout > TimeSpan.Zero)
                    {
                        timedOut = !ExecuteWithTimeout(pass, pipelineContext, phaseTimeout, cancellationToken);
                    }
                    else
                    {
                        pass.Execute(pipelineContext, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    pipelineContext.AddError($"Pass '{pass.Name}' was cancelled.");
                    abortedByPass = pass.Name;
                    sw.Stop();
                    break;
                }
                catch (Exception ex)
                {
                    pipelineContext.AddError($"Pass '{pass.Name}' threw {ex.GetType().Name}: {ex.Message}");
                    abortedByPass = pass.Name;
                    sw.Stop();
                    break;
                }
                sw.Stop();

                if (request.EnableMetrics && request.RecordTimings)
                {
                    generatorContext.Metrics.RecordPhaseDuration(pass.Phase.ToString(), sw.Elapsed.TotalMilliseconds);
                }

                if (timedOut)
                {
                    pipelineContext.AddError($"Pass '{pass.Name}' exceeded PhaseTimeout ({phaseTimeout.TotalMilliseconds:F0}ms).");
                    abortedByPass = pass.Name;
                    break;
                }

                if (pipelineContext.Errors.Count > errorsBefore)
                {
                    abortedByPass = pass.Name;
                    break;
                }
            }

            // If the pipeline ran clean but never produced a world, surface that explicitly
            // so callers don't NRE on a silent failure.
            if (pipelineContext.World == null && pipelineContext.Errors.Count == 0)
            {
                pipelineContext.AddError(
                    $"World generation completed without producing a world (layoutGenerator='{resolvedLayout}', template={request.Template}). " +
                    "Check that the layout generator is registered and that a pass assigned WorldGenerationContext.World.");
            }

            // Validation outcome is only meaningful if no pass aborted mid-pipeline; otherwise
            // validation may have been skipped or run against partial state.
            if (abortedByPass == null && pipelineContext.ValidationResult != null)
            {
                if (!pipelineContext.ValidationResult.Success)
                {
                    foreach (var err in pipelineContext.ValidationResult.Errors)
                    {
                        generatorContext.Metrics.AddValidationFailure(err);
                    }
                }
                else
                {
                    generatorContext.Metrics.ValidationPassed = true;
                }
            }

            generatorContext.Metrics.EffectiveSeed = generatorContext.EffectiveSeed;

            // Compute the difficulty profile from the effective parameters and the measured layout so
            // the difficulty implied by a curriculum stage or benchmark is observable on the result
            // (rather than being purely advisory input the generator ignored). Best-effort: a bad
            // profile calculation must never fail an otherwise-successful generation.
            try
            {
                generatorContext.Metrics.CalculateDifficultyProfile(
                    request.Width, request.Height, Math.Max(1, request.Levels), generatorParams);
            }
            catch
            {
                // Introspection only — swallow and continue.
            }

            return new WorldGenerationResult(
                pipelineContext.World,
                generatorContext.Metrics,
                pipelineContext.ValidationResult,
                pipelineContext.Errors,
                abortedByPass,
                generatorContext.EffectiveSeed);
        }

        /// <summary>
        /// Runs the pass on a background task with the linked CTS cancelled on timeout.
        /// Returns true if the pass completed before the timeout; false otherwise.
        /// </summary>
        private static bool ExecuteWithTimeout(
            IWorldGenerationPass pass,
            WorldGenerationContext context,
            TimeSpan timeout,
            CancellationToken outerToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
            var task = Task.Run(() => pass.Execute(context, cts.Token), cts.Token);

            if (task.Wait(timeout))
            {
                // Propagate any exception from the task (Wait + completed task surfaces the exception
                // wrapped in AggregateException; unwrap so the orchestrator's catch can see the real type).
                if (task.IsFaulted && task.Exception != null)
                {
                    var inner = task.Exception.Flatten().InnerException ?? task.Exception;
                    throw inner;
                }
                return true;
            }

            cts.Cancel();
            // We don't wait for the task to actually stop — uncooperative passes will leak,
            // but the pipeline must remain bounded by PhaseTimeout.
            return false;
        }
    }
}



