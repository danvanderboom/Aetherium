# simulation-runtime Specification

## Purpose

Cross-cutting simulation services (`WorldClock`, `WeatherSystem`, `EventScheduler`) are
process-wide singletons driven concurrently by many Orleans region/world tick activations.
This capability defines their thread-safety and time-progression contracts.

## Requirements

### Requirement: Concurrency-safe shared simulation singletons

`WorldClock`, `WeatherSystem`, and `EventScheduler` SHALL tolerate concurrent access from
multiple region/world ticks without throwing or corrupting internal state. Any randomness
they use SHALL come from a thread-safe source.

#### Scenario: Concurrent weather updates do not corrupt state
- **WHEN** many threads call `UpdateWeather`/`SetWeather`/`GetWeather` for overlapping
  region ids simultaneously
- **THEN** no call throws and every region resolves to a defined `WeatherType`

#### Scenario: Concurrent event scheduling does not corrupt state
- **WHEN** many threads call `ScheduleEventAsync`/`CancelEventAsync`/
  `GetScheduledEventsAsync`/`ProcessScheduledEventsAsync` simultaneously
- **THEN** no call throws and the scheduler's event set stays internally consistent

#### Scenario: Event handlers are awaited without holding the lock
- **WHEN** `ProcessScheduledEventsAsync` invokes a handler that performs async work
- **THEN** the scheduler's lock is not held across the `await` (handlers may re-enter the
  scheduler without deadlocking)

### Requirement: Consistent game-time progression

`WorldClock.Tick()` SHALL return the game time elapsed since the previous `Tick()` call,
independent of any interleaved read calls that also advance accumulated time.

#### Scenario: Readers between ticks do not steal tick elapsed time
- **GIVEN** a clock is ticked once
- **AND** several reads (`GetTimeOfDay`/`GetDay`) occur before the next tick
- **WHEN** `Tick()` is called again
- **THEN** the returned elapsed time reflects the full real time since the previous
  `Tick()`, not merely the time since the last read

### Requirement: Weather transitions track game time

Weather transition probability SHALL scale with the game time elapsed since a region's last
weather change, so transition cadence follows game time rather than the number of update
calls.

#### Scenario: Cadence is independent of update frequency
- **WHEN** a region is updated many times within a short span of game time
- **THEN** the aggregate chance of a weather change reflects the elapsed game time, not the
  raw number of `UpdateWeather` calls
