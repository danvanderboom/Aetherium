# Tasks

## 1. Heat durability (P3-8a)
- [x] 1.1 Add `ExportTrails()` / `ImportTrails()` to `HeatTrailTracker` (drop fully-faded trails at export)
- [x] 1.2 Add `PersistedHeatTrail` `[GenerateSerializer]` + `[Id(15)] HeatTrails` to `RegionStateSnapshot`
- [x] 1.3 `ForceSnapshotAsync` captures live trails; `TryHydrateFromSnapshotAsync` restores them

## 2. Observable delta-append failures + self-heal (P3-8b)
- [x] 2.1 Resolve `ILogger<GameMapGrain>`; replace persistence-path `Console.WriteLine` with structured logging
- [x] 2.2 Track append failure count / last error / timestamp; mark persistence dirty on failure
- [x] 2.3 On next successful append when dirty, force a healing snapshot and clear the flag
- [x] 2.4 Expose `GetPersistenceHealthAsync()` on `IGameMapGrain` + `PersistenceHealthDto`

## 3. Tests & verification
- [x] 3.1 Heat survives snapshot→hydrate round-trip (trails present with correct intensity)
- [x] 3.2 Throwing store → failure count increments + unhealthy (not swallowed); recovery triggers heal snapshot
- [x] 3.3 Full `Aetherium.sln` build (0 errors) + full `Aetherium.Test` suite green (1037 passed / 0 skipped)

## Notes
- `PersistedHeatTrail` stores primitive `X/Y/Z` (not `WorldLocation`) because `WorldLocation` has
  no Orleans codec — a `[GenerateSerializer]` type referencing it fails silo startup.
- Heat is carried on `RegionStateSnapshot` (persistence path) rather than `WorldSnapshot`, so the
  joiner-hydration path is unchanged and no `SnapshotVersion` bump is needed (Orleans tolerates
  the added `[Id(15)]` field on old snapshots).
