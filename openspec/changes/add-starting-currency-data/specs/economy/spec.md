## ADDED Requirements

### Requirement: Per-World Starting Currency

A joining player's opening purse SHALL be per-world authored data, not an engine constant. A game bundle MAY declare `player.startingCurrency` (a number of credits); it SHALL be threaded through world creation (`CreateWorldRequest`, `WorldTemplate`, `WorldConfig`, `WorldGrainState`) to every map the world creates and applied where the canonical join-time `Wallet` is created. When a bundle omits the key, the engine SHALL fall back to the built-in default `Aetherium.Components.Wallet.StartingCurrency`, so a world that never sets one behaves exactly as before. The configured value MUST survive grain reactivation without re-running world creation.

#### Scenario: Bundle declares a starting purse

- **WHEN** a game bundle declares `player.startingCurrency: N`
- **THEN** `GameDefinition.Player.StartingCurrency` holds `N` and the mapper carries it onto `CreateWorldRequest.StartingCurrency`

#### Scenario: Configured purse reaches every map a world creates

- **WHEN** a world is created with a starting currency and later adds another map
- **THEN** a player joining the initial map and a player joining the later-added map each start with a `Wallet` of exactly the configured credits

#### Scenario: Absent key falls back to the engine default

- **WHEN** a world is created without a starting currency
- **THEN** a joining player starts with a `Wallet` of `Wallet.StartingCurrency` credits (500), identical to pre-feature behavior

#### Scenario: Configured purse survives grain reactivation

- **WHEN** a map grain is reactivated after deactivation (its `InitializeAsync` is not called again)
- **THEN** it rehydrates the configured starting currency from persisted `MapState`
- **AND** a player joining after reactivation still receives the configured credits
