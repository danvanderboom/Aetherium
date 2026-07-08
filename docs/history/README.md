# Historical Documents

Point-in-time status reports, bug investigations, and implementation plans, archived here during documentation consolidations (first batch 2026-07-03; a second batch of superseded status/plan docs added 2026-07-08). They record what was true **when they were written** — do not treat them as current. Internal links inside these files may reference old (pre-move) locations. For current documentation, start at [docs/README.md](../README.md); for the current state of each subsystem, see [docs/audits/](../audits/).

| File | What it was | Superseded by |
|---|---|---|
| `CLIENT_SERVER_README.md` | Original client-server architecture overview & run instructions | [architecture/overview.md](../architecture/overview.md), root [README.md](../../README.md) |
| `IMPLEMENTATION_COMPLETE.md` | Client-server conversion completion report (2025-10) | [architecture/overview.md](../architecture/overview.md) |
| `TEST_STATUS.md` | Build/test snapshot ("597 passed / 2 skipped") | [audits/2026-07-03-initial-subsystem-audit/README.md](../audits/2026-07-03-initial-subsystem-audit/README.md) ground-truth section |
| `TOOL_SYSTEM_STATUS.md` | Agent tool system phase tracker & security-fix notes | [architecture/server.md](../architecture/server.md), [audits/2026-07-03-initial-subsystem-audit/agents-and-tools.md](../audits/2026-07-03-initial-subsystem-audit/agents-and-tools.md) |
| `SPECTRE_IMPLEMENTATION_SUMMARY.md` | Console UI abstraction implementation report | [architecture/clients.md](../architecture/clients.md) |
| `MONITORING_IMPLEMENTATION_SUMMARY.md` | Monitoring system implementation report | [docs/monitoring.md](../monitoring.md) |
| `FOV_BUG_SUMMARY.md`, `FOV_FIX_SUMMARY.md`, `FOV_ISSUES.md`, `FOV_TEST_MAPS_EXPECTED.md` | FOV/rotation bug investigation & fix records (2025-11) | [audits/2026-07-03-initial-subsystem-audit/perception-fov-lighting.md](../audits/2026-07-03-initial-subsystem-audit/perception-fov-lighting.md) |
| `ORLEANS_IMPLEMENTATION_PLAN.md` | Multi-world grain roadmap (travel/housing/factions/territory still unbuilt) | [architecture/server.md](../architecture/server.md), [audits/2026-07-03-initial-subsystem-audit/orleans-and-hosting.md](../audits/2026-07-03-initial-subsystem-audit/orleans-and-hosting.md) |
| `PHASE8_STATUS.md` | Tool-system phase 8 test status (from Aetherium.Test) | [audits/2026-07-03-initial-subsystem-audit/tooling-testing-devex.md](../audits/2026-07-03-initial-subsystem-audit/tooling-testing-devex.md) |
| `a.plan.md` | Unified CLI (aetherctl) build plan | [architecture/tooling-and-data.md](../architecture/tooling-and-data.md) |
| `Goals.txt` | Original design goals & scratch notes | Root [README.md](../../README.md) |
| `AGENTS_IMPLEMENTATION_SUMMARY.md` | Agent tool-system implementation report ("17 tools", Oct 2025) | [agents/TOOLS.md](../agents/TOOLS.md), [agents/README.md](../agents/README.md) |
| `AGENTS_FINAL_SUMMARY.md` | Agent tool-system "final" status & metrics (Oct 2025; lists placeholder tools never built) | [agents/TOOLS.md](../agents/TOOLS.md) |
| `events-spawn-integration-plan.md` | Event↔spawn integration plan (all steps now shipped) | [architecture/server.md](../architecture/server.md) Events section |
