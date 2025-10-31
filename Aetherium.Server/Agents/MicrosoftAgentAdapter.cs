using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents
{
    /// <summary>
    /// Minimal OpenAI-compatible chat adapter for LM Studio (phi-4) to drive agent actions.
    /// Produces a structured decision with an action and arguments.
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

        public async Task<LlmDecision> DecideAsync(string perceptionJson, CancellationToken ct)
        {
            await rateLimiter.WaitAsync(ct);
            try
            {
                // Throttle: ensure minimum interval between requests
                var now = DateTime.UtcNow;
                var timeSinceLastRequest = now - lastRequestTime;
                if (timeSinceLastRequest < minRequestInterval)
                {
                    await Task.Delay(minRequestInterval - timeSinceLastRequest, ct);
                }
                lastRequestTime = DateTime.UtcNow;

                Interlocked.Increment(ref requestCount);

                var systemPrompt = GetSystemPrompt();
                var userPrompt = BuildUserPrompt(perceptionJson);

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

                var json = JsonSerializer.Serialize(request);
                var req = new HttpRequestMessage(HttpMethod.Post, Combine(apiBase, "chat/completions"))
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                if (debug)
                    Console.WriteLine($"[AgentAdapter] Request #{requestCount}: {Truncate(perceptionJson, 200)}");

                LlmDecision? decision = null;
                try
                {
                    var resp = await httpClient.SendAsync(req, ct);
                    var respText = await resp.Content.ReadAsStringAsync(ct);

                    if (!resp.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref errorCount);
                        Console.WriteLine($"[AgentAdapter] HTTP {resp.StatusCode}: {Truncate(respText, 200)}");
                        decision = new LlmDecision { Action = "move", Args = new Dictionary<string, string> { ["direction"] = "F" } };
                    }
                    else
                    {
                        if (debug)
                            Console.WriteLine($"[AgentAdapter] Response: {Truncate(respText, 800)}");

                        decision = ParseDecisionFromResponse(respText);
                    }
                }
                catch (TaskCanceledException)
                {
                    Interlocked.Increment(ref errorCount);
                    Console.WriteLine($"[AgentAdapter] Request timeout");
                    decision = new LlmDecision { Action = "move", Args = new Dictionary<string, string> { ["direction"] = "F" } };
                }
                catch (HttpRequestException ex)
                {
                    Interlocked.Increment(ref errorCount);
                    Console.WriteLine($"[AgentAdapter] HTTP error: {ex.Message}");
                    decision = new LlmDecision { Action = "move", Args = new Dictionary<string, string> { ["direction"] = "F" } };
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref errorCount);
                    Console.WriteLine($"[AgentAdapter] Unexpected error: {ex.Message}");
                    decision = new LlmDecision { Action = "move", Args = new Dictionary<string, string> { ["direction"] = "F" } };
                }

                return decision ?? new LlmDecision { Action = "move", Args = new Dictionary<string, string> { ["direction"] = "F" } };
            }
            finally
            {
                rateLimiter.Release();
            }
        }

        private static string GetSystemPrompt()
        {
            return "You are an NPC navigator in a grid-based dungeon. You can move (F/L/R/B or N/E/S/W), pickup items by id, open/close doors by entity id, and use keys on doors. Always output a single JSON object with fields action and args. Examples: {\"action\":\"move\",\"args\":{\"direction\":\"F\"}} or {\"action\":\"pickup\",\"args\":{\"targetEntityId\":\"item:123\"}}. No extra text.";
        }

        private static string BuildUserPrompt(string perceptionJson)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Perception:");
            sb.AppendLine(perceptionJson);
            sb.AppendLine();
            sb.AppendLine("Available actions:");
            sb.AppendLine("- move {direction: F|L|R|B|N|E|S|W}");
            sb.AppendLine("- pickup {targetEntityId}");
            sb.AppendLine("- drop {itemEntityId}");
            sb.AppendLine("- open {targetEntityId}");
            sb.AppendLine("- close {targetEntityId}");
            sb.AppendLine("- use {itemEntityId, onEntityId}");
            sb.AppendLine("Goal: Find a key and open a locked door. Prefer safe exploration.");
            sb.AppendLine("Respond with strict JSON only.");
            return sb.ToString();
        }

        private static LlmDecision? ParseDecisionFromResponse(string response)
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



