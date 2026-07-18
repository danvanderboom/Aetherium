using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Threading.Tasks;
using Aetherctl.Orleans;

namespace Aetherctl.Commands
{
    /// <summary>
    /// Agent telemetry inspection: per-step snapshots, aggregated analysis, and failed-run
    /// replays recorded by AgentTelemetryGrain (see OpenSpec change add-aetherctl-telemetry).
    /// </summary>
    public static class TelemetryCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var telemetryCmd = new Command("telemetry", "Inspect agent telemetry");

            // snapshots
            var snapshotsCmd = new Command("snapshots", "List recent per-step performance snapshots for an agent");
            var snapAgentArg = new Argument<string>("agentId", "Agent ID");
            var snapLimitOpt = new Option<int?>("--limit", () => 20, "Maximum snapshots to return");
            snapshotsCmd.AddArgument(snapAgentArg);
            snapshotsCmd.AddOption(snapLimitOpt);
            snapshotsCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var agentId = parseResult.GetValueForArgument(snapAgentArg);
                    var limit = parseResult.GetValueForOption(snapLimitOpt);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var telemetry = factory.GetAgentTelemetry(agentId);
                    var snapshots = await telemetry.GetSnapshotsAsync(limit);

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            agentId,
                            count = snapshots.Count,
                            snapshots = snapshots.Select(s => new
                            {
                                step = s.StepNumber,
                                timestamp = s.Timestamp,
                                sessionId = s.SessionId,
                                actionType = s.ActionType,
                                actionSummary = s.ActionSummary,
                                succeeded = s.ActionSucceeded,
                                error = s.ErrorMessage,
                                decisionLatencyMs = s.DecisionLatencyMs,
                                perceptionComplexity = s.PerceptionComplexity
                            })
                        });
                    }
                    else
                    {
                        if (snapshots.Count == 0)
                        {
                            Console.WriteLine($"No telemetry snapshots for agent '{agentId}'.");
                            return;
                        }
                        Console.WriteLine($"Snapshots for {agentId} ({snapshots.Count}):");
                        foreach (var s in snapshots)
                            Console.WriteLine($"  #{s.StepNumber} {(s.ActionSucceeded ? "✓" : "✗")} {s.ActionType} ({s.DecisionLatencyMs}ms) {s.ActionSummary}{(s.ErrorMessage != null ? $" [{s.ErrorMessage}]" : "")}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to get snapshots: {ex.Message}");
                }
            });

            // analysis
            var analysisCmd = new Command("analysis", "Show aggregated performance analysis for an agent");
            var analysisAgentArg = new Argument<string>("agentId", "Agent ID");
            analysisCmd.AddArgument(analysisAgentArg);
            analysisCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var agentId = parseResult.GetValueForArgument(analysisAgentArg);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var telemetry = factory.GetAgentTelemetry(agentId);
                    var analysis = await telemetry.GetAnalysisAsync();

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            agentId = analysis.AgentId,
                            totalSteps = analysis.TotalSteps,
                            successful = analysis.TotalSuccessfulActions,
                            failed = analysis.TotalFailedActions,
                            successRate = analysis.SuccessRate,
                            avgDecisionLatencyMs = analysis.AverageDecisionLatencyMs,
                            avgPerceptionComplexity = analysis.AveragePerceptionComplexity,
                            weaknesses = analysis.IdentifiedWeaknesses,
                            recommendations = analysis.Recommendations
                        });
                    }
                    else
                    {
                        Console.WriteLine($"Analysis for {agentId}:");
                        Console.WriteLine($"  Steps: {analysis.TotalSteps} ({analysis.TotalSuccessfulActions} ok / {analysis.TotalFailedActions} failed)");
                        Console.WriteLine($"  Success rate: {analysis.SuccessRate:P1}");
                        Console.WriteLine($"  Avg decision latency: {analysis.AverageDecisionLatencyMs:F0}ms");
                        Console.WriteLine($"  Avg perception complexity: {analysis.AveragePerceptionComplexity:F0}");
                        foreach (var w in analysis.IdentifiedWeaknesses)
                            Console.WriteLine($"  Weakness: {w}");
                        foreach (var r in analysis.Recommendations)
                            Console.WriteLine($"  Recommendation: {r}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to get analysis: {ex.Message}");
                }
            });

            // replays (list ids)
            var replaysCmd = new Command("replays", "List failed-run replay IDs for an agent");
            var replaysAgentArg = new Argument<string>("agentId", "Agent ID");
            var replaysLimitOpt = new Option<int?>("--limit", () => 20, "Maximum replay IDs to return");
            replaysCmd.AddArgument(replaysAgentArg);
            replaysCmd.AddOption(replaysLimitOpt);
            replaysCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var agentId = parseResult.GetValueForArgument(replaysAgentArg);
                    var limit = parseResult.GetValueForOption(replaysLimitOpt);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var telemetry = factory.GetAgentTelemetry(agentId);
                    var ids = await telemetry.GetFailedRunIdsAsync(limit);

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new { success = true, agentId, count = ids.Count, replayIds = ids });
                    }
                    else
                    {
                        if (ids.Count == 0)
                        {
                            Console.WriteLine($"No failed-run replays for agent '{agentId}'.");
                            return;
                        }
                        Console.WriteLine($"Failed-run replays for {agentId} ({ids.Count}):");
                        foreach (var id in ids)
                            Console.WriteLine($"  {id}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to list replays: {ex.Message}");
                }
            });

            // replay (fetch one)
            var replayCmd = new Command("replay", "Fetch a stored failed-run replay (JSON)");
            var replayAgentArg = new Argument<string>("agentId", "Agent ID");
            var replayIdArg = new Argument<string>("replayId", "Replay ID");
            replayCmd.AddArgument(replayAgentArg);
            replayCmd.AddArgument(replayIdArg);
            replayCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var agentId = parseResult.GetValueForArgument(replayAgentArg);
                    var replayId = parseResult.GetValueForArgument(replayIdArg);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var telemetry = factory.GetAgentTelemetry(agentId);
                    var json = await telemetry.GetReplayAsync(replayId);

                    if (string.IsNullOrEmpty(json))
                    {
                        Common.WriteError(parseResult, $"Replay '{replayId}' not found for agent '{agentId}'.");
                        return;
                    }

                    // Replay payload is already JSON; emit as-is in both modes.
                    Console.WriteLine(json);
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to fetch replay: {ex.Message}");
                }
            });

            // clear
            var clearCmd = new Command("clear", "Clear all telemetry data for an agent");
            var clearAgentArg = new Argument<string>("agentId", "Agent ID");
            clearCmd.AddArgument(clearAgentArg);
            clearCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var agentId = parseResult.GetValueForArgument(clearAgentArg);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var telemetry = factory.GetAgentTelemetry(agentId);
                    await telemetry.ClearTelemetryAsync();

                    if (Common.IsJsonOutput(parseResult))
                        Common.WriteOutput(parseResult, new { success = true, agentId });
                    else
                        Common.WriteSuccess(parseResult, $"Cleared telemetry for {agentId}");
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to clear telemetry: {ex.Message}");
                }
            });

            telemetryCmd.AddCommand(snapshotsCmd);
            telemetryCmd.AddCommand(analysisCmd);
            telemetryCmd.AddCommand(replaysCmd);
            telemetryCmd.AddCommand(replayCmd);
            telemetryCmd.AddCommand(clearCmd);
            root.AddCommand(telemetryCmd);
        }
    }
}
