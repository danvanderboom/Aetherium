## Why
The prior change (`move-contracts-to-model`) removed the Dashboard's *direct* `Aetherium.Server` reference but left it pulling Server **transitively** via `WorldGenCLI` (whose PCG models the PCG page uses). That transitive path also hid several Dashboard→Server contract dependencies the compiler couldn't flag while Server was still on the graph. This change finishes P3-10's decoupling so the Dashboard references **only `Aetherium.Model`** (+ framework packages).

## What Changes
- **Move `WorldGenCLI.Models` to `Aetherium.Model`** (10 self-contained PCG DTO files — `GenerateRequest/Response`, `AbTest*`, `ConstraintDescriptor`, `HybridAnchor/Layout`, `MapRenderDto`, `TemplateDto`, `GeneratorInfo`; no Server/Core deps), namespace `WorldGenCLI.Models` retained. `WorldGenCLI` gains a direct `Aetherium.Model` reference; its own code is unchanged.
- **Drop the `WorldGenCLI` project reference from `Aetherium.Dashboard`** — the Dashboard now references only `Aetherium.Model`.
- **Move the remaining hidden contract dependencies** that the transitive Server path had masked, both consumed by Dashboard pages via HTTP/SignalR:
  - `InterestProfile` (+ `ActionPreference`, `AreaPreference`) — a clean DTO file, moved wholesale.
  - `ReplayData` (+ `ReplayStep`) — the SignalR "ReplayStored" payload. Split out of `ReplayStorage.cs` (storage logic stays in Server). Its `InitialWorldState` (`Aetherium.Core.World`) property is **removed**: it was always null, never read (only set to null in one test), and its engine coupling would have forced `Aetherium.Model` to reference the whole Core engine. Removing it makes `ReplayData` a clean contract.

## Impact
- Affected specs: `training-dashboard` (ADDED: the Dashboard references only shared contracts — no Server, no WorldGenCLI).
- Affected code: `WorldGenCLI/Models/** → Aetherium.Model/Contracts/Pcg/**`; `Aetherium.Server/Agents/Analysis/InterestProfile.cs → Aetherium.Model/Contracts/Analysis/`; `ReplayData`/`ReplayStep` split out of `ReplayStorage.cs` into `Aetherium.Model/Contracts/Telemetry/ReplayData.cs` (minus the dead `World` field); `WorldGenCLI.csproj` (+Model ref), `Aetherium.Dashboard.csproj` (drop WorldGenCLI ref); one test drops the dead `InitialWorldState = null` initializer. Namespaces retained throughout — no `using` churn.
- Build/behavior: the only behavior-affecting change is removing the always-null `ReplayData.InitialWorldState`; everything else is relocation. Full solution build 0 errors; full suite green (1027 passed / 0 skip).

## Status
Implemented on `feat/phase5-worldgencli` (branched from `develop`). The Dashboard's project references are now exactly `{ Aetherium.Model }`; removing the transitive Server path is what surfaced `InterestProfile` and `ReplayData` (previously resolved silently through WorldGenCLI→Server), both now cleanly in Model. Namespaces are still retained (`Aetherium.Server.*`, `WorldGenCLI.Models` living in `Aetherium.Model`); an optional clean re-namespace to `Aetherium.Model.*` remains a separate cosmetic pass.
