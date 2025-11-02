## Why
Extend `aetherctl` to cover remaining operator workflows from the plan: agent/session lifecycle convenience commands and PNG rendering for worldgen previews.

## What Changes
- Add agent/session list/create/close commands to `aetherctl` (server-side dependent)
- Add `worldgen render --png <path>` using SkiaSharp (Phase 2)

## Impact
- Affected specs: world-building, pcg-tooling
- Affected code: Aetherctl CLI commands and optional SkiaSharp rendering module

