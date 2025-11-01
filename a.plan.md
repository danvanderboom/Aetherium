<!-- 01917720-8957-4aa3-9cbe-6a4fff43acef 9e8fcb0b-452d-425b-967a-fcad4fce07c6 -->
# Aetherctl: Unified Cross‑Platform CLI

### Goal

Unify all operator tooling into a single, cross‑platform CLI (`aetherctl`) published as a .NET global tool. Standardize flags/output, port existing AgentCLI and WorldGenCLI features, add missing commands, and ensure JSON-friendly automation.

### Scope

- Create `Aetherctl/` project (System.CommandLine) with global flags: `--json`, `--verbose`, `--quiet`, `--timeout`, and Orleans connectivity: `--gateway`, `--cluster-id`, `--service-id` (env fallbacks).
- Port AgentCLI commands under `aetherctl`:
  - `session`: `list`, (later: `create`, `close` if server supports)
  - `agent`: `attach`, `step`, `run`, `stop`, `status`, (later: `list`)
  - `tools`: `list`, `describe`, `categories`, `test`
  - `vision`: `directional`, `omnidirectional`, `fov`, `status`
  - `world`: `create`, `list`, `info`, `pause`, `resume`, `shutdown`
  - `narrative`: `create`, `load`, `show`, `delete`, `list` (new)
  - `prompts`: `add`, `list`, `edit` (new), `delete` (new)
- Port WorldGenCLI under `aetherctl worldgen`:
  - `generate` with existing flags; add `--json` to stdout (in addition to `--output`).
  - `serve` HTTP API (preserve existing CORS); align port flag.
  - `render` subcommand: `--ascii` (Phase 1); `--png <path>` (Phase 2 with SkiaSharp).
- Add `monitor` subcommand (WebSocket client) mirroring PowerShell monitor: `--server-url`, `--ascii`, `--json`, `--save <dir>`, `--verbose`.
- JSON output contract: Successful commands print a single JSON object to stdout when `--json` is set; errors print `{ success:false, error:"..." }` and exit non‑zero.
- Packaging: Ship as dotnet global tool (`ToolCommandName=aetherctl`); update docs and deprecate old CLIs with pointers.

### Key Files

- New: `Aetherctl/Aetherctl.csproj`, `Aetherctl/Program.cs`, `Aetherctl/OrleansClientFactory.cs`, `Aetherctl/Commands/*`.
- Update (docs): `TOOL_SYSTEM_STATUS.md`, `docs/agents/*` (new CLI guide), `docs/console/user/README.md` (links).
- Optional Phase 2: Add `Aetherctl.Rendering` (SkiaSharp) for PNG.

### Command Shape (selected)

- `aetherctl session list [--json]`
- `aetherctl agent attach <sessionId> --agent <id> --runner <id>`
- `aetherctl tools test <toolId> --session-id <id> [--args '{...}']`
- `aetherctl world generate --generator ... --width ... --height ... [--json | --output file]`
- `aetherctl monitor --server-url ws://... [--ascii] [--json] [--save dir]`

### Non‑Goals

- Server‑side features beyond minimal hooks; where APIs don’t yet exist (agent/session list, create/close), add commands once server exposes them.

### Backward Compatibility

- Keep `AgentCLI` and `WorldGenCLI` in repo temporarily; mark deprecated in help. Recommend `aetherctl` going forward.

### Risks / Dependencies

- Agent registry and session lifecycle APIs may be needed for `list`/`create`/`close`—track as follow‑ups.
- PNG rendering requires a cross‑platform dependency; defer to Phase 2.

### Example Snippets

Global flags wiring (illustrative only):

```csharp
var root = new RootCommand("Aetherctl - unified CLI");
var jsonOpt = new Option<bool>("--json");
var verboseOpt = new Option<bool>("--verbose");
var quietOpt = new Option<bool>("--quiet");
var gatewayOpt = new Option<string?>("--gateway", () => Environment.GetEnvironmentVariable("ORLEANS_GATEWAY"));
var clusterOpt = new Option<string?>("--cluster-id", () => Environment.GetEnvironmentVariable("ORLEANS_CLUSTER_ID") ?? "dev");
var serviceOpt = new Option<string?>("--service-id", () => Environment.GetEnvironmentVariable("ORLEANS_SERVICE_ID") ?? "Aetherium");
root.AddGlobalOption(jsonOpt);
root.AddGlobalOption(verboseOpt);
root.AddGlobalOption(quietOpt);
root.AddGlobalOption(gatewayOpt);
root.AddGlobalOption(clusterOpt);
root.AddGlobalOption(serviceOpt);
```

### To-dos

- [x] Create `Aetherctl` project and wire System.CommandLine root
- [x] Add global flags and Orleans connectivity/env fallbacks
- [ ] Port agent/session/tools/vision/world commands to `aetherctl`
- [ ] Implement prompts edit/delete; add narrative list
- [ ] Port worldgen generate/serve; add --json and render --ascii
- [ ] Implement monitor subcommand (WebSocket) with ascii/json/save
- [ ] Standardize JSON output and non-zero exits on errors
- [ ] Package/publish as .NET global tool (ToolCommandName=aetherctl)
- [ ] Update TOOL_SYSTEM_STATUS and write CLI usage docs
- [ ] Mark AgentCLI/WorldGenCLI deprecated; update help pointers
- [ ] Add agent/session list/create/close once server APIs exist
- [ ] Add PNG render (SkiaSharp) under worldgen render --png


