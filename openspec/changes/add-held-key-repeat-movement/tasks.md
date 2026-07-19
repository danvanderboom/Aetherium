## 1. Phase 1 — ActionCadence + server enforcement
- [ ] 1.1 Add an `ActionCadence` component (`MovesPerSecond`, `LastActionGameTime`; `Interval = 1 / MovesPerSecond`)
- [ ] 1.2 Author cadence as data: per-world default with a per-entity override, threaded through world creation
- [ ] 1.3 Rate-limit / coalesce `move`/`rotate`/`changelevel` in the action path (`MoveTool` → `GameSession.MoveView`, plus rotate and changelevel) using `WorldClock` game-time stamps
- [ ] 1.4 Surface the current cadence / interval in the client-facing perception/HUD payload
- [ ] 1.5 Pace the `FlightPlan` follower's leg-stepping with the same cadence clock
- [ ] 1.6 Tests: an early action is coalesced/deferred (not doubly applied); cadence is present in the payload; the flight-plan follower steps at cadence

## 2. Phase 2 — Unity held-repeat
- [ ] 2.1 Track pressed/held state for move/rotate/change-level (`started` → held → `canceled`) instead of one-shot `performed`
- [ ] 2.2 In `Update()`, re-issue the held action every `Interval`, reading the interval from the latest perception (with a sane fallback until the first frame arrives)
- [ ] 2.3 Suppress repeat during option-selection mode (existing `isChoosingOption` guard)
- [ ] 2.4 PlayMode tests: holding forward repeats at the character rate; releasing stops; repeat disabled while choosing an option

## 3. Phase 3 — Console held-repeat
- [ ] 3.1 Add a key-down repeat loop that re-issues the held movement key at `Interval` until released
- [ ] 3.2 Handle console key-up detection with a short key-repeat grace window; stop on any other key or on timeout
- [ ] 3.3 Tests: a held key re-issues the move at cadence; release stops it

## 4. Phase 4 — (optional) client prediction & reconciliation
- [ ] 4.1 Optimistically advance the local player marker on send
- [ ] 4.2 Reconcile against the next authoritative perception
- [ ] 4.3 Document the feel-vs-latency trade-off

## 5. Validation
- [ ] 5.1 `openspec validate add-held-key-repeat-movement --strict` passes with zero errors
