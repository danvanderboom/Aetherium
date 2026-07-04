## Why
The audit's very first recommendation (`docs/audits/IMPROVEMENT_PLAN.md`): "Nothing else is safe to change until a full-solution build and test run gate every commit. The Dashboard sat broken for ~8 months precisely because nothing built the solution." This is item **P2-1**. It had been *deliberately deferred* on a project-cost decision; this change reverses that deferral at the user's explicit request and adds the gate.

## What Changes
- Add `.github/workflows/ci.yml`: on push/PR to `develop`/`main`/`master` (and manual `workflow_dispatch`), restore → build the **whole solution** (`Aetherium.sln`) → run the full test suite (`Aetherium.Test`).
- Runs on `windows-latest` to match the development/test environment exactly (the suite spins Orleans `TestCluster`s and a few spots touch Windows-only APIs), so a green CI run means the same as green locally.
- Installs both the `9.0.x` and `10.0.x` SDKs because the branch TFMs differ (`main`/`master` = net9.0, `develop` = net10.0); caches `~/.nuget/packages`.
- This is additive infrastructure — it changes no product code and no capability behavior, so there is no capability-spec delta. The existing `deploy-server.yml` (Azure deploy on `master`) is untouched.

## Impact
- Affected specs: none (project CI infrastructure; no capability behavior changes).
- Affected code: new `.github/workflows/ci.yml` only.
- Cost note: `windows-latest` bills at 2× minutes; triggers are scoped to the integration branches + PRs + manual dispatch (not every feature-branch push) to bound cost. Switching `runs-on` to `ubuntu-latest` halves the cost once the suite is confirmed green on Linux — left as a follow-up so the first gate is guaranteed to reflect the known-good Windows result.

## Status
Implemented on `feat/phase5-ci` (branched from `develop`). The workflow mirrors the exact local verification steps used throughout Phase 5 (whole-solution Debug build + `Aetherium.Test`), which have been green at 1027 passing. The workflow first executes on the next push to `develop` after this merges.
