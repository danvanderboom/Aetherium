## 1. Rename namespaces in the moved contract files
- [x] 1.1 Telemetry → `Aetherium.Model.Telemetry`; Analysis → `Aetherium.Model.Analysis`
- [x] 1.2 Training → `Aetherium.Model.Training`; Narrative → `Aetherium.Model.Narrative`; Pcg → `Aetherium.Model.Pcg`
- [x] 1.3 Strip stale intra-Pcg `using WorldGenCLI.Models;` / `using Aetherium.WorldGen;` (now same-namespace / unused)

## 2. Update consumers
- [x] 2.1 Server/CLI/Console/Test: add the new `using Aetherium.Model.*` (alongside the retained server namespace for the producing logic); insert it into files that referenced moved types via same-namespace
- [x] 2.2 Dashboard: replace old namespaces with `Aetherium.Model.*` (it references only Model)
- [x] 2.3 Fix fully-qualified / partially-qualified stragglers (Quest FQNs, `Models.*` qualifiers, telemetry alias FQNs)
- [x] 2.4 Update the now-stale "namespace retained" explanatory comments

## 3. Verify
- [x] 3.1 Full solution build 0 errors; full suite green (1027 passed / 0 skip); 0 duplicate-using warnings
- [x] 3.2 No `Aetherium.Server.*` / `WorldGenCLI.Models` namespaces remain in `Aetherium.Model.dll`; Dashboard source free of Server/WorldGenCLI type references
