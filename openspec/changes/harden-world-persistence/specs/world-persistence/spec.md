# world-persistence Specification

## ADDED Requirements

### Requirement: Heat-trail state survives cold start

The grain-authoritative heat-trail map SHALL be captured in the world snapshot and restored on
grain reactivation, so infrared/heat-vision perception does not silently lose recent-trail state
across a silo restart. Trails that have fully faded at capture time MAY be dropped, and restored
trails SHALL preserve their original timestamps so fade continues correctly across the restart.

#### Scenario: Heat trails restored after snapshot round-trip
- **GIVEN** a map has recorded heat trails at one or more cells
- **WHEN** the grain captures a snapshot and is later rehydrated from it (cold start)
- **THEN** the restored heat map reports the same non-faded trails (locations and intensities)
  that were present at capture time

#### Scenario: Fully-faded trails are not persisted
- **WHEN** a snapshot is captured while some trails have already fully faded
- **THEN** those faded trails are not written into the snapshot

### Requirement: Delta-append failures are observable and self-healing

A failure to append a map delta to the durable log SHALL NOT be silently swallowed. The grain
SHALL log the failure via structured logging (not `Console.WriteLine`), record a failure count
and the last error, expose that health via a grain query, and recover: because a full snapshot
supersedes any deltas that failed to append, the grain SHALL force a healing snapshot once
persistence succeeds again after a failure.

#### Scenario: Append failure is recorded, not swallowed
- **WHEN** the snapshot store throws on `AppendMapDeltaAsync`
- **THEN** the grain's persistence health reports a non-zero failure count and the last error,
  and remains functional for gameplay

#### Scenario: Recovery forces a healing snapshot
- **GIVEN** at least one delta append has failed (persistence marked dirty)
- **WHEN** a subsequent delta append succeeds
- **THEN** the grain captures a full snapshot so the durable state reflects the mutations whose
  deltas were lost, and clears the dirty flag
