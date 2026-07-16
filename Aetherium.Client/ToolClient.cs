using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Aetherium.Client.Contracts;

namespace Aetherium.Client
{
    /// <summary>Typed result of an attack, parsed from ExecuteTool("attack").Data.</summary>
    public sealed class AttackResult
    {
        public bool Success { get; internal set; }
        public string Message { get; internal set; } = string.Empty;
        public string? TargetId { get; internal set; }
        public int? Damage { get; internal set; }
        public int? RemainingHealth { get; internal set; }
        public bool Defeated { get; internal set; }
    }

    /// <summary>Typed result of a use, surfacing disambiguation options when the item has
    /// multiple usages.</summary>
    public sealed class UseResult
    {
        public bool Success { get; internal set; }
        public string Message { get; internal set; } = string.Empty;
        public List<UsageOptionDto> Options { get; internal set; } = new List<UsageOptionDto>();
    }

    /// <summary>
    /// Typed wrappers over the server's single action funnel, ExecuteTool(toolId, args) —
    /// games never build arg dictionaries. Movement is deliberately relative-only on the wire
    /// (the engine's fairness constraint: humans and AI agents act through the same embodied
    /// interface); the composite <see cref="MoveAsync"/> is the WASD bridge, issuing the same
    /// minimal rotate + forward pair any agent could.
    ///
    /// On every successful move the client advances the PerceptionStore's anchor by the
    /// world-axis delta it computed from the heading — exact on square topology (M0's scope);
    /// on hex/tri/h3 worlds the anchor is not advanced (relative rendering still works; stable
    /// anchoring for non-square tilings is a documented follow-up).
    /// </summary>
    public sealed class ToolClient
    {
        private readonly AetheriumConnection _connection;

        public ToolClient(AetheriumConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        // ---- raw funnel ----

        public Task<ToolExecutionResultDto> ExecuteToolAsync(string toolId, Dictionary<string, object> args)
            => _connection.InvokeAsync<ToolExecutionResultDto>("ExecuteTool", new object?[] { toolId, args });

        public Task<List<ToolInfoDto>> ListAvailableToolsAsync()
            => _connection.InvokeAsync<List<ToolInfoDto>>("ListAvailableTools", Array.Empty<object?>());

        // ---- movement ----

        public Task<ToolExecutionResultDto> MoveForwardAsync(int distance = 1) => MoveRelativeAsync("FORWARD", distance);
        public Task<ToolExecutionResultDto> MoveBackwardAsync(int distance = 1) => MoveRelativeAsync("BACKWARD", distance);
        public Task<ToolExecutionResultDto> MoveLeftAsync(int distance = 1) => MoveRelativeAsync("LEFT", distance);
        public Task<ToolExecutionResultDto> MoveRightAsync(int distance = 1) => MoveRelativeAsync("RIGHT", distance);

        private async Task<ToolExecutionResultDto> MoveRelativeAsync(string direction, int distance)
        {
            int headingAtCall = CurrentHeadingDegrees();
            var result = await ExecuteToolAsync("move", new Dictionary<string, object>
            {
                ["direction"] = direction,
                ["distance"] = distance,
            }).ConfigureAwait(false);

            if (result.Success)
                AdvanceAnchorForMove(headingAtCall, direction, distance);
            return result;
        }

        /// <summary>Rotate by ±90° (square-grid preset).</summary>
        public Task<ToolExecutionResultDto> RotateAsync(bool clockwise)
            => ExecuteToolAsync("rotate", new Dictionary<string, object> { ["clockwise"] = clockwise });

        /// <summary>Rotate by an explicit angle (positive = clockwise).</summary>
        public Task<ToolExecutionResultDto> RotateByAsync(int degrees)
            => ExecuteToolAsync("rotate", new Dictionary<string, object> { ["degrees"] = degrees });

        /// <summary>
        /// The composite WASD bridge: rotates to the compass heading (minimal single rotate),
        /// then steps forward — exactly the rotate+move pair any AI agent would issue.
        /// <see cref="WorldDirection.Up"/>/<see cref="WorldDirection.Down"/> map to changelevel.
        /// </summary>
        public async Task<ToolExecutionResultDto> MoveAsync(WorldDirection direction, int distance = 1)
        {
            if (direction == WorldDirection.Up) return await ChangeLevelAsync(+1).ConfigureAwait(false);
            if (direction == WorldDirection.Down) return await ChangeLevelAsync(-1).ConfigureAwait(false);

            int target = direction switch
            {
                WorldDirection.North => 0,
                WorldDirection.East => 90,
                WorldDirection.South => 180,
                WorldDirection.West => 270,
                _ => throw new ArgumentOutOfRangeException(nameof(direction)),
            };

            int current = CurrentHeadingDegrees();
            int delta = ((target - current) % 360 + 360) % 360;
            if (delta != 0)
            {
                int signed = delta > 180 ? delta - 360 : delta; // 270 → -90: one CCW beats three CW
                var rotate = await RotateByAsync(signed).ConfigureAwait(false);
                if (!rotate.Success)
                    return rotate;
            }

            var move = await ExecuteToolAsync("move", new Dictionary<string, object>
            {
                ["direction"] = "FORWARD",
                ["distance"] = distance,
            }).ConfigureAwait(false);

            if (move.Success)
                AdvanceAnchorForMove(target, "FORWARD", distance);
            return move;
        }

        public Task<ToolExecutionResultDto> ChangeLevelAsync(int delta)
            => ExecuteToolAsync("changelevel", new Dictionary<string, object> { ["delta"] = delta })
                .ContinueWith(t =>
                {
                    if (t.Result.Success)
                        _connection.Store.AdvanceAnchor(0, 0, delta);
                    return t.Result;
                }, TaskContinuationOptions.OnlyOnRanToCompletion);

        // ---- combat / interaction ----

        public async Task<AttackResult> AttackAsync(string targetEntityId)
        {
            var result = await ExecuteToolAsync("attack", new Dictionary<string, object>
            {
                ["targetEntityId"] = targetEntityId,
            }).ConfigureAwait(false);

            var attack = new AttackResult
            {
                Success = result.Success,
                Message = result.Message,
                TargetId = GetString(result.Data, "targetId") ?? targetEntityId,
                Damage = GetInt(result.Data, "damage"),
                RemainingHealth = GetInt(result.Data, "remainingHealth"),
                Defeated = GetBool(result.Data, "defeated") ?? false,
            };

            // Fold combat feedback into presentation state (last-known HP, kill marking).
            if (attack.Success)
                _connection.Store.NoteAttackResult(attack.TargetId!, attack.RemainingHealth, attack.Defeated);
            return attack;
        }

        public Task<ToolExecutionResultDto> PickupAsync(string targetEntityId)
            => ExecuteToolAsync("pickup", new Dictionary<string, object> { ["targetEntityId"] = targetEntityId });

        public Task<ToolExecutionResultDto> DropAsync(string itemEntityId)
            => ExecuteToolAsync("drop", new Dictionary<string, object> { ["itemEntityId"] = itemEntityId });

        public async Task<UseResult> UseAsync(string itemEntityId, string onEntityId, string? usageId = null)
        {
            var args = new Dictionary<string, object>
            {
                ["itemEntityId"] = itemEntityId,
                ["onEntityId"] = onEntityId,
            };
            if (usageId != null)
                args["usageId"] = usageId;

            var result = await ExecuteToolAsync("use", args).ConfigureAwait(false);
            return new UseResult
            {
                Success = result.Success,
                Message = result.Message,
                Options = ParseUsageOptions(result.Data),
            };
        }

        public Task<ToolExecutionResultDto> OpenAsync(string targetEntityId)
            => ExecuteToolAsync("open", new Dictionary<string, object> { ["targetEntityId"] = targetEntityId });

        public Task<ToolExecutionResultDto> CloseAsync(string targetEntityId)
            => ExecuteToolAsync("close", new Dictionary<string, object> { ["targetEntityId"] = targetEntityId });

        // ---- vision / lighting ----

        public Task<ToolExecutionResultDto> SetVisionModeAsync(VisionMode mode)
            => ExecuteToolAsync("setvisionmode", new Dictionary<string, object> { ["mode"] = mode.ToString() });

        public Task<ToolExecutionResultDto> SetLightingModeAsync(LightingMode mode)
            => ExecuteToolAsync("setlightingmode", new Dictionary<string, object> { ["mode"] = mode.ToString() });

        public Task<ToolExecutionResultDto> SetFieldOfViewAsync(int degrees)
            => ExecuteToolAsync("setfieldofview", new Dictionary<string, object> { ["degrees"] = degrees });

        public Task<ToolExecutionResultDto> ToggleDirectionalVisionAsync()
            => ExecuteToolAsync("toggledirectionalvision", new Dictionary<string, object>());

        // ---- anchoring helpers (square topology; see class doc) ----

        private int CurrentHeadingDegrees() => _connection.Store.LatestFrame?.HeadingDegrees ?? 0;

        private bool AnchoringIsExact()
        {
            var topology = _connection.Store.LatestFrame?.Topology;
            return string.IsNullOrEmpty(topology) || topology == "square";
        }

        private void AdvanceAnchorForMove(int headingDegrees, string relativeDirection, int distance)
        {
            if (!AnchoringIsExact())
                return;

            int offset = relativeDirection switch
            {
                "FORWARD" => 0,
                "RIGHT" => 90,
                "BACKWARD" => 180,
                "LEFT" => 270,
                _ => 0,
            };
            int effective = ((headingDegrees + offset) % 360 + 360) % 360;
            // Snap to the nearest cardinal (the server cardinalizes before stepping) —
            // engine axes: north = -Y, east = +X.
            var (dx, dy) = ((effective + 45) / 90 % 4) switch
            {
                0 => (0, -1),
                1 => (1, 0),
                2 => (0, 1),
                _ => (-1, 0),
            };
            _connection.Store.AdvanceAnchor(dx * distance, dy * distance, 0);
        }

        // ---- Data-dictionary readers: values arrive as JsonElement over the wire ----

        internal static string? GetString(Dictionary<string, object>? data, string key)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value is null)
                return null;
            return value is JsonElement element
                ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
                : value.ToString();
        }

        internal static int? GetInt(Dictionary<string, object>? data, string key)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value is null)
                return null;
            if (value is JsonElement element)
                return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var n)
                    ? n
                    : int.TryParse(element.ToString(), out var parsed) ? parsed : (int?)null;
            return value is int i ? i : int.TryParse(value.ToString(), out var p) ? p : (int?)null;
        }

        internal static bool? GetBool(Dictionary<string, object>? data, string key)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value is null)
                return null;
            if (value is JsonElement element)
                return element.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => bool.TryParse(element.ToString(), out var b) ? b : (bool?)null,
                };
            return value is bool direct ? direct : bool.TryParse(value.ToString(), out var parsed) ? parsed : (bool?)null;
        }

        internal static List<UsageOptionDto> ParseUsageOptions(Dictionary<string, object>? data)
        {
            var options = new List<UsageOptionDto>();
            if (data == null || !data.TryGetValue("options", out var raw) || raw is not JsonElement element
                || element.ValueKind != JsonValueKind.Array)
                return options;

            foreach (var entry in element.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    continue;
                options.Add(new UsageOptionDto
                {
                    UsageId = ReadProperty(entry, "usageId"),
                    Label = ReadProperty(entry, "label"),
                    Description = ReadProperty(entry, "description"),
                });
            }
            return options;
        }

        private static string ReadProperty(JsonElement obj, string name)
        {
            // Tool Data payloads are built server-side from anonymous dictionaries whose key
            // casing is the author's choice — accept both spellings.
            if (obj.TryGetProperty(name, out var value)
                || obj.TryGetProperty(char.ToUpperInvariant(name[0]) + name.Substring(1), out value))
                return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
            return string.Empty;
        }
    }
}
