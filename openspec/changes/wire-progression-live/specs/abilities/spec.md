## MODIFIED Requirements

### Requirement: Ability Resource & Cooldown Gating
`UseAbilityAsync` SHALL reject a cast, without mutating any state, when: the caster carries `Downed` or `Corpse`; the ability id is unknown to the map's catalog; **the world's `RequireSkillToCastAbilities` is true and the ability is not in the caster's `GrantedAbilities`**; the ability is on cooldown for that caster (tracked per-caster in an `AbilityCooldowns` component); the ability's resource pool is absent or cannot afford its `ResourceCost`; a targeted ability's target is missing, has no location, or is beyond the ability's range; or the caster's `ActionSpeed` budget cannot afford the cast. Resource and action-budget SHALL be committed only after every gate passes. A successful cast SHALL place the ability on cooldown for its `Cooldown` duration.

When `RequireSkillToCastAbilities` is false (the default), catalog membership remains the sole learned-status gate — preserving the pre-progression behavior in which any catalog ability is castable.

**Verified by:** `Aetherium.Test.Abilities.AbilityCastTests.Cast_WhileDowned_IsRejected`, `.Cast_OnCooldown_IsRejected`, `.Cast_InsufficientResource_IsRejected_AndPoolUnchanged`, `.Cast_TargetOutOfReach_IsRejected`, `.Cast_Success_PutsAbilityOnCooldown`, `Aetherium.Test.Progression.ProgressionLiveTests.RequireSkillToCastAbilities_True_GatesUngrantedCast_AllowsGranted`, `.RequireSkillToCastAbilities_False_AnyCatalogAbilityCastable`

#### Scenario: A cast on cooldown is rejected
- **WHEN** a player casts an ability with a nonzero cooldown, then casts it again before the cooldown elapses
- **THEN** the second cast is rejected and no resource is spent

#### Scenario: An unaffordable cast leaves the pool untouched
- **WHEN** a player casts an ability whose `ResourceCost` exceeds the caster's pool `Current`
- **THEN** the cast is rejected and the pool's `Current` is unchanged

#### Scenario: Skill-gated casting is enforced only when the world opts in
- **WHEN** `RequireSkillToCastAbilities` is true and a player casts a catalog ability not in their `GrantedAbilities`
- **THEN** the cast is rejected as not-yet-learned; the same cast succeeds once the granting skill is unlocked; and with the flag false the cast is allowed on catalog membership alone
