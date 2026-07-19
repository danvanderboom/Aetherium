# simulation Specification

## Purpose
TBD - created by archiving change feed-prompt-templates-to-agents. Update Purpose after archive.
## Requirements
### Requirement: Agent LLM decisions use editable prompt templates

When an agent decides its next action via the LLM path, its system prompt SHALL be sourced from an
editable prompt template (via `PromptRegistry`) with the agent's goal substituted into it, so agent
behavior can be tuned at runtime without recompiling. When no template/registry is available the
agent SHALL fall back to a built-in default prompt so decisions still function.

#### Scenario: System prompt rendered from a template
- **GIVEN** a prompt template registered under the configured name containing a `{{goal}}` placeholder
- **WHEN** the agent composes its LLM system prompt
- **THEN** the composed prompt is the template text with the agent's goal substituted for `{{goal}}`

#### Scenario: Fallback when no template is available
- **WHEN** no prompt registry is wired, or the configured template is absent
- **THEN** the agent composes its built-in default system prompt and decision-making still proceeds

#### Scenario: Placeholder substitution is safe
- **WHEN** a template is rendered with a set of variables
- **THEN** `{{name}}` placeholders are replaced case-insensitively (ignoring surrounding whitespace),
  and placeholders with no matching variable are left intact rather than blanked

