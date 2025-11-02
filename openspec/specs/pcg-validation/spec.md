# pcg-validation Specification

## Purpose
TBD - created by archiving change expand-pcg-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Connectivity and Access Proofs
Validation MUST confirm start→objective connectivity, key→lock access, and absence of generation deadlocks.

#### Scenario: Access proof recorded
- WHEN validation passes
- THEN proof artifacts (paths, reasons) are recorded

### Requirement: Structural Metrics Thresholds
Maps MUST meet configured thresholds (branching factor, loop ratio, dead-end cap, path-length distribution).

#### Scenario: Threshold enforcement
- WHEN a metric is out of bounds
- THEN regeneration or fallback strategy is applied

### Requirement: Multi-Level Validation
Vertical paths and connectors SHALL be validated across levels for reachability and safety.

#### Scenario: Cross-level validation
- WHEN multi-level maps exist
- THEN at least one valid route spans required levels

### Requirement: Performance Budgets
Generation MUST complete within time/memory budgets, with incremental fallback if exceeded.

#### Scenario: Time budget fallback
- WHEN a budget is exceeded
- THEN the system downgrades complexity or retries with simpler parameters

