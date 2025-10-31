# Benchmark Scenarios

This directory contains benchmark scenario definitions for evaluating agent performance.

## Format

Each benchmark is a JSON file with the following structure:

```json
{
  "benchmarkId": "unique-id",
  "name": "Display Name",
  "description": "What this benchmark tests",
  "categories": ["category1", "category2"],
  "difficulty": 5,
  "version": "1.0.0",
  "recipe": {
    "generator": "AdvancedDungeon",
    "template": "dungeon",
    "seed": 12345,
    "generatorVersion": "1.0.0",
    "width": 60,
    "height": 60,
    "levels": 1,
    "parameters": {
      "key": "value"
    }
  },
  "successCriteria": {
    "type": "ReachGoal|CollectItems|SurviveTurns|CompleteWithinLimits",
    "goalLocation": { "x": 50, "y": 50, "z": 0 },
    "requiredItems": ["item1", "item2"],
    "minSurvivalTurns": 50,
    "maxSteps": 200,
    "maxTimeSeconds": 300.0
  }
}
```

## Success Criteria Types

- **ReachGoal**: Agent must reach a specific location
- **CollectItems**: Agent must collect specific items
- **SurviveTurns**: Agent must survive for a minimum number of turns
- **CompleteWithinLimits**: Agent must complete within step/time limits

## Standard Benchmarks

- **navigation-basic**: Simple pathfinding test
- **combat-survival**: Combat encounter survival
- **puzzle-keys**: Multi-key lock chain navigation

## Versioning

Benchmark versions are tracked to enable regression detection. When updating a benchmark, increment the version number.

