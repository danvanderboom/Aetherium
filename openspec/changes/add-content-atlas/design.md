## Context

`TileTypeDto`/`VisualDto` (`Aetherium.Model`) carry an untyped `Settings: Dictionary<string,string>` that tile-type definitions fill with literal console glyphs and `ConsoleColor` names, read directly by the console renderer. The engine gap-analysis (§4.10) specs a typed, versioned `ContentAtlas` vocabulary as the fix, so perception payloads can reference stable ids instead. This change ships that schema plus a seed atlas and a coverage test — see [proposal.md](proposal.md) for why retrofitting the live DTOs/renderers is a deferred Phase 2.

## Goals / Non-Goals

- Goals:
  - Typed tag classes per §4.10: `TerrainTag`, `EntityKindTag`, `MaterialTag`, `LightSourceTag`, `AnimationCueTag`, `EffectTag`, `AudioTag`, each with a stable string `Id` + `Description`; `MaterialTag`/`LightSourceTag` carry the extra typed metadata the doc calls out explicitly.
  - `ContentAtlas` container with a semver `Version` and per-category lookup, plus a category-generic `Contains(category, id)` for the coverage-test / future "null renderer" pattern.
  - A real seed (`DefaultContentAtlas`) built from the engine's actual current tile-type and entity-kind vocabulary — not placeholder data.
  - A coverage test that fails if new tile types are added without a matching atlas entry, giving Phase 2 (and any future content work) an early warning instead of silent drift.
- Non-Goals (Phase 2 / later):
  - Changing `TileTypeDto`, `VisualDto`, `PerceptionDto`, or any DTO sent to clients.
  - Changing the console or Unity renderer.
  - A `content-atlas.v{n}.json` file-based delivery format or a version-negotiation handshake — the schema exists in code first; a serialization/delivery format is a Phase 2+ concern once something actually needs to ship the atlas over the wire.
  - Orientation/animation-cue *wiring* into entity DTOs (the `AnimationCueTag` vocabulary is defined here; nothing populates it on an entity yet).

## Decisions

- **Typed classes per category, not one generic `Tag{Id, Dictionary<string,object> Metadata}`.** The design doc is explicit that a material has `hardness/friction/combustibility` and a light source has `color/intensity/flicker` as *typed* fields, not a stringly-typed bag — that's the whole point of replacing `Settings<string,string>`. `TerrainTag`/`EntityKindTag`/`AnimationCueTag`/`EffectTag`/`AudioTag` carry only `Id`+`Description` for v1 since the doc doesn't specify richer fields for them yet; they can grow typed fields later without a breaking change (adding a property is additive).
  - Note: `TerrainTag` deliberately does **not** duplicate passability/opacity — those already live in `engine-core` (`ObstructsMovement`/`ObstructsView` components on `Aetherium.Server.Core.TileType`). The atlas is a *rendering* vocabulary, not a re-specification of movement rules.
- **`ContentAtlas` lives in `Aetherium.Model`, the seed in `Aetherium.Server`.** The schema must be shared by every client (console, Unity, future Unreal), so it belongs in the shared contracts project. The *seed data* reflects server-authored content (today's tile builder / entity classes) and has no reason to ship inside client-facing contracts.
- **Semver via a minimal `SemVer` value type**, not a NuGet dependency — parse/compare `major.minor.patch`; `ContentAtlas.SupportsClientVersion` checks major-version equality only, matching the doc's "renaming/removing a tag is a major bump; additive changes are minor" rule (an additive minor/patch bump must stay compatible with an older client that hasn't seen the new tags).
- **The coverage test derives expected ids from `TorusWorldBuilder().TileTypes` directly**, not a hand-maintained list, so it can't silently drift out of sync with the real tile vocabulary — the same reasoning as the design doc's "null renderer" idea, applied one step earlier (schema coverage instead of live perception-frame coverage, since Phase 1 doesn't wire DTOs yet).

## Risks / Trade-offs

- **Coverage is currently one-directional** (atlas ⊇ real tile types) and terrain-only; it doesn't yet check entity kinds or perception-frame content, because entity-kind and DTO wiring don't exist yet. Acceptable for Phase 1; Phase 2 extends the same pattern to entity kinds and, once DTOs reference ids, to actual perception frames.
- **Two vocabularies exist in parallel until Phase 2**: the atlas (new, unused by DTOs) and `Settings["MapCharacter"]`/`ConsoleColor` (old, still live). Zero risk to running gameplay since nothing consumes the atlas yet.

## Migration Plan

Additive only — no migration. Phase 2 (separate change) migrates `VisualDto`/`TileTypeDto` and both renderers onto atlas ids, and will need its own plan for how a renderer with a missing binding falls back (design doc: "missing bindings render as a fallback 'unknown' glyph or sprite").

## Open Questions

- Should `EntityKindTag` ids track the C# entity class name (`Monster`, `Zombie`) or a content-author-facing name (`monster.basic`, `monster.undead.zombie`) that survives a class rename? Deferred to Phase 2, where real DTO wiring forces the decision.
