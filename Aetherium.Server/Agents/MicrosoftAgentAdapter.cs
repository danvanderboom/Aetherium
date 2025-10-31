using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Agents.Tools;

namespace Aetherium.Server.Agents
{
    /// <summary>
    /// Minimal OpenAI-compatible chat adapter for LM Studio (phi-4) to drive agent actions.
    /// Supports both OpenAI function calling and simple JSON formats.
    /// </summary>
    public class MicrosoftAgentAdapter
    {
        private readonly HttpClient httpClient;
        private readonly string apiBase;
        private readonly string apiKey;
        private readonly string model;
        private readonly bool debug;
        private readonly SemaphoreSlim rateLimiter;
        private DateTime lastRequestTime = DateTime.MinValue;
        private readonly TimeSpan minRequestInterval = TimeSpan.FromMilliseconds(100); // 10 requests/sec max
        private int requestCount = 0;
        private int errorCount = 0;

        public MicrosoftAgentAdapter(HttpClient? httpClient = null)
        {
            this.httpClient = httpClient ?? new HttpClient();
            this.httpClient.Timeout = TimeSpan.FromSeconds(10); // 10 second timeout
            apiBase = Environment.GetEnvironmentVariable("OPENAI_API_BASE") ?? "http://localhost:1234/v1";
            apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "lm-studio";
            model = Environment.GetEnvironmentVariable("AGENT_MODEL") ?? "phi-4";
            debug = string.Equals(Environment.GetEnvironmentVariable("AGENT_DEBUG"), "1", StringComparison.OrdinalIgnoreCase);
            
            var maxConcurrent = int.TryParse(Environment.GetEnvironmentVariable("AGENT_LLM_MAX_CONCURRENT") ?? "2", out var m) ? m : 2;
            rateLimiter = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        }

        /// <summary>
        /// Decides on an action using available tools (new version).
        /// Supports both OpenAI function calling and simple JSON formats.
        /// </summary>
        public async Task<LlmDecision> DecideAsync(string perceptionJson, IEnumerable<IAgentTool> availableTools, CancellationToken ct)
        {
            var toolsList = availableTools.ToList();
            
            // Check if model supports OpenAI function calling
            if (SupportsOpenAIFunctionCalling(model))
            {
                return await DecideWithFunctionCallingAsync(perceptionJson, toolsList, ct);
            }
            else
            {
                return await DecideWithSimpleFormatAsync(perceptionJson, toolsList, ct);
            }
        }

        /// <summary>
        /// Legacy version for backward compatibility (uses hardcoded tool descriptions).
        /// </summary>
        public async Task<LlmDecision> DecideAsync(string perceptionJson, CancellationToken ct)
        {
            // Use simple format with hardcoded tools
            return await DecideWithSimpleFormatAsync(perceptionJson, new List<IAgentTool>(), ct);
        }

        private bool SupportsOpenAIFunctionCalling(string modelName)
        {
            // GPT-4 and GPT-3.5-turbo support function calling
            // Most other models (including phi-4) don't yet
            return modelName.Contains("gpt-4", StringComparison.OrdinalIgnoreCase) ||
                   modelName.Contains("gpt-3.5-turbo", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<LlmDecision> DecideWithFunctionCallingAsync(string perceptionJson, List<IAgentTool> tools, CancellationToken ct)
        {
            await rateLimiter.WaitAsync(ct);
            try
            {
                await ThrottleAsync(ct);
                Interlocked.Increment(ref requestCount);

                var systemPrompt = "You are an NPC navigator in a grid-based dungeon. Use the provided tools to interact with the world.";
                var userPrompt = $"Perception:\n{perceptionJson}\n\nGoal: Find a key and open a locked door. Prefer safe exploration.";

                // Build OpenAI function calling format
                var toolDefs = tools.Select(tool => new
                {
                    type = "function",
                    function = new
                    {
                        name = tool.ToolId,
                        description = tool.Description,
                        parameters = tool.GetParameterSchema().ToOpenAIFormat()
                    }
                }).ToArray();

                var request = new
                {
                    model = model,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    tools = toolDefs,
                    tool_choice = "auto",
                    temperature = 0.2,
                    max_tokens = 128
                };

                return await SendRequestAndParse(request, ct, isFunction Calling: true);
            }
            finally
            {
                rateLimiter.Release();
            }
        }

        private async Task<LlmDecision> DecideWithSimpleFormatAsync(string perceptionJson, List<IAgentTool> tools, CancellationToken ct)
        {
            await rateLimiter.WaitAsync(ct);
            try
            {
                await ThrottleAsync(ct);
                Interlocked.Increment(ref requestCount);

                var systemPrompt = GetSystemPrompt();
                var userPrompt = BuildUserPrompt(perceptionJson, tools);

                var request = new
                {
                    model = model,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.2,
                    max_tokens = 128
                };

                return await SendRequestAndParse(request, ct, isFunctionCalling: false);
            }
            finally
            {
                rateLimiter.Release();
            }
        }

        private async Task ThrottleAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var timeSinceLastRequest = now - lastRequestTime;
            if (timeSinceLastRequest < minRequestInterval)
            {
                await Task.Delay(minRequestInterval - timeSinceLastRequest, ct);
            }
            lastRequestTime = DateTime.UtcNow;
        }

        private async Task<LlmDecision> SendRequestAndParse(object request, CancellationToken ct, bool isFunctionCalling)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                var req = new HttpRequestMessage(HttpMethod.Post, Combine(apiBase, "chat/completions"))
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                if (debug)
                    Console.WriteLine($"[AgentAdapter] Request #{requestCount}: {Truncate(json, 200)}");

                var resp = await httpClient.SendAsync(req, ct);
                var respText = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref errorCount);
                    Console.WriteLine($"[AgentAdapter] HTTP {resp.StatusCode}: {Truncate(respText, 200)}");
                    return CreateFallbackDecision();
                }

                if (debug)
                    Console.WriteLine($"[AgentAdapter] Response: {Truncate(respText, 800)}");

                var decision = isFunctionCalling 
                    ? ParseFunctionCallingResponse(respText)
                    : ParseSimpleFormatResponse(respText);
                
                return decision ?? CreateFallbackDecision();
            }
            catch (TaskCanceledException)
            {
                Interlocked.Increment(ref errorCount);
                Console.WriteLine($"[AgentAdapter] Request timeout");
                return CreateFallbackDecision();
            }
            catch (HttpRequestException ex)
            {
                Interlocked.Increment(ref errorCount);
                Console.WriteLine($"[AgentAdapter] HTTP error: {ex.Message}");
                return CreateFallbackDecision();
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errorCount);
                Console.WriteLine($"[AgentAdapter] Unexpected error: {ex.Message}");
                return CreateFallbackDecision();
            }
        }

        private static LlmDecision CreateFallbackDecision()
        {
            return new LlmDecision { Action = "move", Args = new Dictionary<string, string> { ["direction"] = "F" } };
        }

        private static string GetSystemPrompt()
        {
            return "You are an NPC navigator in a grid-based dungeon. You can move (F/L/R/B or N/E/S/W), pickup items by id, open/close doors by entity id, and use keys on doors. Always output a single JSON object with fields action and args. Examples: {\"action\":\"move\",\"args\":{\"direction\":\"F\"}} or {\"action\":\"pickup\",\"args\":{\"targetEntityId\":\"item:123\"}}. No extra text.";
        }

        private static string BuildUserPrompt(string perceptionJson, List<IAgentTool> tools)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Perception:");
            sb.AppendLine(perceptionJson);
            sb.AppendLine();
            
            if (tools.Count > 0)
            {
                sb.AppendLine("Available tools:");
                foreach (var tool in tools)
                {
                    var schema = tool.GetParameterSchema();
                    var paramsStr = schema.ToSimpleFormat();
                    sb.AppendLine($"- {tool.ToolId} ({paramsStr}): {tool.Description}");
                }
            }
            else
            {
                // Fallback to hardcoded list
                sb.AppendLine("Available actions:");
                sb.AppendLine("- move {direction: F|L|R|B|N|E/S|W}");
                sb.AppendLine("- pickup {targetEntityId}");
                sb.AppendLine("- drop {itemEntityId}");
                sb.AppendLine("- open {targetEntityId}");
                sb.AppendLine("- close {targetEntityId}");
                sb.AppendLine("- use {itemEntityId, onEntityId}");
            }
            
            sb.AppendLine("Goal: Find a key and open a locked door. Prefer safe exploration.");
            sb.AppendLine("Respond with strict JSON only.");
            return sb.ToString();
        }

        private static LlmDecision? ParseFunctionCallingResponse(string response)
        {
            try
            {
                using var doc = JsonDocument.Parse(response);
                var choice = doc.RootElement.GetProperty("choices")[0];
                var message = choice.GetProperty("message");
                
                // Check if there are tool calls
                if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
                {
                    var toolCall = toolCalls[0];
                    var function = toolCall.GetProperty("function");
                    var functionName = function.GetProperty("name").GetString();
                    var argumentsJson = function.GetProperty("arguments").GetString();
                    
                    if (string.IsNullOrWhiteSpace(functionName) || string.IsNullOrWhiteSpace(argumentsJson))
                        return null;
                    
                    var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
                    var stringArgs = args?.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.ValueKind == JsonValueKind.String ? kvp.Value.GetString() ?? "" : kvp.Value.ToString()
                    ) ?? new Dictionary<string, string>();
                    
                    return new LlmDecision { Action = functionName, Args = stringArgs };
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static LlmDecision? ParseSimpleFormatResponse(string response)
        {
            try
            {
                using var doc = JsonDocument.Parse(response);
                var content = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();
                    
                if (string.IsNullOrWhiteSpace(content)) return null;

                // Extract JSON object from content (some models wrap with text)
                var m = Regex.Match(content, @"\{[\n\r\s\S]*\}");
                var json = m.Success ? m.Value : content.Trim();

                var obj = JsonSerializer.Deserialize<LlmDecision>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (obj == null || string.IsNullOrWhiteSpace(obj.Action)) return null;
                obj.Args ??= new Dictionary<string, string>();
                return obj;
            }
            catch
            {
                return null;
            }
        }

        private static string Combine(string a, string b)
        {
            if (!a.EndsWith("/")) a += "/";
            return a + b.TrimStart('/');
        }

        private static string Truncate(string s, int max)
        {
            if (s.Length <= max) return s;
            return s.Substring(0, max) + "...";
        }
    }

    public class LlmDecision
    {
        public string Action { get; set; } = string.Empty;
        public Dictionary<string, string>? Args { get; set; }
    }
}



