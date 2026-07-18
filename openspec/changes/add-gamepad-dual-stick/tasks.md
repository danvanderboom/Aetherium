## 1. Dual-stick on foot
- [ ] 1.1 Add `RightStick` bindings and rebind `Move` to heading-relative in `InputActions.inputactions` (keep keyboard bindings)
- [ ] 1.2 Replace absolute `Vector2ToDirection` with relative mapping (left stick → `move` F/B/L/R) in `PlayerController`
- [ ] 1.3 Route right-stick X → `rotate` and right-stick Y → `changelevel`
- [ ] 1.4 Implement **X** context get-or-use (query perception for a carriable on the tile → `pickup`, else `use`)
- [ ] 1.5 Wire A (interact/confirm), B (cancel/back), Y (secondary) actions
- [ ] 1.6 Update `docs/unity/README.md` controls reference and on-screen hints

## 2. Held-stick repeat
- [ ] 2.1 Integrate held-stick auto-repeat with action cadence (depends on `add-held-key-repeat-movement`)

## 3. Piloting context
- [ ] 3.1 Add Avatar vs Piloting client control state; toggle on entering/leaving a pilot seat
- [ ] 3.2 In Piloting, route movement tools to the controlled vehicle in 3D (thrust/strafe/yaw/climb/descend)
- [ ] 3.3 Show the altitude gauge while piloting (depends on `add-adaptive-depth-visualization`)

## 4. Accessibility & config
- [ ] 4.1 Remappable bindings; swap-sticks option; invert-Y for turn/climb
- [ ] 4.2 Per-stick dead-zone/sensitivity; comfort cap on repeat rate

## 5. Testing
- [ ] 5.1 PlayMode tests for relative move, right-stick rotate/level, and X context action
- [ ] 5.2 PlayMode tests for Avatar/Piloting context switch
