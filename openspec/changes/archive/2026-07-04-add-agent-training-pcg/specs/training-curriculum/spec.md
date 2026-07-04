## ADDED Requirements

### Requirement: Manual Curriculum Definition
The system SHALL support manual curriculum definitions in JSON format with multiple stages, prerequisites, and completion criteria.

#### Scenario: Curriculum loaded from JSON
- **WHEN** a curriculum JSON file is loaded from Data/Curricula/
- **THEN** it MUST be parsed into CurriculumDefinition with curriculumId, name, description, categories, version, and stages
- **AND** curriculum validation MUST detect missing required fields and duplicate stage IDs

#### Scenario: Curriculum stage defines parameters
- **WHEN** a CurriculumStage is created
- **THEN** it MUST include: StageId, Name, Description, Difficulty (0-100), Prerequisites, Parameters (dimensions, trap density, enemy count, etc.), and CompletionCriteria

#### Scenario: Stage prerequisites validated
- **WHEN** a curriculum stage has prerequisites
- **THEN** required stage IDs MUST exist in the curriculum
- **AND** minSuccessRate, minCompletedRuns, and minSkillLevel MUST be validated as reasonable values

### Requirement: Automatic Curriculum Generation
The system SHALL automatically generate curriculum stages based on agent performance analysis.

#### Scenario: Auto-generate initial stage
- **WHEN** AutoCurriculumGenerator.GenerateNextStage is called with no current stage
- **THEN** it MUST return an initial stage with low difficulty (20) and minimal complexity
- **AND** stage MUST have no prerequisites

#### Scenario: Auto-adjust difficulty based on performance
- **WHEN** agent success rate >=80% with >=20 steps
- **THEN** next stage difficulty MUST increase by 10 points (up to 100)
- **WHEN** agent success rate <40% with >=20 steps
- **THEN** next stage difficulty MUST decrease by 10 points (down to 0)

#### Scenario: Auto-adjust parameters for weaknesses
- **WHEN** performance analysis identifies weaknesses
- **THEN** stage parameters MUST be adjusted accordingly
- **AND** navigation weaknesses MUST reduce map size or room count
- **AND** key-lock weaknesses MUST reduce chain depth
- **AND** trap weaknesses MUST reduce trap density

### Requirement: Curriculum Progression Tracking
The system SHALL track agent progression through curriculum stages using an Orleans grain.

#### Scenario: Curriculum started for agent
- **WHEN** CurriculumProgressionGrain.StartCurriculumAsync is called with curriculum ID and agent ID
- **THEN** curriculum MUST be loaded and first stage MUST be set as current
- **AND** progression state MUST be initialized

#### Scenario: Stage completion checked
- **WHEN** CurriculumProgressionGrain.TryAdvanceStageAsync is called
- **THEN** it MUST check if current stage completion criteria are met
- **AND** it MUST verify prerequisites for next stage
- **AND** if all conditions met, current stage MUST advance to next

#### Scenario: Completion criteria enforced
- **WHEN** completion criteria include minSuccessRate
- **THEN** agent performance MUST meet or exceed the threshold
- **WHEN** completion criteria include minSuccessfulCompletions
- **THEN** agent MUST have completed that many successful runs
- **WHEN** completion criteria include minAttempts
- **THEN** agent MUST have attempted that many runs total

#### Scenario: Progress retrievable
- **WHEN** CurriculumProgressionGrain.GetProgressAsync is called
- **THEN** it MUST return CurriculumProgress with: CurriculumId, CurrentStageId, TotalStages, CompletedStages, TotalRuns, SuccessfulRuns, CurrentSuccessRate, and StageProgress dictionary

### Requirement: Curriculum Integration with World Generation
The system SHALL apply curriculum stage parameters to world generation requests.

#### Scenario: Stage parameters applied to request
- **WHEN** WorldGenerationRequest.ApplyCurriculumStage is called with a CurriculumStage
- **THEN** request dimensions MUST be set from stage parameters (width, height, levels)
- **AND** request Parameters dictionary MUST be populated with stage values (trapDensity, enemyCount, etc.)
- **AND** request IsTrainingMode MUST be set to true

#### Scenario: Stage parameters override defaults
- **WHEN** curriculum stage parameters are applied
- **THEN** they MUST override any default values in the request
- **AND** additional parameters from stage MUST be merged into request Parameters

