# Engine Gap Analysis: What Aphelion Needs From Aetherium

*Part of the [Unity sample design suite](README.md). Status: analysis against the engine as of 2026-07-16 (develop @ df65e5d).*

The design brief allows adding a few key engine features "within reason." This document is the honest ledger: what the game needs that the engine doesn't do, what we recommend building versus working around, and what we explicitly defer. Each gap is sized (S ≈ a day-scale slice, M ≈ a normal OpenSpec slice, L ≈ multiple slices) and mapped to the milestone that needs it ([milestones.md](milestones.md)).

**The headline is how short this list is.** Multiplayer visibility, per-player FOV/lighting, vision modes, heat trails, z-levels, doors/keys/affordances, data-driven creatures/items/spawns, ECA rules, abilities, factions, progression, death policy, audio perception hints — all shipped. The gaps cluster in one place: **the perception protocol tells clients slightly less than a rich visual client wants to show.**

## First, the non-gaps (verified working — no engine change)

| Looked like a gap | Reality |
|---|---|
| "How does a 3D client know a monster is a `custodian`?" | Creature identity rides the wire as tile name `Creature:<contentId>` with glyph/colors — theme binding works today |
| "Can two clients share a station?" | Yes — grain-bound sessions on the same map see each other join/move/leave; end-to-end tested |
| "Does audio have server support?" | `PerceptionDto.Audio` already carries biome, danger level, reverb, occlusion, ambient emitters, suggested music, footstep material |
| "Do stations need a new deck concept?" | z-levels + `changelevel` are native; `world.size.depth: 3` gives three decks |
| "Down-state/respawn for co-op?" | Death policy per bundle + `ReceiveDowned/Respawn/Died` events are wired |
| "Can operators stand up stations?" | `aetherctl game create aphelion` / management REST — shipped |

## The gaps

### G1 — Self state in perception *(S/M · needed by M0)*

**Need:** a HUD. Today a client learns its own health only from downed/died events and its inventory from perception — there is no self health/max, no active statuses, no resource pools (charge/stamina), no cooldowns in any frame.
**Recommendation:** add a `Self` block to `PerceptionDto` (health/max, active status ids with remaining ticks, resource pool values/max, ability cooldown remainders). Pure protocol read-model over state the grain already owns; no gameplay change; per-world-data neutral.
**Workaround if deferred:** HUD infers HP from attack-received deltas — unacceptably lossy; this gap is the one true M0 blocker.

### G2 — Enemy presentation state *(S · M1)*

**Need:** enemy health bars, status VFX on others, and clean identity. `CharacterDto` today: id, tile, hostile flag, relative location — no health fraction, no statuses, and the creature id must be parsed out of `"Creature:<id>"`.
**Recommendation:** add `CreatureTypeId` (first-class string), `HealthFraction` (0–1, coarse — a fraction leaks less than absolute values), and `ActiveStatusIds`. Respect the existing privacy stance: other *players'* heading stays hidden; a `FacingDegrees` for non-player creatures only.
**Workaround until then:** attacker-only HP from attack results (the store already folds this); status VFX limited to self.

### G3 — Ability casting as a player tool *(S · M1)*

**Need:** the Reclaimer kit. `GameMapGrain.UseAbilityAsync` is wired (combat slice) but no `ability` tool exists in the player-profile registry, so `ExecuteTool` can't reach it.
**Recommendation:** an `ability` tool (`abilityId`, `targetEntityId?`) delegating to the grain method, plus ability/pool state via G1's `Self` block. Small because both ends exist; the tool is the missing middle.

### G4 — Absolute-direction movement through the grain gateway *(S · M1, workaround at M0)*

**Need:** WASD in a top-down game means "move north," not "rotate then advance." `move` with `N/E/S/W` currently errors through the grain gateway ("Absolute directions require async execution via management grain"); only relative `F/B/L/R` work.
**Recommendation:** make `MoveTool` resolve absolute→(rotate+step) atomically server-side through the gateway.
**Workaround (M0):** the client library's composite `MoveAsync(direction)` issues the rotate+move pair; slightly chattier, same result; API unchanged when the fix lands.

### G5 — Vision-mode wire vocabulary *(S · M1)*

**Need:** the sonar ping is a signature beauty feature. The `setvisionmode` tool advertises `Normal/Infrared/UltraViolet/Sonar`, but the wire enum carries only `Normal=0, Infrared=1` — a real advertised-vs-representable inconsistency.
**Recommendation:** extend the wire enum (append-only, integer-stable) and reconcile the tool's allowed values; audit `setlightingmode`'s `Darkness` the same way. Worth doing as a correctness fix regardless of Aphelion.

### G6 — Player-facing instance lifecycle *(S/M · M1)*

**Need:** "Host a station" in the lobby UI. Instance creation lives on the operator plane (aetherctl / API-key-gated REST); `GameHub` can list/join but not create.
**Recommendation:** `CreateGameInstance(gameDefinitionId, name?)` on `GameHub`, delegating to the management grain with a per-definition allowlist flag (bundle manifest opt-in, e.g. `allowPlayerInstances: true`) so operators keep control of what players can spin up. That flag is per-game data, honoring the per-world-data principle.

### G7 — Co-op revive interaction *(M · M2)*

**Need:** the design's "channel-revive a downed teammate." Down-state and the revive *window* exist in the death policy; what's missing is the verb — no `revive` tool, and the exact semantics of window expiry need verifying against the shipped death flow first.
**Recommendation:** a `revive` interaction tool (adjacency + downed-target precondition, channel time as ticks) as a normal OpenSpec slice. Until then the design degrades gracefully: down-state ends in respawn at the dock.

### G8 — Objectives as bundle data *(M/L · M2)*

**Need:** server-authoritative goals — *restore three relays, then extract* — shared by the party, so a run has a win condition the server owns.
**Recommendation:** an `objectives:` bundle section (id, description, completion trigger, reward hooks) riding the ECA maturity ladder — objective completion is naturally an ECA trigger/action pair, and the reflectable-vocabulary pattern extends to objective conditions. This is the next big per-world-data family, worth its own proposal.
**Until then:** M0/M1 runs use a client-orchestrated goal (extract with salvage; score screen) — honest about being presentation-only.

### G9 — Station worldgen generator *(M · M2)*

**Need:** decks that read as a space station — rooms off spine corridors, bulkhead-gated sections, a docking bay, a reactor room, window walls on the hull perimeter.
**Recommendation:** a `station` generator (or a parameterized dungeon-generator profile) in the existing registry — the pipeline (layout → theming → population → validation) needs no structural change; population passes already consume `ContentConfig`. Until then `rooms-and-corridors`/`maze` are serviceable stand-ins with station *dressing* carried by the theme.

### G10 — Status-effect ticking *(M · M2)*

**Need:** `burning` that actually burns per tick, hazard floors, and the stasis/overheat feel. Statuses today are canonical *state* (applied, timed) but not tick-*processed* — a known pre-existing engine gap, unchanged by the ECA slice.
**Recommendation:** the already-anticipated status-processing slice in the world tick (damage-over-time, expiry events), which also unlocks hazard terrain. Aphelion M0/M1 presents statuses visually and via their existing mechanical hooks only.

### G11 — Per-world simulation options *(S/M · M2, principle item)*

**Need:** station power cycles (the day/night reskin) and "no weather indoors." `DayLengthMinutes`, `EnableWeather`, `EnableSeasons`, `TickHz` are **server-global** `SimulationOptions` — the one place the per-world-data principle is currently violated. Aphelion wants weather off and a short "power cycle" period; Emberfall wants standard days and weather — on the same server.
**Recommendation:** lift the relevant options into an optional bundle `simulation:` section threaded per-world (the established WorldConfig recipe), falling back to server defaults. Modest, and it repays the engine's own principle.

### G12 — Presentation hints in bundles *(S · later, optional)*

**Need (speculative):** letting a bundle *suggest* client presentation (model/palette/music-set hints) so a mod is playable in a generic client without a bespoke theme.
**Recommendation:** defer. The ThemeAsset mapping keyed by content ids is cleaner (presentation stays client-side; the engine stays semantic), and `TileTypeDto.Settings` is already an open string dictionary if a hint channel is ever wanted. Revisit only when a second visual client exists.

## Summary table

| # | Gap | Size | Milestone | Type |
|---|---|---|---|---|
| G1 | Self state in perception (HP, statuses, pools, cooldowns) | S/M | **M0** | Protocol |
| G2 | Enemy presentation state (type id, HP fraction, statuses) | S | M1 | Protocol |
| G3 | `ability` player tool | S | M1 | Tool registry |
| G4 | Absolute-move via grain gateway | S | M1 (client workaround M0) | Tool/grain |
| G5 | Vision-mode wire enum completion | S | M1 | Protocol fix |
| G6 | Player-facing instance creation (+ bundle opt-in flag) | S/M | M1 | Hub + data |
| G7 | Revive interaction | M | M2 | Gameplay slice |
| G8 | `objectives:` bundle section | M/L | M2 | New data family |
| G9 | `station` generator | M | M2 | Worldgen |
| G10 | Status-effect ticking | M | M2 | Simulation |
| G11 | Per-world `simulation:` options | S/M | M2 | Data principle |
| G12 | Bundle presentation hints | S | later | Deferred |

Total engine ask for a *playable, beautiful* M0: **one small protocol slice (G1)**. Everything else either has a clean client-side workaround or belongs to later milestones — each would go through the normal OpenSpec change flow when its milestone arrives.
