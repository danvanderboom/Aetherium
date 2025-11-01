using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using Aetherctl.Orleans;

namespace Aetherctl.Commands
{
    public static class AgentCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var agentCmd = new Command("agent", "Agent runner orchestration");

            var attachCmd = new Command("attach", "Attach an agent runner to a session");
            var attachSessionArg = new Argument<string>("sessionId", "Session ID to attach to");
            var attachAgentOpt = new Option<string>("--agent", () => "agent-1", "Agent identifier");
            var attachRunnerOpt = new Option<string>("--runner", () => "runner-1", "Runner grain id");
            attachCmd.AddArgument(attachSessionArg);
            attachCmd.AddOption(attachAgentOpt);
            attachCmd.AddOption(attachRunnerOpt);
            attachCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var sessionId = parseResult.GetValueForArgument(attachSessionArg);
                    var agent = parseResult.GetValueForOption(attachAgentOpt);
                    var runner = parseResult.GetValueForOption(attachRunnerOpt);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var r = factory.GetAgentRunner(runner);
                    var ok = await r.AttachAsync(sessionId, agent);

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = ok,
                            runnerId = runner,
                            sessionId = sessionId,
                            agentId = agent
                        });
                    }
                    else
                    {
                        Console.WriteLine(ok ? $"✓ Attached {runner} to {sessionId} as {agent}" : $"✗ Failed to attach {runner} to {sessionId}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Attach failed: {ex.Message}");
                }
            });

            var stepCmd = new Command("step", "Execute one agent step");
            var stepRunnerArg = new Argument<string>("runnerId", "Runner grain id");
            stepCmd.AddArgument(stepRunnerArg);
            stepCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var runnerId = parseResult.GetValueForArgument(stepRunnerArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var r = factory.GetAgentRunner(runnerId);
                    await r.StepAsync();
                    var s = await r.GetStatusAsync();

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            runnerId = runnerId,
                            steps = s.Steps,
                            lastAction = s.LastAction,
                            lastResult = s.LastResult
                        });
                    }
                    else
                    {
                        Console.WriteLine($"✓ Step done. Steps={s.Steps} LastAction={s.LastAction} Result={s.LastResult}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Step failed: {ex.Message}");
                }
            });

            var runCmd = new Command("run", "Run agent loop");
            var runRunnerArg = new Argument<string>("runnerId", "Runner grain id");
            var runMaxOpt = new Option<int?>("--max-steps", () => 10, "Max steps");
            var runDelayOpt = new Option<int>("--delay", () => 200, "Delay ms between steps");
            runCmd.AddArgument(runRunnerArg);
            runCmd.AddOption(runMaxOpt);
            runCmd.AddOption(runDelayOpt);
            runCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var runnerId = parseResult.GetValueForArgument(runRunnerArg);
                    var maxSteps = parseResult.GetValueForOption(runMaxOpt);
                    var delay = parseResult.GetValueForOption(runDelayOpt);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var r = factory.GetAgentRunner(runnerId);
                    await r.RunAsync(maxSteps, delay);

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            runnerId = runnerId,
                            maxSteps = maxSteps,
                            delay = delay
                        });
                    }
                    else
                    {
                        Console.WriteLine($"✓ Running {runnerId} (maxSteps={maxSteps}, delay={delay}ms)");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Run failed: {ex.Message}");
                }
            });

            var stopCmd = new Command("stop", "Stop agent loop");
            var stopRunnerArg = new Argument<string>("runnerId", "Runner grain id");
            stopCmd.AddArgument(stopRunnerArg);
            stopCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var runnerId = parseResult.GetValueForArgument(stopRunnerArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var r = factory.GetAgentRunner(runnerId);
                    await r.StopAsync();

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new { success = true, runnerId = runnerId });
                    }
                    else
                    {
                        Console.WriteLine($"✓ Stopped {runnerId}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Stop failed: {ex.Message}");
                }
            });

            var statusCmd = new Command("status", "Show runner status");
            var statusRunnerArg = new Argument<string>("runnerId", "Runner grain id");
            statusCmd.AddArgument(statusRunnerArg);
            statusCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var runnerId = parseResult.GetValueForArgument(statusRunnerArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var r = factory.GetAgentRunner(runnerId);
                    var s = await r.GetStatusAsync();

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            runnerId = runnerId,
                            sessionId = s.SessionId,
                            agentId = s.AgentId,
                            isRunning = s.IsRunning,
                            steps = s.Steps,
                            lastAction = s.LastAction,
                            lastResult = s.LastResult
                        });
                    }
                    else
                    {
                        Console.WriteLine($"Runner {runnerId}");
                        Console.WriteLine($"  Session: {s.SessionId}");
                        Console.WriteLine($"  Agent:   {s.AgentId}");
                        Console.WriteLine($"  Running: {s.IsRunning}");
                        Console.WriteLine($"  Steps:   {s.Steps}");
                        Console.WriteLine($"  Last:    {s.LastAction} -> {s.LastResult}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Status failed: {ex.Message}");
                }
            });

            agentCmd.AddCommand(attachCmd);
            agentCmd.AddCommand(stepCmd);
            agentCmd.AddCommand(runCmd);
            agentCmd.AddCommand(stopCmd);
            agentCmd.AddCommand(statusCmd);
            root.AddCommand(agentCmd);
        }
    }
}

