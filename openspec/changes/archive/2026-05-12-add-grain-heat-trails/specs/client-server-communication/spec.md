## MODIFIED Requirements

### Requirement: Heat Trail Tracking is Grain-Authoritative
The `HeatTrailTracker` for a map SHALL be owned by `IGameMapGrain`, not by individual sessions. Heat recording and decay SHALL be driven by grain-side mutations and tick events. Sessions SHALL maintain a local `HeatTrailTracker` mirror that converges to the grain's via `HeatRecordedDelta` and `HeatExpiredDelta` deltas. Whether a session can *perceive* a heat trail (e.g. `VisionMode.Infrared` reveals trails the eye can't see) remains a session-side perception filter on the same underlying data. Sessions SHALL NOT collect heat by iterating their local mirror's entities — the per-perception `UpdateHeatTracker` pass is removed; heat flows in only via deltas.

#### Scenario: Movement records heat in the grain
- **WHEN** any entity with a `HeatSignature` component moves and the move fires an `EntityMoved` event on `_world.WorldEvents`
- **THEN** the grain's subscriber SHALL call `_heatTracker.RecordEntityPosition` at the destination location with the current game time
- **AND** the grain SHALL emit a `HeatRecordedDelta { EntityId, X, Y, Z, GameTimeHours, Intensity }`
- **AND** the delta SHALL propagate to every session bound to the map via the host-side broker (`GameSessionManager.NotifyMapMutationAsync`)

#### Scenario: Heat expiry fires from the grain
- **WHEN** `IGameMapGrain.TickAsync` runs and `HeatTrailTracker.CleanupOldTrails(cutoff)` removes trails older than the retention window
- **THEN** the grain SHALL emit a `HeatExpiredDelta { X, Y, Z }` for each cell whose last trail was just cleared
- **AND** the grain SHALL NOT emit expiry deltas for cells that already had no trails before the cleanup

#### Scenario: Session ApplyDelta updates the local mirror
- **WHEN** a session receives a `HeatRecordedDelta`
- **THEN** the session SHALL update its local `HeatTrailTracker` with the same `(location, intensity, gameTime)` triple
- **AND** if the entity referenced by `EntityId` is in the session's local mirror, its reference SHALL be attached
- **AND** if the entity is not in the session's mirror, the heat SHALL still be recorded — heat is observable independently of entity visibility

#### Scenario: Session ApplyDelta removes expired trails
- **WHEN** a session receives a `HeatExpiredDelta`
- **THEN** the session SHALL call `HeatTrailTracker.RemoveTrailsAt(new WorldLocation(X, Y, Z))` to clear that cell's trails from the local mirror

#### Scenario: Two sessions in the same map have identical heat-tracker mirrors
- **WHEN** sessions A and B are joined to the same map and a sequence of movements occurs
- **THEN** after delta propagation, A's local heat data and B's local heat data SHALL be equivalent (same trails at same cells, same intensities, same timestamps)
- **AND** rendering differences SHALL come only from per-session vision modes, not from divergent data

#### Scenario: GetPerception no longer drives heat collection
- **WHEN** a session's `GetPerception` is invoked
- **THEN** it SHALL NOT iterate `World.Entities` to populate the heat tracker
- **AND** the heat tracker's content reflects only what `ApplyDelta` has put there
