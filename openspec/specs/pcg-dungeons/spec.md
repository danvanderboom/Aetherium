# pcg-dungeons Specification

## Purpose
TBD - created by archiving change expand-pcg-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Varied Room Geometry and Corridors
Dungeon layouts SHALL include varied room shapes (rect, L, T, circular-like), corridor widths, and spacing.

#### Scenario: Shape diversity threshold
- WHEN generating a dungeon
- THEN at least two distinct room shape families appear

### Requirement: Branching and Loops
Dungeons MUST include branches and intentional loops for alternative routes.

#### Scenario: Minimum loop ratio
- WHEN validation runs
- THEN loop ratio meets configured minimum (e.g., ≥10%)

### Requirement: Multi-Level Dungeons
Dungeons SHALL span multiple levels with vertical connectors and consistent difficulty ramp.

#### Scenario: Cross-level path
- WHEN a multi-level dungeon is generated
- THEN there exists at least one traversable path connecting start to boss across levels

### Requirement: Secrets and Hidden Connectivity
Secret rooms/passages SHALL be placed with discoverability cues and optional rewards.

#### Scenario: Discoverable secret
- WHEN secrets are placed
- THEN at least one has a soft cue within N tiles (e.g., cracks, odd symmetry)

### Requirement: Gating and Keys
Critical areas MAY be gated; required keys/tools MUST exist and be accessible on the critical path.

#### Scenario: Access proof
- WHEN a gated door blocks progress
- THEN validation proves a path from start → key → gate → objective

