using System.Collections.Generic;
using System.Text.Json;
using Aetherium.Model;

namespace Aetherctl.Commands
{
    /// <summary>
    /// Parses JSON action scripts and scenario files into <see cref="ScriptedActionDto"/> lists.
    /// Argument values are normalized to Orleans-friendly primitives (string/long/double/bool).
    /// </summary>
    public static class ActionScript
    {
        /// <summary>
        /// Parses a JSON array of <c>{ "tool", "args" }</c> objects. Property names are matched
        /// case-insensitively (lower- and PascalCase).
        /// </summary>
        public static List<ScriptedActionDto> ParseActions(JsonElement arrayElement)
        {
            var actions = new List<ScriptedActionDto>();
            if (arrayElement.ValueKind != JsonValueKind.Array)
                return actions;

            foreach (var el in arrayElement.EnumerateArray())
            {
                var tool = TryProp(el, "tool", out var t) ? (t.GetString() ?? string.Empty) : string.Empty;
                var args = new Dictionary<string, object>();
                if (TryProp(el, "args", out var a) && a.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in a.EnumerateObject())
                        args[p.Name] = ToObject(p.Value);
                }
                actions.Add(new ScriptedActionDto { Tool = tool, Args = args });
            }
            return actions;
        }

        public static bool TryProp(JsonElement el, string name, out JsonElement value)
        {
            if (el.ValueKind == JsonValueKind.Object)
            {
                if (el.TryGetProperty(name, out value))
                    return true;
                var pascal = char.ToUpperInvariant(name[0]) + name.Substring(1);
                if (el.TryGetProperty(pascal, out value))
                    return true;
            }
            value = default;
            return false;
        }

        public static object ToObject(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.String => e.GetString() ?? string.Empty,
            JsonValueKind.Number => e.TryGetInt64(out var l) ? (object)l : e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => string.Empty,
            _ => e.GetRawText()
        };
    }
}
