# Aetherium Quality-Improvement Plan

*Date: 2026-07-03. A sequenced, phased plan to act on the audits. It orders the [RECOMMENDATIONS.md](RECOMMENDATIONS.md) register into executable sprints, chosen so that each phase makes the next one safer and cheaper to verify. The strategic argument for this ordering is in [DESIGN_ANALYSIS.md](DESIGN_ANALYSIS.md); the short version: **build the safety net, fix what's silently wrong, converge the duplicates, then deepen one vertical slice at a time.***

## Guiding principles

1. **Safety net first.** Nothing else is safe to change until a full-solution build and test run gate every commit. The Dashboard sat broken for ~8 months precisely because nothing built the solution.
2. **Definition of done = end-to-end + tested + honest docs.** A fix isn't done when the class compiles; it's done when a player or agent can reach it through a client, an automated test exercises that path, and the docs/`tasks.md` say what actually works.
3. **Converge before you extend.** Every duplicate (two action paths, two clocks, two world-build paths, duplicated engine code) is fixed twice and drifts; collapse duplicates before building on them.
4. **Deepen one slice.** After the foundation is solid, finish *single-world exploration with validated movement, working interactions, visible NPCs, and an agent playing it* before re-opening multi-world/narrative/persistence.
5. **Respect OpenSpec.** Bug fixes and wiring go straight in; capability/architecture changes (P3 items, and the action-path convergence) get an OpenSpec proposal per [openspec/AGENTS.md](../../openspec/AGENTS.md), reusing/reconciling the seven active changes rather than duplicating them.

---

## Phase 1 — Foundation & quick wins (≈1 sprint)

**Goal:** green full-solution build, a working CI gate, runnable dev tooling, and the silent correctness bugs that a fresh contributor would trip over — cleared. Almost all of these are the [quick-wins shortlist](RECOMMENDATIONS.md#quick-wins-shortlist-high-value--a-few-hours-each).

- **Make the server bootable:** P0-0 (delete the self-referential DI bridge, `Program.cs:276-348` — the server currently hangs at startup with Orleans enabled; verify with a boot smoke test that asserts the startup banner appears with Orleans ON).
- **Restore the build:** P1-1 (Dashboard: rename `BehaviorAnalysis.razor` + delete `OrleansClientConnectionService`), P1-3 (add Aetherctl to `.sln`).
- **Restore the toolchain:** P1-2 (`global.json`/`RollForward` for the runtime mismatch), P1-19 (drop the stale Orleans pin), P1-21 (script bugs).
- **Stand up CI:** P2-1 (`dotnet build && dotnet test` on `main`) — do this *after* the build is green so the first run passes.
- **Silent correctness quick wins:** P0-2 (West/East axis), P0-4 (register `metaStore` + audit all store names), P0-5 (`Memory.AddMemory`), P1-16 (A/B seed), P1-17 (narrative seed), P1-5 (populate generator registry), P1-7 (prefab loading), P1-9 (dead input block), P1-18 (prompt copy-to-output).
- **Test hygiene:** P2-8 (un-skip/delete the 2 stale skipped tests).

**Exit criteria:** `dotnet build Aetherium.sln` is clean; CI runs and is green on `main`; dev scripts launch on a clean .NET machine; the quick-win correctness fixes have regression tests.

## Phase 2 — Security & correctness on live paths (≈1–2 sprints)

**Goal:** the paths a player/agent actually exercises are correct and not exploitable. This is where the "authoritative server" promise gets made real.

- **Movement & interaction validation:** P0-1 (single validated movement path; delete `MoveView`/`ChangeLevel` bypasses), P0-3 (range-check doors/use), P0-9 (capacity exploit), P0-10 (`RemoveEntity`/`MoveEntity` hardening) — with P2-3 (movement-path tests) written alongside.
- **Authorization:** P0-6 (centralize tool auth at the hub), P0-7 (auth on the REST control-plane).
- **Rendering/perception correctness:** P0-8 (infrared black-screen), P0-11 (console reconnect soft-lock), P1-10 (synchronize console rendering), P1-11 (StatusMessage + inventory markup).
- **Concurrency:** P0-12 (cross-path state races), P1-13 (thread-safe clock/weather/scheduler).

**Exit criteria:** a player cannot walk through walls, act at a distance, dupe items, or reach admin tools; the REST surface isn't anonymous; infrared and reconnect work; targeted tests cover each fix.

## Phase 3 — Convergence & debt reduction (≈1–2 sprints)

**Goal:** one implementation of each concept; delete the dead weight; make docs/specs tell the truth.

- **Converge duplicates:** P1-4 (retire obsolete hub methods → `ExecuteTool`, via an OpenSpec change that also updates `client-server-communication/spec.md`), P1-6 (unify worldgen pass lists), P1-8 (delete ~6k lines of dead legacy client engine), P1-12 (decide `WorldTickService`'s fate).
- **Honest stubs:** P1-14 (implement or honestly-decline the `NotImplementedException`/stub tools & repos), P1-15 (cap unbounded growth), P1-22 (enforce `MapValidator` at runtime + fix typos).
- **Truthful status:** P1-20 (reconcile all `tasks.md` with reality; split implemented/planned in the multiworld/instances/narrative docs).
- **Consider** extracting the engine code duplicated between `Aetherium.Console` and `Aetherium.Server` into a shared library (removes the fix-twice burden; larger, optional).

**Exit criteria:** one action path, one clock, one world-build pass-list; no dead legacy loops; every OpenSpec `tasks.md` reflects verified reality; docs mark planned-vs-implemented.

## Phase 4 — Test & CI depth (≈1–2 sprints, overlaps Phase 5)

**Goal:** the integration coverage that protects the deepening work in Phase 5, and the parts of the suite that are currently hollow.

- P2-2 (cross-grain integration tests: connect→act→observe, travel, quest, instance), P2-5 (real CLI tests), P2-6 (client-side unit tests), P2-4 (FOV rotation regression), P2-9 (PCG determinism), P2-10 (make the Unity project import & test — or rebase onto `develop`), P2-7 (share TestClusters to cut runtime).

**Exit criteria:** the vertical slice targeted in Phase 5 has an end-to-end test before it's declared done; CI covers CLI and (headless) client where feasible.

## Phase 5 — Deepen one vertical slice, then the rest (multi-sprint, per feature)

**Goal:** finish scaffolded subsystems to end-to-end working state, one at a time, each behind an OpenSpec change with the Phase-2 definition of done. Recommended order (dependency- and value-driven):

1. **Single-world exploration slice** — P3-1 (perception to observers) + the Phase 2 movement/interaction work makes co-op exploration real; make NPCs visible and ticking (depends on P1-12). This is the "make the game a game" milestone.
2. **Agent play** — P3-5 (agent↔game integration on the runner, prompt-driven decisions) so an LLM agent can play the slice; leverages the strong existing tool system.
3. **Durable persistence** — P3-8 (serialize `World`, finish a real grain-storage backend) so the slice survives restart; prerequisite for meaningful multi-world/meta-progression.
4. **PCG placement & difficulty** — P3-6 (real NPC/item/prefab placement, generator params affect output) so generated worlds are populated and the training/difficulty system stops being decorative.
5. **Narrative** — P3-2 (consequence engine wired into gameplay, quest activation).
6. **Multi-world travel** — P3-3 (break the circular dependency, load target worlds, client entry point).
7. **Instances/parties/raids** — P3-4 (entry points, lockout fixes, cleanup sweeper).
8. **Combat** — P3-7 (implement, or formally remove the scaffolding).
9. **Polish** — P3-9 (audio assets/effects), P3-10 (finish Dashboard pages, move contracts to `Aetherium.Model`).

**Exit criteria (per item):** reachable through a client, covered by an integration test, gated by CI, and documented as working.

---

## Sequencing rationale (at a glance)

```
Phase 1  Foundation ──► Phase 2  Live-path correctness ──► Phase 3  Convergence
   │  (build+CI+quick fixes)      (validation+auth+races)       (kill duplicates+dead code)
   └─────────────────────────────────────────────┬───────────────────────────┘
                                                  ▼
                                    Phase 4  Test depth  ◄─overlaps─►  Phase 5  Deepen slices
                                    (integration+CLI+client+Unity)      (one subsystem at a time,
                                                                          each end-to-end + tested)
```

Phases 1–3 are mostly bug-fix/refactor work that needs no OpenSpec proposal (except P1-4's action-path change). Phase 5 is feature work; each item should open or reconcile an OpenSpec change before implementation, per the project's workflow.

## Effort & risk notes

- Phases 1–2 are where the correctness and security risk lives and are the best value; they are also the least likely to break anything because the suite (once CI runs) guards them.
- Phase 3's biggest single item (deleting ~6k lines of dead client code, P1-8) is low-risk *because* it's unreferenced — verify with a build + test after removal.
- Phase 5 items are genuinely large (L) and should not be estimated as "finish the TODO"; each is a real feature with a design, because the scaffolding solved the easy 20%.
- Throughout: prefer the boring, proven fix; keep changes small enough to verify with the newly-standing CI; and update the relevant OpenSpec spec/`tasks.md` as part of "done," not after.
