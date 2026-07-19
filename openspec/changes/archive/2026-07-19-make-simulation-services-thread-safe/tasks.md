# Tasks

## 1. WorldClock
- [x] 1.1 Add a lock guarding `_accumulatedGameTime`/`_lastRealTime`/`_worldStartTime`
- [x] 1.2 Decouple `Tick()` delta from reader accumulation via `_accumulatedAtLastTick`
- [x] 1.3 Keep `RealTime<->GameTime` conversion helpers lock-free (stateless)

## 2. WeatherSystem
- [x] 2.1 Guard `_regionWeather` reads/writes with a lock
- [x] 2.2 Replace private `Random` with `Random.Shared`
- [x] 2.3 Scale transition chance by `hoursSinceChange` (clamped to a valid probability)

## 3. EventScheduler
- [x] 3.1 Guard `_events`/`_handlers` with a lock
- [x] 3.2 `ProcessScheduledEventsAsync`: snapshot under lock, await outside, update flags under lock

## 4. Tests & verification
- [x] 4.1 Concurrency stress tests (parallel access does not throw; state stays consistent)
- [x] 4.2 `WorldClock.Tick()` elapsed-vs-interleaved-readers regression
- [x] 4.3 Weather cadence-follows-game-time test
- [x] 4.4 Full `Aetherium.sln` build (0 errors) + full `Aetherium.Test` suite green (1032 passed / 0 skipped)
