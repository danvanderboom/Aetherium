## Why

Enable faster iteration and designer control over procedural content generation through a visual editor with real-time preview, constraint tweaking, template library, hybrid anchors, and A/B testing capabilities.

## What Changes

- Add REST API to WorldGenCLI for remote generation requests
- Add `ConstraintDescriptor` JSON schema for serializing parameters
- Add `HybridLayout` system for mixing authored and procedural content
- Add enhanced visualization beyond current debug overlays
- Add Blazor-based PCG editor UI in Aetherium.Dashboard
- Add template library for saving/loading PCG configurations
- Add A/B testing capability to generate multiple candidates and select best by metrics

## Impact

- Affected specs: `pcg-tooling`, `world-building`
- Affected code: `WorldGenCLI/Program.cs`, `WorldGenCLI/Api/`, `Aetherium.Server/WorldGen/`, `Aetherium.Dashboard/Pages/`, `Aetherium.Dashboard/Services/`

