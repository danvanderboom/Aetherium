## Why

`add-abilities` (Wave 1 §4.3) shipped `Ability`/`AbilityCatalog`/`ResourcePool(s)`/`IAbilityEffect` as pure, unreferenced data primitives — confirmed by grep, nothing outside `Aetherium.Server/Abilities/` and its own tests touches them. No entity carries `ResourcePools`. No command path casts an ability.

But those primitives are only half of what "abilities" means for an *engine*. `Ability` conflates **data and behavior**: its `Effects` are live `IAbilityEffect` instances holding injected services (`DamagePipeline`, `IHitResolver`) and `DamagePacket`s — you cannot serialize that onto `MapState` or ride it through a world-creation contract. So the primitive, as shipped, cannot be per-world content. Aetherium is a game engine, not a game: a fantasy world's fireball and a sci-fi world's net-hack must both be **data a world declares**, never a set the engine hardcodes.

This change closes that gap the same way `wire-death-respawn-live` made `DeathPolicy` per-world data: introduce the missing serializable **data tier**, thread it through world creation, and build the live cast path that consumes it.

## What Changes

- **New data tier (`Aetherium.Model.Abilities`):** `AbilityDefinition` (pure `[GenerateSerializer]` data), `AbilityEffectDescriptor` (a `Kind` + per-kind fields, mirroring `RespawnLocationPolicy`'s Mode+optional-fields shape — no polymorphic serialization), `ResourcePoolDefinition`, and an `AbilityConfig` bundle carrying a world's abilities and the resource pools its characters start with. Lives in `Aetherium.Model` (like `DeathPolicy`) so it's reachable from both `WorldConfig` and `WorldTemplate`.
- **New compiler (`Aetherium.Server.Abilities.AbilityCompiler`):** turns `AbilityDefinition[]` into a runtime `AbilityCatalog` (binding the map's `DamagePipeline`/`IHitResolver` into `DealDamageEffect` et al.) and `ResourcePoolDefinition[]` into a fresh `ResourcePools` component. The existing `Ability`/effects become the *compiled/runtime* tier; this is the data/seeding split `ContentAtlas` already established.
- **Per-world threading:** `AbilityConfig` rides `WorldConfig`/`WorldTemplate`/`CreateWorldRequest` → `WorldGrainState` → `AddMapAsync` → `IGameMapGrain.InitializeAsync` → persisted on `MapState`, rehydrated in `OnActivateAsync`. **The engine ships zero abilities**; tests (and games) supply their own definitions.
- **Live cast path:** `Character` construction gains a default `ActionSpeed`; `JoinPlayerAsync` stamps the world's configured resource pools onto the joining character. New `IGameMapGrain.UseAbilityAsync(sessionId, abilityId, targetEntityId?)`, gated by `IsActionable`, an `AbilityCooldowns` component, resource affordability, single-target reach (when targeted), and a flat `ActionSpeed` spend; on success it applies the ability's effects and fans out any resulting deltas.
- **Tick + observability:** `TickAsync` counts down `AbilityCooldowns` and regenerates every live `ResourcePools` (using `ThreatTable` presence as the in-combat proxy). New `GetResourcePoolsAsync`/`GetAbilityCooldownsAsync` read accessors, mirroring `GetDeathPolicyAsync`.

## Impact

- Affected specs: `abilities` (new live-wiring requirements), `engine-core` (Character now carries `ActionSpeed`).
- Affected code: `Aetherium.Model/Abilities/*` (new), `Aetherium.Server/Abilities/*` (new compiler + cooldowns component; `ResourcePools` gains pool enumeration for regen), `Aetherium.Server/Entities/Character.cs`, `Aetherium.Server/MultiWorld/{GameMapGrain,IGameMapGrain,WorldGrain,WorldModels}.cs`, `Aetherium.Model/Worlds/WorldContracts.cs`, `Aetherium.Server/Management/GameManagementGrain.cs`, `Aetherium.Server/Services/OrleansWorldHost.cs`.
- Explicitly deferred (see design.md Non-Goals): **phased charge/cast/recover execution** (schema carries the timing fields now; instant execution ships this slice, the `CastInProgress` state machine is the next slice), per-ability AP cost, NPC/monster ability use, AOE/shape targeting, `SkillDefinition.UnlocksAbilityId` grants, `Teleport`/`Spawn`/`Summon`/`TriggerNarrativeEvent` effects, and a client push signal (read accessors only this slice).
