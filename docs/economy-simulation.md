# Economy Simulation — Design Vision

**Status:** Living design. A cluster-level macro-economy already ticks in production (markets, trade routes, supply-driven prices) but only agents can touch it; players have no currency, shops, or data-driven loot — see §8.
**Scope:** What makes game economies loved or ruinous, Aetherium's layered model from loot tables to living markets, creative leaps, and the maturity ladder.
**Audience:** Engine maintainers, OpenSpec proposal authors, and game/campaign designers building on Aetherium.

---

## 1. Framing

Aetherium is an engine, not a game. A gold-and-shopkeepers fantasy economy, a barter-only survival economy, a corporate-scrip dystopia, and a fully player-driven market game must be the same machinery with different data. Everything below is therefore **per-world declarative configuration** (`EconomyConfig`, threaded through world creation like its four predecessors) with an ECA graduation path. The engine ships zero currencies, zero items, zero prices.

An economy earns its complexity the same way factions do: only when players **change behavior because of it** — hauling goods *there* because they're scarce there, sparing the caravan because trade matters more than loot, picking a fight over a mine. The failure mode to design against is equally clear: the inert vending-machine economy (static prices, infinite stock, gold faucets with no sinks) that every player learns to ignore.

The deepest principle, taken from the games that got this right: **things must be produced, moved, consumed, and destroyed by the simulation itself.** Prices that emerge from real flows are interesting; prices that are numbers in a table are furniture.

## 2. What the beloved systems teach

| System | What it does | The lesson |
|---|---|---|
| **EVE Online** | Fully player-driven markets, regional prices, hauling through dangerous space, everything destructible | **Destruction is the engine of value.** Without sinks that eat items, production makes everything worthless. Geography + risk = arbitrage gameplay, the purest emergent content there is |
| **Path of Exile** | No gold: currency items are themselves crafting tools (an orb *does* something) | **Functional currency.** Money that has intrinsic use has a value floor, resists inflation, and makes every pickup a small decision. The most original economy design of its era |
| **X series / Patrician / Port Royale** | NPC factories consume inputs and produce outputs; NPC traders haul; the economy runs whether or not the player participates | **Simulate flows, not prices.** Producer/consumer chains make shortages *causal* — a raided convoy visibly starves a factory — which makes economic events narratable |
| **Albion Online** | Localized markets with no global auction house; transport is gameplay; nearly everything is player-crafted from gathered resources | **Friction is content.** Instant global markets are convenient and boring; local prices + risky transport turn logistics into a profession |
| **RuneScape (Grand Exchange)** | Order-book market with full price history, accessible to everyone | **Legibility.** Players love economies they can *read* — price charts, trends, visible flows. Opacity kills participation |
| **Animal Crossing (turnips)** | One volatile commodity with a weekly cycle becomes a beloved social ritual | **A single legible volatile good** can carry an entire economy's worth of engagement. Complexity is not the goal; *decision density* is |
| **Recettear / Moonlighter** | The player stands on the merchant's side of the counter | **Both sides of the counter are fun.** Selling, pricing, and reading customers is gameplay, not UI |

Distilled, the five properties a living economy needs — each mapped to the Aetherium asset it builds on:

| Property | In play | Engine asset |
|---|---|---|
| **Real flows** | Goods produced, hauled, consumed, destroyed by simulation | `ClusterGrain` economy tick + trade routes (shipped, agent-only) |
| **Local prices** | Scarcity *here*, glut *there*; arbitrage and hauling | `ClusterEconomyState.Market` per world:map (shipped) |
| **Sinks** | Items break, are consumed, are destroyed; currency exits | Death/drop policy (shipped), crafting (future) |
| **Legibility** | Players can read prices, trends, and causes | Perception + dashboard surfaces |
| **Consequence coupling** | Shortages mint quests; markets obey faction politics | `NarrativeConsequenceEngine` (shipped), factions T4 |

## 3. The layered, composable model

Five layers, each independently data-defined. The contract discipline: economic consumers bind to **resource tags, currency ids, and market events** — never to engine types or hardcoded goods.

```
1. GOODS        resource/currency vocabulary as content-atlas-style tags
                ore · grain · medkit · gold · ration_card  (+ typed metadata: stack, decay, weight)
                    │
2. FLOWS        Producer/Consumer components on entities & terrain
                (mine → ore @ rate · settlement(size N) → consumes grain @ rate)
                    │
3. MARKETS      per-settlement stock, prices from supply/demand pressure,
                faction owner, allowed goods — the player-facing counter
                    │
4. TRANSPORT    trade routes as world-graph edges with capacity + carriers
                (caravan NPCs, portals); blockable, raidable, escortable
                    │
5. EVENTS       MarketEvents (shortage, glut, embargo, price_shock) emitted as
                a tag family → quests, faction moves, live events subscribe
```

Design rules that keep this composable:

- **Currency is data, plural.** `EconomyConfig` declares any number of currencies (`gold`, `credits`, `ration_cards`), each optionally *functional* (usable as a reagent/crafting input — the PoE lesson, available to every game as a YAML flag). The engine never assumes one currency, or any.
- **Loot is the economy's front door and must be table-driven.** Drop tables (already present as narrative `LootTable`/`LootEntry` types, unwired) become per-world data resolved at the existing `SpawnMonsterLoot` chokepoint. Every hardcoded drop is a bug against this design.
- **Markets emit events; they never call other systems.** `market:shortage:grain@rivertown` is a tag on the same bus as `kill:` tags. Quests, doctrines, and the live-event director subscribe. This keeps economy ↔ narrative ↔ factions coupling declarative and one-directional.
- **The macro and micro tiers meet at the market, not in each other's internals.** The shipped cluster tick (macro flows between markets) and player trades (micro flows at one market) both mutate the same market state and nothing else of each other's.
- **Faction ownership gates by band.** A market's prices/access read the buyer's standing band with the owning faction (`friendly` discount, `hostile` embargo) — bands, never raw numbers, per the factions contract.

## 4. Creative leaps

1. **LLM merchants with real ledgers.** The agent infrastructure means a shopkeeper can be an LLM agent whose *negotiation is talk but whose trades are tools* — it can haggle, gossip about what's scarce upriver (reading real market events), buy low because it genuinely knows a caravan failed, and cannot hallucinate gold it doesn't have because every trade clears through the market grain. Bartering with something that actually *wants* things is an experience no shipped game has.
2. **The full interdiction loop as emergent content.** Caravans are already spawnable entities; routes are already graph edges. Close the loop — raided caravan → cargo actually lost → destination market shortage → prices spike → consequence engine mints an escort quest → factions judge who did the raiding — and the engine generates the "player-caused famine" stories players retell, from five data-driven systems composing.
3. **Economy as narrative pressure, by contract.** Because market events are tags, the *narrative* system decides what a shortage means (quest, festival, migration, war). Scarcity becomes a storytelling input rather than an inventory inconvenience.
4. **Functional currency as an engine primitive.** Any world can declare that its currency *does something* (heals, enchants, fuels casting — a `use_item`/ability bridge). This single flag lets designers build PoE-style barter economies, consumable-money survival games, or classic gold games from the same machinery.
5. **Legibility as a perception feature.** Price boards, trend glyphs, and trade-flow overlays are semantic perception data — so the screen-reader client can *speak the market*, agents can trade on it, and every renderer draws it natively. An accessible economy is a first.

## 5. Maturity ladder

| Tier | Ships | Depends on |
|---|---|---|
| **T0 — Goods & loot substrate** | `EconomyConfig` (currencies, resource tags, loot tables); wallet component stamped at join; data-driven drops at the `SpawnMonsterLoot` chokepoint; currency pickups | Kill/loot chokepoint (shipped) |
| **T1 — The counter** | Vendor interaction (buy/sell at a market's prices); market stock/prices readable through perception; simple faucet/sink accounting surfaced in telemetry | T0; cluster markets (shipped) |
| **T2 — Real flows** | `Producer`/`Consumer` components driving market stock; the shipped cluster tick becomes the mover of player-visible goods; local price divergence | T1 |
| **T3 — Transport as gameplay** | Carriers as killable/escortable entities carrying real cargo; route blockage → shortage; `market:` event tag family emitted | T2; live caravans (partially shipped, orphaned) |
| **T4 — Politics & players inside** | Faction market ownership (band-gated prices/access/embargoes); player shops as world entities; crafting as flow participation; consequence engine subscribing to `market:` events | T3; factions T4 |
| **T5 — Scripted & living merchants** | Market policies as ECA graphs (dynamic tariffs, festivals); LLM merchant/guild agents trading through tools | T0–T4; ECA runtime |

## 6. The ECA graduation path

A price rule is already a degenerate ECA rule: `WHEN supply_changes THEN price = base × pressure`. The generalization:

1. **Conditions on market behavior** — `when: market:shortage:grain, where: [severity > 0.5, owner.at_war == false], then: [raise_buy_price(grain, 2.0), post_bounty(supply_grain)]`.
2. **Action lists beyond prices** — mint quests, shift faction dispositions, spawn caravans, trigger live events.
3. **Market brains as ECA graphs** — a settlement's economic personality (hoarder, free port, war profiteer) authored in the visual palette.
4. **LLM economic actors** — merchant princes and trade guilds as LLM agents emitting decisions as authored trades and ECA rules: auditable, rate-limited, incapable of counterfeiting because every mutation clears through market grains.

## 7. Anti-goals

- **No global auction house in the engine.** Local markets are the primitive; a game *may* build a global exchange as content, but instant frictionless trade is not the default (the Albion lesson).
- **No engine-blessed currency.** Not even as a default name.
- **No prices without flows** at T2+. Static price lists are a T0/T1 convenience, not the destination.
- **No hidden economy.** Every faucet and sink is telemetry-visible to designers; opacity is how inflation ruins games (the WoW lesson).
- **No economy content in the engine.** Goods, currencies, tables, and market personalities are campaign data.

## 8. Current state

- **Shipped (macro, agent-facing):** `ClusterGrain` + `ClusterEconomyState` — per-map markets auto-registered, `ResourcePricing` (base/current price, supply/demand), trade routes and transport schedules, and a 5-minute economy tick that moves quantities between markets and adjusts supply-driven prices. Driven today only by agent tools (`CreateTradeRouteTool`, `ScheduleTransportTool`); spec'd in `openspec/specs/multiworld/spec.md`. REST read surface at `/api/cluster`.
- **Shipped (fragments):** capacity-10 `Inventory` component (no value/stack/rarity); narrative `LootTable`/`LootEntry` types plus `ContextualLootGenerator` — none wired to actual drops; `MerchantCaravanHandler` can spawn caravan entities (orphaned, see [live-events.md](live-events.md)).
- **Absent:** any currency/wallet type, shops/vendors, crafting, data-driven drops (`SpawnMonsterLoot` hardcodes a sword), player-facing prices.
- **The gap in one sentence:** a real macro-economy already ticks with nobody in it; T0–T1 connect players to it through loot tables, wallets, and a vendor counter — wiring slices on existing chokepoints, the established pattern.
