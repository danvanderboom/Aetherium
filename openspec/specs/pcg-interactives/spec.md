# pcg-interactives Specification

## Purpose
TBD - created by archiving change expand-pcg-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Keys, Locks, and Tools
Interactive locks SHALL require items or tools that are generated and accessible prior to the lock.

#### Scenario: Guaranteed key availability
- WHEN a lock is placed
- THEN its key/tool exists with a provable access path from the start

### Requirement: Puzzles and Multi-Path Solutions
Puzzles SHALL support at least one alternate solution path when the template supports alternates (e.g., tool, brute-force risk, secret bypass).

#### Scenario: Alternate solution present
- WHEN a puzzle template supports alternates
- THEN at least one alternate is placed within constraints

### Requirement: Traps and Risk/Reward
Traps SHALL include at least one telegraphing cue to reward player skill.

#### Scenario: Telegraphed trap
- WHEN a trap is placed
- THEN at least one cue (visual/audio) exists within perception range

### Requirement: Destructible/Movable Obstacles
Certain obstacles SHALL be solvable by environment interaction (push blocks, breakable walls) where applicable.

#### Scenario: Environment solve exists
- WHEN an obstacle blocks optional content
- THEN a feasible environment-based solution is present

