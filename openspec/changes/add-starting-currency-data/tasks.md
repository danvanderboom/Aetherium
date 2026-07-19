## 1. Data model + mapping
- [x] 1.1 Add `StartingCurrency` (`double?`) to `GamePlayerDefinition` (binds `player.startingCurrency`)
- [x] 1.2 Map `definition.Player?.StartingCurrency` → `CreateWorldRequest.StartingCurrency` in `GameDefinitionMapper`
- [x] 1.3 Add `StartingCurrency` to `CreateWorldRequest`, `WorldConfig`, and `WorldTemplate`

## 2. Threading through world creation
- [x] 2.1 Copy the field in both `GameManagementGrain.CreateWorldAsync` paths (IWorldHost template + direct-grain fallback)
- [x] 2.2 Copy `template.StartingCurrency` → `WorldConfig` in `OrleansWorldHost.CreateWorldAsync`
- [x] 2.3 Store on `WorldGrainState` in `WorldGrain.InitializeAsync`; forward from `AddMapAsync` to every map's `InitializeAsync`
- [x] 2.4 Add trailing optional `startingCurrency` param to `IGameMapGrain.InitializeAsync`

## 3. Grain runtime
- [x] 3.1 `GameMapGrain._startingCurrency` field (defaults to `Wallet.StartingCurrency`)
- [x] 3.2 Set it in `InitializeAsync`; persist on `MapState`; rehydrate in `OnActivateAsync`
- [x] 3.3 Apply `_startingCurrency` at the canonical join-time wallet creation in `JoinPlayerAsync`
- [x] 3.4 Add `GetWalletAsync(playerId)` accessor (reads the joined character's `Wallet.Currency`)

## 4. Data + tests
- [x] 4.1 Set `player.startingCurrency: 750` on the `aphelion-h3` bundle to demonstrate the knob
- [x] 4.2 Grain integration tests: purse reaches the inline map and a later-added map; the `CreateWorldRequest` path; absent → engine default
- [x] 4.3 Mapper test asserts `Player.StartingCurrency` → `request.StartingCurrency`

## 5. Validation
- [x] 5.1 `openspec validate add-starting-currency-data --strict` passes with zero errors
- [x] 5.2 Full economy/worldgrain/game-definition test suites green (`dotnet test`)
