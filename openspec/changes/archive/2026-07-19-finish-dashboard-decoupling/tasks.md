## 1. Move WorldGenCLI.Models to Aetherium.Model
- [x] 1.1 `git mv` the 10 PCG DTO files → `Aetherium.Model/Contracts/Pcg/` (namespace `WorldGenCLI.Models` retained)
- [x] 1.2 Add `Aetherium.Model` project reference to `WorldGenCLI`

## 2. Move the remaining hidden Dashboard→Server contracts
- [x] 2.1 `InterestProfile` (+ `ActionPreference`, `AreaPreference`) → `Aetherium.Model/Contracts/Analysis/`
- [x] 2.2 `ReplayData` + `ReplayStep` split out of `ReplayStorage.cs` → `Aetherium.Model/Contracts/Telemetry/ReplayData.cs`
- [x] 2.3 Remove the dead `ReplayData.InitialWorldState` (`Core.World`) field + the test initializer that set it

## 3. Drop the Dashboard→WorldGenCLI reference
- [x] 3.1 Remove `WorldGenCLI` `ProjectReference` from `Aetherium.Dashboard.csproj` (now references only `Aetherium.Model`)

## 4. Verify
- [x] 4.1 Full solution build 0 errors; full suite green (1027 passed / 0 skip)
- [x] 4.2 `Aetherium.Dashboard.csproj` project references == `{ Aetherium.Model }`
