## Context
`ExecuteToolAsync(toolId, sessionId, args)` (`GameManagementGrain.cs`) already executes a single action against a session and mutates the ECS world. The management grain is a single-threaded Orleans singleton keyed `GLOBAL`, so any one grain call runs to completion before the next begins. There is no server-side primitive for an ordered sequence, and the `AgentRunner` only offers LLM or a hardcoded heuristic policy — no caller-supplied action list. Headless sessions now make client-less driving possible, so a batch primitive can be exercised end-to-end without a game client.

## Goals / Non-Goals
- Goals:
  - Deterministically run an ordered action sequence against one character and get per-step results.
  - Drive one or more characters from a single CLI invocation via script/scenario files.
  - Reuse the existing tool-execution path (no new action semantics).
- Non-Goals: LLM-driven scripts; branching/conditionals/loops or assertions inside scripts; cross-character synchronization or barriers; a persisted server-side scenario object; anything beyond the existing tool catalog.

## Decisions
- **Decision: the sequence primitive is a server-side batch, `ExecuteToolBatchAsync(sessionId, actions, stopOnError)`.** It iterates the actions calling the same internal path as `ExecuteToolAsync`, collecting a `BatchActionResultDto` per step. Because it runs inside one grain turn, no other management call can interleave against that session mid-batch — ordering is deterministic by construction.
  - Alternatives considered: (a) a `ScriptedPolicy` inside `AgentRunnerGrain` — rejected as coupling sequencing to the runner's perceive→act loop and telemetry; the batch is a cleaner, directly-testable primitive a runner could later call. (b) A pure CLI loop of `ExecuteToolAsync` calls — rejected: N network round-trips, and ordering/atomicity depend on the client rather than the server.
- **Decision: `stopOnError` controls partial-failure behavior.** Default `false` = run every step and report each result (best for test observability); `true` = halt at the first failed step and return results so far. Either way the return is the ordered list of attempted-step results.
- **Decision: multi-character orchestration lives in the CLI, not a server grain.** `scenario run` reads a file of `{ sessionId | (worldId + start), actions[] }` entries and fans out one `ExecuteToolBatchAsync` per session — sequential by default, `--concurrent` to overlap. Keeping the server primitive single-session avoids a stateful scenario grain and keeps determinism per character; cross-character timing is explicitly not guaranteed (a Non-Goal).
- **Decision: file formats are plain JSON.**
  - Action script (`agent script --file`): `[{ "tool": "move", "args": { "direction": "forward" } }, ...]`.
  - Scenario (`scenario run`): `{ "characters": [ { "sessionId": "...", "actions": [...] }, { "world": "...", "at": "x,y,z", "actions": [...] } ] }` — an entry may name an existing `sessionId` or ask the CLI to create a headless session first via `CreateHeadlessSessionAsync`.
- **Decision: reuse existing serialization patterns.** `ScriptedActionDto.Args` is `Dictionary<string, object>` (same shape `ExecuteToolAsync` already accepts across Orleans); DTOs are `[GenerateSerializer]`.

## Risks / Trade-offs
- **Long batches occupy the singleton grain turn** → keep batches bounded; document that very large scripts should be split. A cap with a clear error protects the grain.
- **`--concurrent` scenarios race on shared world state** (two characters mutating adjacent tiles) → that is inherent to concurrency and acceptable for driving/observation; determinism is only promised per-character, not across characters. Sequential is the default.
- **Args typing** (JSON numbers/bools → `object`) already flows through `ExecuteToolAsync`; no new coercion risk beyond what tools already handle.

## Migration Plan
Purely additive: one new grain method, two DTOs, two CLI commands. No existing signatures change. Rollback = remove the method/commands/DTOs.

## Open Questions
- Should `scenario run` support an optional per-step delay (to pace observation) or stay as fast as the grain allows? Leaning: add an optional `--delay-ms` on the CLI only (client-side pacing), server batch stays immediate.
- Batch size cap value (proposed: 1000 actions/batch) — configurable or constant? Leaning: constant with a clear over-cap error for now.
