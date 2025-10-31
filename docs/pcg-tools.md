# Procedural Generation Tooling

Use the `WorldGenCLI` application to reproduce deterministic worlds and export metrics.

```bash
dotnet run --project WorldGenCLI -- \
  --generator AdvancedDungeon \
  --template dungeon \
  --width 60 --height 60 --levels 2 \
  --seed 12345 --version 2.0.0 \
  --param minLoopRatio=0.12 \
  --output artifacts/dungeon-12345.json
```

Key options:

- `--generator`: layout generator name (e.g., `AdvancedDungeon`, `AdvancedOutdoor`).
- `--template`: `dungeon` or `outdoor` to select pipeline passes.
- `--seed`: deterministic seed (omit for random seed).
- `--param`: additional generator parameters (`key=value`). Repeat for multiple values.
- `--output`: optional path to write metrics JSON.

The JSON export contains branching factor, loop ratio, room counts, biome coverage, and per-phase timings for regression tracking.

