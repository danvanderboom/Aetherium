# Tasks

## 1. Prompt rendering
- [x] 1.1 Add `PromptRegistry.Render(name, vars)` + static `Substitute` ({{var}} placeholders)

## 2. Template-driven adapter
- [x] 2.1 `MicrosoftAgentAdapter` takes optional registry + system template name + goal
- [x] 2.2 `BuildSystemPrompt()` renders the template (fallback to built-in default); goal is a value
- [x] 2.3 Both simple-format and function-calling paths use the composed system prompt + goal

## 3. Grain wiring + template
- [x] 3.1 `AgentRunnerGrain` resolves `PromptRegistry`; `AGENT_PROMPT_TEMPLATE`/`AGENT_GOAL` overrides
- [x] 3.2 Ship `Prompts/agent_decision.md` (copied to output via existing csproj glob)

## 4. Tests & verification
- [x] 4.1 `AgentPromptTemplateTests` — substitution, render-missing, adapter template vs fallback, user prompt
- [x] 4.2 Full `Aetherium.sln` build (0 errors) + full `Aetherium.Test` suite green
