## ADDED Requirements

### Requirement: Content Atlas Schema
The system SHALL define a `ContentAtlas` shared contract (`Aetherium.Model`) holding seven categories of typed, string-id-keyed tags — `TerrainTag`, `EntityKindTag`, `MaterialTag`, `LightSourceTag`, `AnimationCueTag`, `EffectTag`, `AudioTag` — each carrying at minimum a stable `Id` and a human-readable `Description`. `MaterialTag` SHALL additionally carry typed `Hardness`, `Friction`, and `Combustibility` fields; `LightSourceTag` SHALL additionally carry typed `ColorHex`, `Intensity`, and `Flicker` fields.

#### Scenario: A material tag carries typed metadata, not a string bag
- **WHEN** a `MaterialTag` with `Id = "stone"` is constructed with `Hardness = 0.9`, `Friction = 0.6`, `Combustibility = 0.0`
- **THEN** those values are readable as typed `double` properties, not looked up by string key

### Requirement: Content Atlas Versioning
A `ContentAtlas` SHALL carry a semantic version (`Version`, `major.minor.patch`). `ContentAtlas.SupportsClientVersion` SHALL report compatibility based on major-version equality only, so additive (minor/patch) atlas changes remain compatible with a client that declared an older minor/patch of the same major version.

#### Scenario: Same-major client is supported
- **WHEN** a `ContentAtlas` has `Version = "1.3.0"` and a client declares `"1.0.0"`
- **THEN** `SupportsClientVersion("1.0.0")` returns `true`

#### Scenario: Different-major client is not supported
- **WHEN** a `ContentAtlas` has `Version = "2.0.0"` and a client declares `"1.9.9"`
- **THEN** `SupportsClientVersion("1.9.9")` returns `false`

### Requirement: Content Atlas Lookup
A `ContentAtlas` SHALL reject adding a tag whose `Id` already exists in that tag's category, and SHALL expose a category-generic `Contains(category, id)` check usable to validate that a referenced id is registered.

#### Scenario: Duplicate id within a category is rejected
- **WHEN** a `TerrainTag` with `Id = "wall"` is already registered and a caller attempts to add another `TerrainTag` with `Id = "wall"`
- **THEN** the add is rejected and the original tag is unchanged

#### Scenario: Cross-category lookup succeeds and fails correctly
- **WHEN** `Contains(ContentAtlasCategory.Terrain, "wall")` is checked against an atlas that registered a `TerrainTag` with `Id = "wall"` but no `EntityKindTag` with that id
- **THEN** `Contains(ContentAtlasCategory.Terrain, "wall")` returns `true` and `Contains(ContentAtlasCategory.EntityKind, "wall")` returns `false`

### Requirement: Default Content Atlas Coverage
The default seed atlas (`DefaultContentAtlas`) SHALL register a `TerrainTag` for every tile-type name the engine's default world builder (`TorusWorldBuilder`) actually produces, so the atlas never silently falls behind the real tile vocabulary.

#### Scenario: Every real terrain tile type is covered
- **WHEN** `TorusWorldBuilder().TileTypes` is enumerated and filtered to terrain-only names
- **THEN** every resulting name resolves to a `TerrainTag.Id` present in `DefaultContentAtlas.Build()`
