using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Threading.Tasks;
using Aetherctl.Orleans;

namespace Aetherctl.Commands
{
	public static class SessionCommands
	{
		public static void AddToRoot(RootCommand root)
		{
			var sessionCmd = new Command("session", "Session management");

			var listCmd = new Command("list", "List all active game sessions");
			listCmd.SetHandler(async (InvocationContext ctx) =>
			{
				try
				{
					var parseResult = ctx.ParseResult;
					await using var factory = new OrleansClientFactory();
					var client = await factory.ConnectAsync();
					var mgmt = factory.GetGameManagement();
					var sessions = await mgmt.ListSessionsAsync();

					if (Common.IsJsonOutput(parseResult))
					{
						var output = new
						{
							success = true,
							count = sessions?.Count ?? 0,
							sessions = sessions?.Select(s => new
							{
								sessionId = s.SessionId,
								connectionId = s.ConnectionId,
								directionalVision = s.DirectionalVisionMode,
								fieldOfView = s.FieldOfViewDegrees,
								connectedAt = s.ConnectedAt
							}).ToArray() ?? Array.Empty<object>()
						};
						Common.WriteOutput(parseResult, output);
					}
					else
					{
						if (sessions == null || sessions.Count == 0)
						{
							Console.WriteLine("No active sessions");
							return;
						}

						Console.WriteLine($"Active sessions ({sessions.Count}):");
						foreach (var session in sessions)
						{
							Console.WriteLine($"  Session ID: {session.SessionId}");
							Console.WriteLine($"    Connection ID: {session.ConnectionId}");
							Console.WriteLine($"    Directional Vision: {(session.DirectionalVisionMode ? "ON" : "OFF")}");
							Console.WriteLine($"    FOV: {session.FieldOfViewDegrees}°");
							Console.WriteLine($"    Connected at: {session.ConnectedAt:yyyy-MM-dd HH:mm:ss}");
							Console.WriteLine();
						}
					}
				}
				catch (Exception ex)
				{
					Common.WriteError(ctx.ParseResult, $"Failed to list sessions: {ex.Message}");
				}
			});

			var closeCmd = new Command("close", "Terminate a session by ID");
			var sessionIdArg = new Argument<string>(name: "sessionId", description: "Session ID to terminate");
			closeCmd.AddArgument(sessionIdArg);
			closeCmd.SetHandler(async (InvocationContext ctx) =>
			{
				try
				{
					var parseResult = ctx.ParseResult;
					var sessionId = parseResult.GetValueForArgument(sessionIdArg);
					await using var factory = new OrleansClientFactory();
					var client = await factory.ConnectAsync();
					var mgmt = factory.GetGameManagement();
					var result = await mgmt.TerminateSessionAsync(sessionId);

					if (Common.IsJsonOutput(parseResult))
					{
						Common.WriteOutput(parseResult, new { success = result.Success, message = result.Message });
					}
					else
					{
						Console.WriteLine(result.Success ? "Session terminated" : $"Failed to terminate: {result.Message}");
					}

					if (!result.Success)
					{
						Environment.Exit(1);
					}
				}
				catch (Exception ex)
				{
					Common.WriteError(ctx.ParseResult, $"Failed to terminate session: {ex.Message}");
				}
			});

			var createCmd = new Command("create", "Create a new session (pending server support)");
			createCmd.SetHandler((InvocationContext ctx) =>
			{
				var parseResult = ctx.ParseResult;
				if (Common.IsJsonOutput(parseResult))
				{
					Common.WriteOutput(parseResult, new { success = false, error = "Session creation via CLI is not supported by the server yet." });
				}
				else
				{
					Console.Error.WriteLine("Session creation via CLI is not supported by the server yet.");
				}
				Environment.Exit(1);
			});

			sessionCmd.AddCommand(listCmd);
			sessionCmd.AddCommand(closeCmd);
			sessionCmd.AddCommand(createCmd);
			root.AddCommand(sessionCmd);
		}
	}
}

