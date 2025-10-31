# Benchmark Format

This document describes the JSON format for benchmark scenarios.

## JSON Schema

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
    "maxTimeSeconds": 300.0,
    "customCriteria": {}
  }
}
```

## Recipe Parameters

The `recipe` section defines how the world is generated:

- `generator`: Generator name (e.g., "AdvancedDungeon", "PerlinTerrain")
- `template`: Template type ("dungeon" or "outdoor")
- `seed`: Deterministic seed (null for random)
- `generatorVersion`: Generator version string
- `width` / `height` / `levels`: World dimensions
- `parameters`: Generator-specific parameters

## Success Criteria Types

### ReachGoal

Agent must reach a specific location:

```json
{
  "type": "ReachGoal",
  "goalLocation": { "x": 50, "y": 50, "z": 0 },
  "maxSteps": 200,
  "maxTimeSeconds": 300.0
}
```

### CollectItems

Agent must collect specific items:

```json
{
  "type": "CollectItems",
  "requiredItems": ["key-1", "key-2"],
  "maxSteps": 300
}
```

### SurviveTurns

Agent must survive for a minimum number of turns:

```json
{
  "type": "SurviveTurns",
  "minSurvivalTurns": 50,
  "maxSteps": 500
}
```

### CompleteWithinLimits

Agent must complete within step/time limits:

```json
{
  "type": "CompleteWithinLimits",
  "maxSteps": 200,
  "maxTimeSeconds": 300.0
}
```

## Example Benchmarks

### Navigation Basic

See `Data/Benchmarks/navigation-basic.json` for a simple pathfinding test.

### Combat Survival

See `Data/Benchmarks/combat-survival.json` for a combat scenario.

### Puzzle Keys

See `Data/Benchmarks/puzzle-keys.json` for a key-lock chain challenge.

## Versioning

Benchmark versions enable regression detection. When updating a benchmark:

1. Increment the version number
2. Document changes in the description
3. Re-run existing agents to detect regressions

## Generating Variations

Use the API to generate variations of a benchmark:

```powershell
curl -X POST http://localhost:5000/api/benchmark/navigation-basic/variations \
  -H "Content-Type: application/json" \
  -d '{"variationCount": 5, "seedOffset": 1000}'
```

This generates 5 variations with different seeds for stress testing.

