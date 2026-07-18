## Why
Headless sessions (change `add-aetherctl-headless-driving`) let an operator place and drive a character, but every action is still a separate one-shot call (`tools test` / `agent step`), so ordering and timing live in the caller and nothing is reproducible. There is no server-side way to send an ordered sequence of actions to a character, and no way to drive several characters through a scripted scenario. This blocks the original goal — "script a series of actions to send to one or more characters" — and deterministic integration testing of emergent multi-character behavior.

## What Changes
- Add `IGameManagementGrain.ExecuteToolBatchAsync(sessionId, actions, stopOnError)` that runs an ordered list of tool invocations against one session in a single grain turn and returns per-step results. Because the management grain is a single-threaded singleton, the batch runs atomically with respect to other management calls, giving deterministic ordering.
- Add serializable DTOs `ScriptedActionDto { Tool, Args }` and `BatchActionResultDto { Index, Tool, Success, Message }`.
- Add `aetherctl agent script <sessionId> --file <actions.json> [--stop-on-error] [--json]` — sends a JSON action list to one session and reports per-step results.
- Add `aetherctl scenario run <scenario.json> [--concurrent] [--stop-on-error] [--json]` — a CLI-side fan-out that drives **multiple** characters, each with its own action script, over the same batch primitive (optionally creating the headless sessions first).
- No LLM, no branching/conditionals, no cross-character synchronization — those stay out of scope (see Non-Goals).

## Impact
- Builds directly on `add-aetherctl-headless-driving` (uses its headless sessions + `ExecuteToolAsync` path).
- Affected specs:
  - `game-management-grain` (ADDED: Batch Action Execution)
  - `aetherctl` (ADDED: Scripted Action Command, Multi-Character Scenario Command)
- Affected code:
  - `Aetherium.Server/Management/IGameManagementGrain.cs`, `GameManagementGrain.cs` — batch method reusing the existing single-action execution
  - `Aetherium.Model` — `ScriptedActionDto`, `BatchActionResultDto`
  - `Aetherctl/Commands/AgentCommands.cs` (add `script`), new `Aetherctl/Commands/ScenarioCommands.cs`, `Aetherctl/Program.cs`
  - `Aetherium.Test/*`, `Aetherctl.Test/*` — new tests
- Non-Goals (future follow-ups): an `AgentRunner` scripted policy; conditionals/branching or assertions inside scripts; cross-character synchronization/barriers; a persisted server-side scenario/replay object; runtime world-building, memory activation, and CLI telemetry (tracked separately).
