## 1. Server: batch action execution
- [x] 1.1 Add `ScriptedActionDto { Tool, Args }` and `BatchActionResultDto { Index, Tool, Success, Message }` (`[GenerateSerializer]`) in `Aetherium.Model`
- [x] 1.2 Add `IGameManagementGrain.ExecuteToolBatchAsync(string sessionId, List<ScriptedActionDto> actions, bool stopOnError)` returning `List<BatchActionResultDto>`
- [x] 1.3 Implement it by iterating actions through the existing `ExecuteToolAsync` path; collect one result per attempted step
- [x] 1.4 Honor `stopOnError` (halt after first failure, returning results so far); default runs all steps
- [x] 1.5 Validate: unknown session → single failure result; empty action list → empty result; cap oversized batches (>1000) with a clear error

## 2. CLI: scripted action command
- [x] 2.1 Add `aetherctl agent script <sessionId> --file <actions.json> [--stop-on-error] [--json]`
- [x] 2.2 Parse the JSON action array (`[{ "tool", "args" }]`) into `ScriptedActionDto`s (arg values normalized to primitives) and call `ExecuteToolBatchAsync`
- [x] 2.3 Print per-step results (and full JSON with `--json`); non-zero exit if any step failed

## 3. CLI: multi-character scenario command
- [x] 3.1 Add `Aetherctl/Commands/ScenarioCommands.cs`: `aetherctl scenario run <scenario.json> [--concurrent] [--stop-on-error] [--delay-ms N] [--json]`
- [x] 3.2 For each character entry, resolve/create its session (existing `sessionId`, or create headless via `CreateHeadlessSessionAsync` from `world`/`at`) then run its action batch
- [x] 3.3 Fan out sequentially by default, concurrently with `--concurrent`; aggregate per-character results
- [x] 3.4 Register `ScenarioCommands` in `Program.cs`

## 4. Tests
- [x] 4.1 Server: batch of actions against a headless session runs in order and returns one result per step
- [x] 4.2 Server: `stopOnError=true` halts at the first failing step; `false` runs all and reports each
- [x] 4.3 Server: unknown session → failure result; empty list → empty results; over-cap batch → error
- [x] 4.4 Server: a batch changes observable state (toggledirectionalvision then perception reflects it)
- [x] 4.5 CLI (`Aetherctl.Test`): `agent script` and `scenario run` structural coverage + action-file parsing/normalization
- [x] 4.6 Multi-character: two headless characters in one world, each driven by its own batch, both report results (grain-level; the CLI `scenario run` fan-out is a thin loop over this)

## 5. Docs
- [x] 5.1 Update `docs/agents/README.md` with the `agent script` / `scenario run` flow and file formats
- [x] 5.2 Note the new commands in `TOOL_SYSTEM_STATUS.md` and update the remaining follow-up list
