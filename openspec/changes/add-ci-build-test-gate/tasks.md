## 1. CI workflow
- [x] 1.1 `.github/workflows/ci.yml`: triggers (push/PR to develop/main/master + workflow_dispatch)
- [x] 1.2 Setup both SDKs (9.0.x + 10.0.x); cache `~/.nuget/packages`
- [x] 1.3 Restore → build whole solution (`Aetherium.sln`, Debug) → test (`Aetherium.Test`, `--no-build`)
- [x] 1.4 `runs-on: windows-latest` (matches dev/test env); triggers scoped to bound cost

## 2. Verify
- [x] 2.1 Steps mirror the local verification (whole-solution Debug build + Aetherium.Test, 1027 passing)
- [ ] 2.2 First live run occurs on the next push to `develop` after merge (observe in Actions)

## Follow-up
- [ ] Optional: switch `runs-on` to `ubuntu-latest` (halves cost) once the suite is confirmed green on Linux
