# Design Suite: Unity Client Library & the Aphelion Sample Game

*Status: design (2026-07-16), plus the first concrete slice: the [Aphelion Unity project skeleton](../../../samples/unity/Aphelion/README.md) with its CC0/generated asset set is committed under `samples/unity/Aphelion/`. Code (client library, scenes, engine gap slices) is not yet implemented. Ground-truth claims about the engine were verified against `develop` @ df65e5d.*

## What this is

Aetherium's vision claims to be **render-agnostic** (semantic perception any client can bind to its own assets) and **genre-agnostic** (games are data bundles, not code). This initiative is the proof: a reusable **Unity client library** any Unity game can install, a **samples folder** housing games across engines (Unity, Unreal, console), and one flagship sample — **Aphelion**, a co-op sci-fi salvage crawl through massive space stations orbiting planets and stars, designed to start simple, be genuinely fun, and grow richer over time. Beauty is a first-class requirement: lighting, VFX, sound, and adaptive music are designed, not deferred.

The engine barely changes. The entire meaning of Aphelion is a YAML bundle (`Data/Games/aphelion/`) loaded by shipped systems; the client work is presentation. The honest list of engine additions is one small protocol slice for milestone 0, and a short, sized backlog after that.

## Reading order

| Doc | What it covers |
|---|---|
| **[repo-structure.md](repo-structure.md)** | Where everything lives: `Aetherium.Client` core (NuGet), `com.aetherium.unity` (UPM via git URL), `clients/` + `samples/` layout, migration phases, source-control hygiene for Unity content |
| **[unity-client-library.md](unity-client-library.md)** | The library design: the protocol as it really is, the two-layer architecture, the anchoring solution for player-relative coordinates, frame-diff entity tracking, ThemeAsset presentation binding, drift-test strategy, IL2CPP notes |
| **[architecture.md](architecture.md)** | End-to-end system diagrams: client subsystems ↔ server/cluster subsystems, session bootstrap, the co-op action loop, YAML→pixels data flow, deployment topologies, trust model |
| **[game-design.md](game-design.md)** | Aphelion: premise, pillars, core loop, the full draft bundle in real YAML (creatures, items, ECA rules, abilities, factions, progression), combat feel, multiplayer shape, expansion roadmap |
| **[art-audio.md](art-audio.md)** | The beauty pass: low-poly URP direction, palette, lighting recipe, the modular station kit, VFX set, IR/sonar rendering, adaptive music state machine, asset licensing rules |
| **[assets.md](assets.md)** | Where every asset comes from: CC0 packs (Quaternius/Kenney), made-in-project, and code-generated music — with the committed first slice inventoried |
| **[engine-gaps.md](engine-gaps.md)** | What the engine lacks, sized and milestone-mapped — and the longer list of things that looked like gaps but are already shipped |
| **[milestones.md](milestones.md)** | M0 "First Light" / M1 "Reclaimer Kit" / M2 "The Long Dark" — deliverables and acceptance criteria per track |

## The design in five decisions

1. **Two-layer client library.** A pure `netstandard2.1` core (`Aetherium.Client`) owns protocol, connection, and state-tracking logic — testable with `dotnet test` beside the server. A thin UPM package (`com.aetherium.unity`) owns main-thread dispatch and Unity binding. Games install the package via git URL; the core doubles as a NuGet for console/other .NET clients.
2. **Mirror DTOs with build-breaking drift tests, not shared assemblies.** `Aetherium.Model` carries Orleans dependencies that must not enter Unity. The core defines wire-shape mirrors, and a server-side test project round-trips real DTOs through hub JSON into the mirrors — schema drift fails the build (the pattern that retired the last Unity attempt's fatal flaw).
3. **The client is a frame consumer with local intelligence.** The server pushes full player-relative perception frames; the library centralizes the hard parts every game would otherwise botch — anchoring to stable coordinates, frame-diffing into entity lifecycle events, remembered-terrain state — and exposes a clean event surface.
4. **Meaning and presentation meet at content ids.** Bundle-authored ids (`custodian`, `arc-cutter`, `burning`) arrive in perception; a ThemeAsset ScriptableObject maps them to prefabs, VFX, and audio with a never-invisible fallback chain. Adding game content = one YAML block + one theme row; the engine stays semantic.
5. **Beauty rides the perception stack the engine already has.** Per-cell light levels, vision modes (IR heat trails, sonar), audio hints (biome, danger, footsteps, reverb) are shipped server features no client has done justice to — Aphelion's art direction is built around finally showing them off.

## Why a space station game

The setting is the engine's feature set wearing fiction: decks are z-levels, bulkheads are the keys-and-locks system, darkness-as-danger is the lighting simulation, IR/sonar are shipped vision modes, feral drones are data-driven creatures with behavior presets, the station's death-reactions are ECA rules, station sub-minds are factions, and every station is a fresh world instance from one bundle — infinitely many stations from one YAML folder. Co-op is the engine's native multiplayer posture. Nothing about the premise fights the substrate.

## Relationship to existing work

- Supersedes the scaffold `Aetherium.Unity/` project (its provider abstraction, mock-first testing idea, and input maps carry forward into the package; the project retires at M1 parity — details in [repo-structure.md](repo-structure.md)).
- Complements `docs/clients/unreal-client-guide.md` — `Aetherium.Client` is the reusable layer that guide already assumes.
- Sits beside the bundle samples: Emberfall (fantasy, split-file), Neonveil (sci-fi netrunner, single-file, permadeath) — Aphelion is the first bundle with a bespoke visual client.
- Engine gap slices will follow the standard OpenSpec change flow when their milestones arrive; this suite is design-ahead documentation, not a spec change itself.
