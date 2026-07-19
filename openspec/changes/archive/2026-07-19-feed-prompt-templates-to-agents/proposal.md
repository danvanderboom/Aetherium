# Feed prompt templates into LLM agent decisions (P3-5)

## Why

The agent LLM decision path (`MicrosoftAgentAdapter`) built its prompts from **hard-coded inline
strings** — the system persona, the action list, and even the goal ("Find a key and open a locked
door") were literals baked into the binary. Meanwhile a `PromptRegistry` already loads editable
`Prompts/*.md` templates and is wired into DI, but **nothing consumed it for decisions** and it had
no placeholder-substitution helper. So agent behavior could not be tuned without recompiling, and
the follow-up noted under P3-5 ("feed prompt templates into LLM decisions") was unaddressed.

## What Changes

- **`PromptRegistry` gains rendering:** a `Render(name, variables)` method (and a static
  `Substitute`) that fills `{{variable}}` placeholders (case-insensitive, whitespace-tolerant),
  leaving unknown placeholders intact and returning null when the template is absent.
- **`MicrosoftAgentAdapter` becomes template-driven:** an optional `PromptRegistry` + system
  template name + goal. Its system prompt is rendered from the template (with `{{goal}}` substituted)
  when a registry/template is available, and falls back to the previous built-in default otherwise.
  The goal is now a value (default preserved) rather than a hard-coded literal, and flows into both
  the simple-format and function-calling paths. The prompt builders are public so the composed
  prompt is inspectable/testable without a live LLM.
- **`AgentRunnerGrain` wires it up:** resolves the `PromptRegistry` from DI and constructs the
  adapter with it; the template name and goal are overridable via `AGENT_PROMPT_TEMPLATE` and
  `AGENT_GOAL`. When no registry is registered, behavior is byte-for-byte the old default.
- **A canonical editable template** `Prompts/agent_decision.md` ships (copied to output) with the
  capabilities, guidance, `{{goal}}` placeholder, and strict-JSON output contract — so operators
  can tune agent decisions at runtime (edit the file or `aetherctl prompts` + reload).

## Impact

- Affected specs: **simulation** (agent decision prompts sourced from editable templates).
- Affected code: `Aetherium.Server/Agents/PromptRegistry.cs`,
  `Aetherium.Server/Agents/MicrosoftAgentAdapter.cs`,
  `Aetherium.Server/Agents/AgentRunnerGrain.cs`, new `Aetherium.Server/Prompts/agent_decision.md`.
- No behavior change when no `PromptRegistry` is registered (existing agent tests unaffected). No
  live LLM required for the new tests — prompt composition is pure.
- Tests: `AgentPromptTemplateTests` (placeholder substitution; render-missing → null; adapter uses
  rendered template; falls back to default without registry/template; user prompt carries
  perception + goal + actions).
