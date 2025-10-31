# Curriculum Guide

This guide explains how to create custom training curricula for agents.

## Curriculum Structure

A curriculum consists of multiple stages that agents progress through sequentially or automatically based on performance.

### JSON Format

```json
{
  "curriculumId": "my-curriculum",
  "name": "My Training Curriculum",
  "description": "Description of what this curriculum teaches",
  "categories": ["category1", "category2"],
  "version": "1.0.0",
  "autoProgression": false,
  "stages": [
    {
      "stageId": "stage-1",
      "name": "First Stage",
      "description": "What agents learn in this stage",
      "difficulty": 20,
      "prerequisites": {
        "requiredStageIds": [],
        "minSuccessRate": null,
        "minCompletedRuns": null,
        "minSkillLevel": null
      },
      "parameters": {
        "width": 40,
        "height": 40,
        "levels": 1,
        "trapDensity": 0.0,
        "enemyCount": 0,
        "puzzleComplexity": 0.0,
        "keyLockChainDepth": 0,
        "secretRoomDensity": 0.0,
        "minRooms": 3,
        "maxRooms": 5,
        "minBranchingFactor": 0.2,
        "maxBranchingFactor": 0.4,
        "resourceAvailability": 0.8,
        "combatDifficulty": 0.0,
        "additionalParameters": {}
      },
      "completionCriteria": {
        "minSuccessRate": 0.7,
        "minSuccessfulCompletions": 3,
        "minAttempts": 5,
        "maxAverageSteps": null,
        "minEfficiency": 0.6
      }
    }
  ]
}
```

## Stage Parameters

### Dimensions
- `width`: Map width (default: 40-80)
- `height`: Map height (default: 40-80)
- `levels`: Number of vertical levels (default: 1)

### Difficulty Settings
- `trapDensity`: 0-1, density of traps (default: 0.0)
- `enemyCount`: Number of enemies (default: 0)
- `puzzleComplexity`: 0-1, puzzle difficulty (default: 0.0)
- `keyLockChainDepth`: Number of sequential key-lock pairs (default: 0)
- `secretRoomDensity`: 0-1, density of secret rooms (default: 0.0)
- `combatDifficulty`: 0-1, combat challenge level (default: 0.0)

### Map Structure
- `minRooms` / `maxRooms`: Room count range (default: 3-5)
- `minBranchingFactor` / `maxBranchingFactor`: Navigation complexity (default: 0.2-0.4)
- `resourceAvailability`: 0-1, resource abundance (default: 0.5)

## Completion Criteria

Define when an agent is ready to advance:

- `minSuccessRate`: Minimum success rate (0-1)
- `minSuccessfulCompletions`: Minimum successful runs
- `minAttempts`: Minimum total attempts
- `maxAverageSteps`: Maximum average steps (optional)
- `minEfficiency`: Minimum efficiency score (0-1, optional)

## Prerequisites

Control stage access:

- `requiredStageIds`: Previous stages that must be completed
- `minSuccessRate`: Minimum success rate in prerequisites
- `minCompletedRuns`: Minimum completed runs
- `minSkillLevel`: Minimum skill level (future use)

## Auto-Progression

Set `autoProgression: true` to enable automatic difficulty adjustment based on agent performance. The system will:

- Analyze agent performance after each stage
- Adjust difficulty automatically
- Generate new stages dynamically

## Example: Beginner Dungeon Curriculum

See `Data/Curricula/beginner-dungeon.json` for a complete example.

## Best Practices

1. **Start Simple**: Begin with minimal obstacles and complexity
2. **Progressive Difficulty**: Gradually increase challenge
3. **Clear Objectives**: Each stage should teach a specific skill
4. **Reasonable Criteria**: Set achievable completion thresholds
5. **Track Progress**: Monitor agent performance across stages

