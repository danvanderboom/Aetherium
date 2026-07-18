using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aetherctl.Orleans;
using Aetherium.Model;

namespace Aetherctl.Commands
{
    /// <summary>
    /// Multi-character scenario driving. A scenario file lists characters, each with its own
    /// action script; the CLI resolves/creates a session per character and fans out one batch
    /// per session (sequential by default, concurrent with --concurrent).
    /// </summary>
    public static class ScenarioCommands
    {
        private sealed class CharacterPlan
        {
            public string? SessionId;
            public string? World;
            public string? At;
            public List<ScriptedActionDto> Actions = new List<ScriptedActionDto>();
        }

        private sealed class CharacterOutcome
        {
            public string SessionId = string.Empty;
            public string? Error;
            public List<BatchActionResultDto> Steps = new List<BatchActionResultDto>();
        }

        public static void AddToRoot(RootCommand root)
        {
            var scenarioCmd = new Command("scenario", "Run multi-character action scenarios");

            var runCmd = new Command("run", "Drive one or more characters from a scenario JSON file");
            var fileArg = new Argument<string>("file", "Path to scenario JSON file");
            var concurrentOpt = new Option<bool>("--concurrent", "Drive characters concurrently instead of sequentially");
            var stopOpt = new Option<bool>("--stop-on-error", "Halt each character's script at its first failing step");
            var delayOpt = new Option<int>("--delay-ms", () => 0, "Delay between characters in sequential mode (ms)");
            runCmd.AddArgument(fileArg);
            runCmd.AddOption(concurrentOpt);
            runCmd.AddOption(stopOpt);
            runCmd.AddOption(delayOpt);
            runCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var file = parseResult.GetValueForArgument(fileArg);
                    var concurrent = parseResult.GetValueForOption(concurrentOpt);
                    var stopOnError = parseResult.GetValueForOption(stopOpt);
                    var delayMs = parseResult.GetValueForOption(delayOpt);

                    if (!File.Exists(file))
                    {
                        Common.WriteError(parseResult, $"Scenario file not found: {file}");
                        Environment.Exit(1);
                        return;
                    }

                    List<CharacterPlan> plans;
                    try
                    {
                        plans = ParseScenario(await File.ReadAllTextAsync(file));
                    }
                    catch (JsonException ex)
                    {
                        Common.WriteError(parseResult, $"Invalid scenario JSON: {ex.Message}");
                        Environment.Exit(1);
                        return;
                    }

                    if (plans.Count == 0)
                    {
                        Common.WriteError(parseResult, "Scenario contains no characters.");
                        Environment.Exit(1);
                        return;
                    }

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();

                    // Resolve or create a session for each character up front.
                    var resolved = new List<(CharacterPlan plan, string sessionId, string? error)>();
                    foreach (var plan in plans)
                    {
                        if (!string.IsNullOrEmpty(plan.SessionId))
                        {
                            resolved.Add((plan, plan.SessionId!, null));
                        }
                        else if (!string.IsNullOrEmpty(plan.World))
                        {
                            var (x, y, z) = ParseAt(plan.At);
                            var res = await mgmt.CreateHeadlessSessionAsync(plan.World!, x, y, z, null);
                            if (res.Success)
                                resolved.Add((plan, res.SessionId!, null));
                            else
                                resolved.Add((plan, string.Empty, res.Message));
                        }
                        else
                        {
                            resolved.Add((plan, string.Empty, "character entry has neither 'sessionId' nor 'world'"));
                        }
                    }

                    async Task<CharacterOutcome> RunOne((CharacterPlan plan, string sessionId, string? error) r)
                    {
                        if (r.error != null)
                            return new CharacterOutcome { SessionId = r.sessionId, Error = r.error };
                        var results = await mgmt.ExecuteToolBatchAsync(r.sessionId, r.plan.Actions, stopOnError);
                        return new CharacterOutcome { SessionId = r.sessionId, Steps = results };
                    }

                    var outcomes = new List<CharacterOutcome>();
                    if (concurrent)
                    {
                        outcomes.AddRange(await Task.WhenAll(resolved.Select(RunOne)));
                    }
                    else
                    {
                        for (int i = 0; i < resolved.Count; i++)
                        {
                            outcomes.Add(await RunOne(resolved[i]));
                            if (delayMs > 0 && i < resolved.Count - 1)
                                await Task.Delay(delayMs);
                        }
                    }

                    var anyFailed = outcomes.Any(o => o.Error != null || o.Steps.Count == 0 || o.Steps.Any(s => !s.Success));

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = !anyFailed,
                            characters = outcomes.Select(o => new
                            {
                                sessionId = o.SessionId,
                                error = o.Error,
                                steps = o.Steps.Select(s => new { index = s.Index, tool = s.Tool, success = s.Success, message = s.Message })
                            })
                        });
                    }
                    else
                    {
                        Console.WriteLine($"Ran scenario with {outcomes.Count} character(s):");
                        foreach (var o in outcomes)
                        {
                            if (o.Error != null)
                            {
                                Console.WriteLine($"  {(string.IsNullOrEmpty(o.SessionId) ? "(unresolved)" : o.SessionId)} ✗ {o.Error}");
                                continue;
                            }
                            var ok = o.Steps.Count(s => s.Success);
                            Console.WriteLine($"  {o.SessionId}: {ok}/{o.Steps.Count} steps ok");
                        }
                    }

                    if (anyFailed)
                        Environment.Exit(1);
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Failed to run scenario: {ex.Message}");
                    Environment.Exit(1);
                }
            });

            scenarioCmd.AddCommand(runCmd);
            root.AddCommand(scenarioCmd);
        }

        private static List<CharacterPlan> ParseScenario(string json)
        {
            var plans = new List<CharacterPlan>();
            using var doc = JsonDocument.Parse(json);
            if (!ActionScript.TryProp(doc.RootElement, "characters", out var chars) || chars.ValueKind != JsonValueKind.Array)
                return plans;

            foreach (var el in chars.EnumerateArray())
            {
                var plan = new CharacterPlan();
                if (ActionScript.TryProp(el, "sessionId", out var sid)) plan.SessionId = sid.GetString();
                if (ActionScript.TryProp(el, "world", out var w)) plan.World = w.GetString();
                if (ActionScript.TryProp(el, "at", out var at)) plan.At = at.GetString();
                if (ActionScript.TryProp(el, "actions", out var acts)) plan.Actions = ActionScript.ParseActions(acts);
                plans.Add(plan);
            }
            return plans;
        }

        private static (int?, int?, int?) ParseAt(string? at)
        {
            if (string.IsNullOrWhiteSpace(at)) return (null, null, null);
            var parts = at.Split(',');
            if (parts.Length < 2) return (null, null, null);
            int? x = int.TryParse(parts[0].Trim(), out var px) ? px : (int?)null;
            int? y = int.TryParse(parts[1].Trim(), out var py) ? py : (int?)null;
            int? z = parts.Length >= 3 && int.TryParse(parts[2].Trim(), out var pz) ? pz : (int?)null;
            return (x, y, z);
        }
    }
}
