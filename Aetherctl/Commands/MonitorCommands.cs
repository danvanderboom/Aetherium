using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aetherctl.Commands
{
    public static class MonitorCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var monitorCmd = new Command("monitor", "Monitor game state via WebSocket");
            var serverUrlOpt = new Option<string>("--server-url", () => "ws://localhost:5001/monitor", "WebSocket server URL");
            var asciiOpt = new Option<bool>("--ascii", "Display ASCII map");
            var saveOpt = new Option<string?>("--save", "Save frames to directory");
            var verboseOpt = new Option<bool>("--verbose", "Enable verbose output");
            monitorCmd.AddOption(serverUrlOpt);
            monitorCmd.AddOption(asciiOpt);
            monitorCmd.AddOption(saveOpt);
            monitorCmd.AddOption(verboseOpt);
            monitorCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var serverUrl = parseResult.GetValueForOption(serverUrlOpt);
                    var displayAscii = parseResult.GetValueForOption(asciiOpt);
                    var saveDir = parseResult.GetValueForOption(saveOpt);
                    var verbose = parseResult.GetValueForOption(verboseOpt) || Common.IsVerbose(parseResult);
                    var isJson = Common.IsJsonOutput(parseResult);

                    if (!string.IsNullOrEmpty(saveDir) && !Directory.Exists(saveDir))
                        Directory.CreateDirectory(saveDir);

                    using var ws = new ClientWebSocket();
                    var uri = new Uri(serverUrl);
                    await ws.ConnectAsync(uri, CancellationToken.None);

                    if (ws.State == WebSocketState.Open)
                    {
                        if (!isJson && !Common.IsQuiet(parseResult))
                            Console.WriteLine("Connected successfully!");

                        var buffer = new byte[65536];
                        var frameCount = 0;

                        while (ws.State == WebSocketState.Open)
                        {
                            var segment = new ArraySegment<byte>(buffer);
                            var result = await ws.ReceiveAsync(segment, CancellationToken.None);

                            if (result.MessageType == WebSocketMessageType.Close)
                                break;

                            if (result.MessageType == WebSocketMessageType.Text)
                            {
                                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                                var json = JsonSerializer.Deserialize<JsonElement>(message);

                                if (json.TryGetProperty("type", out var type) && type.GetString() == "frame")
                                {
                                    frameCount++;
                                    if (json.TryGetProperty("data", out var data))
                                    {
                                        if (isJson)
                                        {
                                            Common.WriteOutput(parseResult, JsonSerializer.Deserialize<object>(message));
                                        }
                                        else
                                        {
                                            if (!Common.IsQuiet(parseResult))
                                            {
                                                Console.WriteLine($"────────────────────────────────────────────────────────────");
                                                Console.WriteLine($"Frame #{(data.TryGetProperty("frameNumber", out var fn) ? fn.GetInt32() : frameCount)}");

                                                if (data.TryGetProperty("rawPerception", out var perception))
                                                {
                                                    if (perception.TryGetProperty("playerLocation", out var loc))
                                                        Console.WriteLine($"  Player Location: ({(loc.TryGetProperty("x", out var x) ? x.GetInt32() : 0)}, {(loc.TryGetProperty("y", out var y) ? y.GetInt32() : 0)}, {(loc.TryGetProperty("z", out var z) ? z.GetInt32() : 0)})");
                                                }

                                                if (displayAscii && data.TryGetProperty("asciiMap", out var asciiMap))
                                                {
                                                    if (asciiMap.TryGetProperty("tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array)
                                                    {
                                                        Console.WriteLine("\n  Map:");
                                                        foreach (var row in tiles.EnumerateArray())
                                                        {
                                                            var rowStr = "  │";
                                                            if (row.ValueKind == JsonValueKind.Array)
                                                            {
                                                                foreach (var tile in row.EnumerateArray())
                                                                    rowStr += tile.GetString() ?? " ";
                                                            }
                                                            rowStr += "│";
                                                            Console.WriteLine(rowStr);
                                                        }
                                                    }
                                                }
                                            }

                                            if (!string.IsNullOrEmpty(saveDir))
                                            {
                                                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                                                var filename = Path.Combine(saveDir, $"frame_{frameCount}_{timestamp}.json");
                                                await File.WriteAllTextAsync(filename, message);
                                                if (verbose)
                                                    Console.WriteLine($"  Saved to: {filename}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error monitoring: {ex.Message}");
                }
            });

            root.AddCommand(monitorCmd);
        }
    }
}

