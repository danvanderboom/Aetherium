## Why
A joining player's opening purse was the engine constant `Wallet.StartingCurrency` (500 credits), hardcoded at the two wallet-creation sites. Aetherium is an engine, not a game: the starting purse is exactly the kind of value that differs between games (a trade sandbox vs. a survival crawl) and so must be per-world **authored data**, not an engine default. The `Wallet.StartingCurrency` comment already anticipated this ("A per-world data override can come later").

## What Changes
- Add an optional `player.startingCurrency` field to the game-bundle definition (`GamePlayerDefinition`, alongside `player.vision`).
- Thread it through the standard per-world-data path — `CreateWorldRequest` → `WorldTemplate`/`WorldConfig` → `WorldGrainState` → every map's `IGameMapGrain.InitializeAsync` → `MapState` — exactly like `DeathPolicy`, so it reaches every map a world creates and survives grain reactivation.
- Apply it where the canonical join-time `Wallet` is created (`GameMapGrain.JoinPlayerAsync`), falling back to `Wallet.StartingCurrency` when a bundle omits the key (absent = pre-feature behavior, byte-identical).
- Set `startingCurrency: 750` on the `aphelion-h3` planet bundle (a trade sandbox) to demonstrate the knob.

## Impact
- Affected specs: `economy` (NEW capability — seeds the first economy requirement: the per-world starting purse)
- Affected code:
  - `Aetherium.Model/Games/GameDefinition.cs` (`GamePlayerDefinition.StartingCurrency`), `Aetherium.Server/Games/GameDefinitionMapper.cs`
  - `Aetherium.Server/MultiWorld/WorldModels.cs` (`CreateWorldRequest`, `WorldConfig`), `Aetherium.Model/Worlds/WorldContracts.cs` (`WorldTemplate`)
  - `Aetherium.Server/Management/GameManagementGrain.cs`, `Aetherium.Server/Services/OrleansWorldHost.cs`
  - `Aetherium.Server/MultiWorld/WorldGrain.cs` (`WorldGrainState`, `InitializeAsync`, `AddMapAsync`)
  - `Aetherium.Server/MultiWorld/IGameMapGrain.cs` + `GameMapGrain.cs` (`InitializeAsync`, `OnActivateAsync`, `JoinPlayerAsync`, `MapState`, new `GetWalletAsync`)
  - `Aetherium.Server/Components/Wallet.cs` (comment: the const is now the fallback default)
  - `Data/Games/aphelion-h3/game.yaml`
- No breaking changes: the field is nullable and null everywhere preserves the 500-credit default exactly.
