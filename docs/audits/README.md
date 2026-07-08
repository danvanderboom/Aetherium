# Aetherium Audits

Dated, self-contained audit passes over the codebase and design. Each subfolder is one audit round — named `YYYY-MM-DD-<short-slug>` for the date the round was conducted — and is not edited by later rounds; a new gap or finding gets a new dated folder instead. This keeps each audit as an honest point-in-time record and gives a historical view of what was assessed, and when.

## Audit rounds

| Round | Date | Scope | Status |
|---|---|---|---|
| [2026-07-03-initial-subsystem-audit/](2026-07-03-initial-subsystem-audit/) | 2026-07-03 | Ten subsystem audits (Orleans/hosting, client-server protocol, simulation core, perception, agents/tools, narrative/multiworld, console client, Unity/dashboard, worldgen/PCG, tooling/testing/devex) plus a consolidated [RECOMMENDATIONS.md](2026-07-03-initial-subsystem-audit/RECOMMENDATIONS.md) register, [DESIGN_ANALYSIS.md](2026-07-03-initial-subsystem-audit/DESIGN_ANALYSIS.md), and a phased [IMPROVEMENT_PLAN.md](2026-07-03-initial-subsystem-audit/IMPROVEMENT_PLAN.md). | **Closed 2026-07-04.** Every actionable P0/P1/P2/P3 item landed on `develop`; the only open entries are two deliberate non-implementations (CI, deferred for cost; TestCluster sharing, skipped as not worth the flakiness risk). See the register's reconciliation note for the full history. |
| [2026-07-06-engine-gap-analysis/](2026-07-06-engine-gap-analysis/) | 2026-07-06 | Forward-looking design review (not a code-correctness audit): [design-next-steps.md](2026-07-06-engine-gap-analysis/design-next-steps.md) catalogs missing engine-level gameplay systems and proposes a priority roadmap; [design-authoring-and-scripting.md](2026-07-06-engine-gap-analysis/design-authoring-and-scripting.md) and [design-eca-visual-scripting.md](2026-07-06-engine-gap-analysis/design-eca-visual-scripting.md) sketch a data-driven authoring pipeline and a visual ECA scripting language. | **Open / draft for discussion.** None of these are OpenSpec proposals yet; each recommended subsystem should become its own `openspec/changes/*` proposal before implementation. |

## Adding a new audit round

1. Create `docs/audits/YYYY-MM-DD-<short-slug>/` for the day the audit is conducted (or written up).
2. Put all of that round's documents inside it — findings, recommendations, design analyses, whatever the round produces.
3. Add a row to the table above.
4. Leave prior rounds' folders alone; reconcile findings by adding a note (with a date) rather than rewriting history.
