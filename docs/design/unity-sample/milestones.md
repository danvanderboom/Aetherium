# Build Plan: Milestones

*Part of the [Unity sample design suite](README.md). Status: proposed plan.*

Three milestones, each independently valuable and demoable. Scope is expressed as deliverables, not dates. Engine work rides the normal OpenSpec change flow; library/sample work follows the [repo-structure migration phases](repo-structure.md#migration-path) (Phase A lands with M0, Phase B completes during M1).

## M0 — "First Light" *(the fun proof)*

**Goal:** one dark deck, one loop, two players, one screenshot that sells the game. If M0 isn't fun, we stop and redesign before building more.

| Track | Deliverables |
|---|---|
| Engine | **G1** interoception channel in perception (own HP, statuses, pools) — the only engine slice in M0 |
| Library | `Aetherium.Client` core: connection, mirror DTOs + drift tests, ToolClient (incl. composite absolute-move), PerceptionStore (anchoring, frame diff, memory), LobbyClient; integration tests against the in-proc server |
| Unity pkg | `com.aetherium.unity`: behaviour + dispatcher, GridMapView, EntityViewRegistry, ThemeAsset with fallback chain, mock provider + frame recorder, `link.xml`, pack script |
| Bundle | `Data/Games/aphelion/` v0.1: world (maze stand-in, 3 decks), death policy, content (5 creatures, 6 items, spawns), 4 ECA rules; no abilities yet (kit = attack + items) |
| Sample | Aphelion Unity project: dock spawn, WASD movement (composite), attack + damage numbers, pickup/medgel/arc-cutter, doors, death/respawn, client-side extraction + score screen, lobby (list/join) |
| Beauty | M0 kit meshes, lighting/bloom/grade stack, suit-lamp cone, room tone + footsteps + one music layer ([art-audio.md](art-audio.md) M0 screenshot test) |

**Acceptance:** two clients on a LAN join the same station, fight custodians in the dark (mites burst out on the ECA rule), one goes down and respawns at the dock, both extract; 60 fps on an integrated GPU; the M0 screenshot exists; `dotnet test` green including client drift + integration suites.

**Non-goals:** abilities, IR/sonar rendering, enemy health bars, adaptive-music graph beyond one layer, hosting from the lobby.

## M1 — "Reclaimer Kit" *(the beauty-and-depth pass)*

**Goal:** the game becomes *gorgeous* and mechanically expressive; the library reaches its public v0.1 tag.

| Track | Deliverables |
|---|---|
| Engine | **G3** `ability` tool · **G2** social-insight channel (condition bands, capability reads) · **G5** vision-mode enum completion · **G6** player instance creation (+ bundle opt-in) |
| Bundle | v0.2: `abilities.yaml` (overcharge-bolt, breach-strike, stasis-snare), `progression.yaml` skills that unlock them, tuning pass from M0 playtests |
| Sample | Ability HUD + targeting, enemy condition reads (posture/smoke/sparks per insight band) & status VFX, IR mode with cooling heat trails, sonar ping, full adaptive-music state graph, window-vista backdrops, "host a station" in the lobby, VFX pass (overcharge beam, stasis dome) |
| Library | v0.1 tag (`unity-client/v0.1.0`), consumable via git URL; legacy `Aetherium.Unity/` project retired after parity (migration Phase B); docs: package README + quickstart for external games |

**Acceptance:** the M1 screenshot set ([art-audio.md](art-audio.md)); an external empty Unity project can install the package from the git URL and render a live server following the quickstart; all M1 engine slices land with their own OpenSpec specs/tests.

## M2 — "The Long Dark" *(the systems milestone)*

**Goal:** runs have server-owned purpose, stations feel authored, co-op gets its signature move.

| Track | Deliverables |
|---|---|
| Engine | **G8** `objectives:` bundle section · **G9** `station` generator · **G10** status ticking (+ hazard groundwork) · **G7** revive interaction · **G11** per-world `simulation:` options |
| Bundle | v1.0: objectives (restore relays → extract), station generator params, hazard placements, overseer tuned as a real boss, power-cycle simulation options |
| Sample | Objective HUD + shared party goal flow, overseer death sequence, revive channel + down-state tableau, Bloom biome dressing, hazard presentation (fire/coolant), extraction framing shot |
| Stretch | Meta hooks (salvage banked across runs via existing meta-progression surface), a second backdrop set, gamepad polish |

**Acceptance:** a full co-op run — dock → objectives → overseer → revive moment → extraction — with zero client-side game rules; the M2 screenshot set; Emberfall (console) and Aphelion (Unity) running side-by-side from one server as the standing demo of *one engine, two genres, two renderers*.

## Sequencing notes

- **G1 lands first** (it unblocks the M0 HUD); everything else in M0 parallelizes across library/sample/bundle tracks.
- Every engine gap slice follows the repo's OpenSpec rhythm — proposal/design/tasks/spec with `Verified by:` tests — and none is speculative: each is pulled by a named milestone deliverable.
- The samples folder and package skeleton (migration Phase A) land at M0 start so all new code is born in its final home; nothing existing moves until Phase B/C.
