## Why
Items should support multiple usage modes depending on context. For example, a crowbar might be used to force open doors OR pry open crates. A key might unlock doors OR activate mechanisms. When multiple usage options are available, the player/agent must be able to choose which one to use. This extends the existing single-use tool system to support multi-use tools with context-gated usage options.

## What Changes
- Add multi-use tool capability where items can have multiple usage options
- Support context-gated usage options (e.g., "only works on doors", "only in forests", "only during combat")
- Implement proactive disambiguation (affordances list usage options) and reactive disambiguation (server returns options when usage is ambiguous)
- Extend InteractionSystem with GetUseOptions() and TryUseWithMode() methods
- Add ContextEvaluator to compute game context tags
- Update perception to populate UsageOptions in affordances
- Update console client to display and handle usage choices

## Impact
- Affected specs: interaction (new capability), perception (MODIFIED to include UsageOptions), client-server-communication (MODIFIED to support usageId parameter)
- Affected code: Aetherium.Server (InteractionSystem, ContextEvaluator, PerceptionService, UseTool), Aetherium.Model (ToolDtos, AffordanceDto, InteractionResultDto), Aetherium.Console (ClientConsoleDungeonGame, GameClient)

