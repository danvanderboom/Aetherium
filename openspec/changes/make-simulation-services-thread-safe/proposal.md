# Make the shared simulation services thread-safe (P1-13)

## Why

`WorldClock`, `WeatherSystem`, and `EventScheduler` are registered as **singletons**
(`Aetherium.Server/Program.cs`) but hold unsynchronized mutable state (plain `double`/
`DateTime` fields, `Dictionary<>`s, and a private `Random`). They are consumed from
`WorldGrain`/`MapRegionGrain`/`SpawnManager`/`PerceptionService`, which run as separate
Orleans activations on the thread pool — so concurrent region ticks call into a single
shared instance at the same time. Consequences:

- **`Dictionary` corruption / crashes** — concurrent `UpdateWeather`/`SetWeather` on
  `WeatherSystem._regionWeather`, and concurrent `Schedule*`/`Process`/`Cancel` on
  `EventScheduler._events`, can throw (`InvalidOperationException`) or wedge the internal
  buckets. `System.Random` is likewise not thread-safe and can silently return degenerate
  values under concurrent use.
- **Corrupt `WorldClock.Tick()` elapsed time** — readers (`GetTimeOfDay`/`GetDay`/…)
  call `UpdateAccumulatedTime()`, which advances the same `_lastTick` field `Tick()` uses.
  If a reader runs between two `Tick()` calls, the next `Tick()` measures only the sliver
  of real time since that reader — not since the previous tick — so `WorldGrain.TickAsync`
  under-reports the game time elapsed for the tick and the sim runs slow/erratically.
- **Weather transitions are per-call, not per-time** — `UpdateWeather` computes
  `hoursSinceChange` but never uses it; the transition probability is a flat per-call roll.
  Weather therefore changes at a rate proportional to tick frequency, not game time.

## What Changes

- **`WorldClock`**: guard all mutable state with a single lock; decouple the `Tick()`
  delta from reader-driven accumulation so `Tick()` always returns the game-time growth
  since the *previous `Tick()`*, regardless of interleaved reads. Pure conversion helpers
  stay lock-free.
- **`WeatherSystem`**: guard `_regionWeather` access with a lock, replace the private
  `Random` with the thread-safe `Random.Shared`, and scale the transition probability by
  `hoursSinceChange` (clamped) so weather changes track elapsed **game time**.
- **`EventScheduler`**: guard `_events`/`_handlers` with a lock, using a
  snapshot-under-lock → await-handlers-outside-lock → update-flags-under-lock pattern in
  `ProcessScheduledEventsAsync` (never holding the lock across an `await`).
- `SeasonManager` is already stateless (pure functions over options + day) — no change.

No public API signatures change. The only observable behavior change is that weather
transition cadence now follows game time instead of call count (a bug fix toward the
already-intended design).

## Impact

- Affected specs: **simulation-runtime** (new capability documenting the concurrency and
  time-based-transition contracts).
- Affected code: `Aetherium.Server/Simulation/WorldClock.cs`,
  `Aetherium.Server/Simulation/WeatherSystem.cs`,
  `Aetherium.Server/Events/EventScheduler.cs`.
- Tests: new concurrency stress tests (parallel access must not throw and must leave
  consistent state) + a `Tick()`-elapsed-vs-readers regression + a weather
  per-game-time-cadence test. Existing `WorldClockTests`/`WeatherSystemTests`/
  `EventSchedulerTests`/integration tests must stay green.
