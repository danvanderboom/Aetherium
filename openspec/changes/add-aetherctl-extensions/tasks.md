> Status (2026-07-03): implementation shipped (root `session` command group, `worldgen render --png` via SkiaSharp, docs); the remaining gap is behavioral test coverage — the CLI tests are parse-only and never invoke rendering.

## 1. Implementation
- [x] 1.1 Add `aetherctl agent session list|create|close` shims (await server APIs) (checked 2026-07-03: shipped as a root `session list|close|create` group in Aetherctl/Commands/SessionCommands.cs, not under `agent`)
- [x] 1.2 Wire `aetherctl worldgen render --png <path>` behind feature flag (checked 2026-07-03: `--png` implemented in WorldGenCommands.cs, but with no feature flag)
- [x] 1.3 Introduce SkiaSharp dependency conditionally (multi-target if needed) (checked 2026-07-03: SkiaSharp 2.88.6 referenced unconditionally in Aetherctl.csproj; no multi-target)
- [x] 1.4 CLI help/docs updates (checked 2026-07-03: command help strings present; documented in docs/pcg-tools.md and docs/agents/README.md)
- [ ] 1.5 Tests for new commands and PNG output (golden image or checksum) (still open 2026-07-03: Aetherctl.Test's WorldGenRenderTests/SessionCommandsTests are parse-only — no test invokes a handler or actually renders a PNG)

