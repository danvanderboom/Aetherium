## 1. Schema (Phase 1 — this change)

- [x] 1.1 `ContentAtlasTag` base + `TerrainTag`, `EntityKindTag`, `AnimationCueTag`, `EffectTag`, `AudioTag` (id + description)
- [x] 1.2 `MaterialTag` (+ hardness/friction/combustibility) and `LightSourceTag` (+ color/intensity/flicker)
- [x] 1.3 `SemVer` value type (parse/compare `major.minor.patch`)
- [x] 1.4 `ContentAtlas` container: per-category dictionaries, add/lookup, category-generic `Contains`, `SupportsClientVersion`
- [x] 1.5 `DefaultContentAtlas` seed (`Aetherium.Server`) covering `TorusWorldBuilder`'s 11 terrain names and the known entity classes (`Character`/player, `Monster`, `Zombie`, `Snake`, `SwordItem`, `KeyItem`, `FoodItem`, dead-monster marker)
- [x] 1.6 Coverage test: every `TorusWorldBuilder().TileTypes` name resolves to a `TerrainTag` in `DefaultContentAtlas`
- [x] 1.7 Unit tests: duplicate-id rejection, `Contains` across categories, `SupportsClientVersion` major/minor/patch behavior
- [x] 1.8 `openspec/specs/content-atlas/spec.md` delta: ADDED requirements

## 2. Live wiring (Phase 2 — separate follow-up change, not started here)

- [ ] 2.1 Add atlas-id fields to `TileTypeDto`/`VisualDto` alongside the existing `Settings` dict
- [ ] 2.2 Replace `VisualDto.LightLevel` (scalar) with a `LightSourceTag`-shaped `{intensity, colorTag, source[]}`
- [ ] 2.3 Populate `intent`/`cadence` on entity perception DTOs from `AnimationCueTag`
- [ ] 2.4 Migrate `ClientConsoleMapView` off `Settings["MapCharacter"]`/`ConsoleColor` onto atlas-id lookups, with an "unknown" glyph fallback
- [ ] 2.5 Wire the Unity client's `TileTypeLite`/`VisualLite` consumers onto the same ids
- [ ] 2.6 Extend the coverage test to real `PerceptionDto` frames (the design doc's literal "null renderer" test) once DTOs reference ids
- [ ] 2.7 Remove the old `Settings["MapCharacter"]`/color keys once every renderer has migrated
