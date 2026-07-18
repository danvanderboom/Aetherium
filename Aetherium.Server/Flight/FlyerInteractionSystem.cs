using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Server.Flying
{
    /// <summary>How an affordance reaches its target flyer.</summary>
    public enum FlyerReach
    {
        /// <summary>Band-agnostic, gated by planar range only (e.g. hack over an uplink).</summary>
        Uplink,
        /// <summary>Band-agnostic, gated by planar range only (e.g. summon/hail an air taxi).</summary>
        Signal,
        /// <summary>Planar range plus a small band delta — cannot reach high-altitude flyers (e.g. attack).</summary>
        Weapon,
        /// <summary>Passive observation within sight range (e.g. inspect/tag).</summary>
        Observe
    }

    /// <summary>An affordance a flyer offers an observer, and whether it is currently reachable.</summary>
    public sealed class FlyerAffordanceState
    {
        public string Id { get; init; } = string.Empty;
        public FlyerReach Reach { get; init; }
        public bool Available { get; init; }
        /// <summary>Reason the affordance is out of reach, when <see cref="Available"/> is false.</summary>
        public string? Unavailable { get; init; }
    }

    /// <summary>Result of a flyer interaction attempt.</summary>
    public readonly struct FlyerInteractionOutcome
    {
        public bool Success { get; }
        public string Reason { get; }
        public FlyerInteractionOutcome(bool success, string reason) { Success = success; Reason = reason; }
        public static FlyerInteractionOutcome Ok(string reason) => new(true, reason);
        public static FlyerInteractionOutcome Fail(string reason) => new(false, reason);
    }

    /// <summary>
    /// Altitude-aware flyer interactions: listing the affordances a flyer offers (per its <see cref="FlyerProfile"/>
    /// and the observer's band/range), and resolving hack (uplink, band-agnostic), summon (signal → AdHoc plan +
    /// land), and attack (weapon, gated by a small band delta so orbit is out of reach). Targeting is by range,
    /// not by the rendered field of view — wiring flyers into the perception DTO is deferred to the multi-Z
    /// perception slab in add-adaptive-depth-visualization; <see cref="FlyersInRange"/> is the primitive it will use.
    /// </summary>
    public static class FlyerInteractionSystem
    {
        /// <summary>Default sight range for the passive inspect/observe affordance.</summary>
        public const int ObserveRange = 64;

        /// <summary>Grid (Chebyshev) distance in the horizontal plane, ignoring altitude.</summary>
        public static int PlanarDistance(WorldLocation a, WorldLocation b)
            => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        /// <summary>Absolute difference in altitude band.</summary>
        public static int BandDelta(WorldLocation a, WorldLocation b)
            => Math.Abs(a.Z - b.Z);

        /// <summary>
        /// Flyers carrying a <see cref="FlyerProfile"/> within a planar range of the observer, at any altitude.
        /// This is the range-based discovery primitive; the multi-Z perception slab will fold its results into the
        /// rendered field of view. Iterates <c>world.Characters</c> — never the terrain-inflated <c>world.Entities</c>.
        /// </summary>
        public static IReadOnlyList<Character> FlyersInRange(World world, Character observer, int range)
        {
            var obs = observer.Get<WorldLocation>();
            return world.Characters.Values
                .Where(c => c.EntityId != observer.EntityId && c.Has<FlyerProfile>())
                .Where(c => PlanarDistance(obs, c.Get<WorldLocation>()) <= range)
                .ToList();
        }

        /// <summary>
        /// The altitude-aware affordances <paramref name="flyer"/> offers <paramref name="observer"/>, each with
        /// availability and a reason when out of reach. Physical affordances (attack) are withheld when the flyer
        /// is only reachable in a high band; ranged/uplink affordances (hack) remain available.
        /// </summary>
        public static IReadOnlyList<FlyerAffordanceState> Affordances(World world, Character observer, Character flyer)
        {
            var list = new List<FlyerAffordanceState>();
            if (flyer == null || !flyer.Has<FlyerProfile>())
                return list;

            var p = flyer.Get<FlyerProfile>();
            var obs = observer.Get<WorldLocation>();
            var fl = flyer.Get<WorldLocation>();
            int planar = PlanarDistance(obs, fl);
            int bandDelta = BandDelta(obs, fl);

            if (p.Hackable)
                list.Add(State("hack", FlyerReach.Uplink, planar <= p.UplinkRange, "out of uplink range"));

            if (p.Summonable)
                list.Add(State("summon", FlyerReach.Signal, planar <= p.SignalRange, "out of signal range"));

            if (p.Attackable)
            {
                bool inBand = bandDelta <= p.MaxReachBandDelta;
                bool inRange = planar <= p.WeaponRange;
                list.Add(State("attack", FlyerReach.Weapon, inBand && inRange,
                    !inBand ? "too high to reach" : "out of weapon range"));
            }

            list.Add(State("inspect", FlyerReach.Observe, planar <= ObserveRange, "out of sight"));
            return list;
        }

        private static FlyerAffordanceState State(string id, FlyerReach reach, bool available, string reason)
            => new() { Id = id, Reach = reach, Available = available, Unavailable = available ? null : reason };

        /// <summary>
        /// Hack a flyer over an uplink: band-agnostic, gated only by planar range. On success the flyer is marked
        /// <see cref="Hacked"/> with the observer as controller, so it can be retasked or read.
        /// </summary>
        public static FlyerInteractionOutcome TryHack(World world, Character observer, Character flyer)
        {
            if (flyer == null || !flyer.Has<FlyerProfile>() || !flyer.Get<FlyerProfile>().Hackable)
                return FlyerInteractionOutcome.Fail("Target cannot be hacked");

            var p = flyer.Get<FlyerProfile>();
            if (PlanarDistance(observer.Get<WorldLocation>(), flyer.Get<WorldLocation>()) > p.UplinkRange)
                return FlyerInteractionOutcome.Fail("Target is out of uplink range");

            flyer.Set(new Hacked { ControllerEntityId = observer.EntityId });
            return FlyerInteractionOutcome.Ok($"Hacked {Describe(p)}");
        }

        /// <summary>
        /// Summon/hail an air taxi: it is issued an AdHoc flight plan to the caller's cell at its cruise band. The
        /// tick follower flies it there and, on arrival, its landing arrival behavior sets it down for boarding.
        /// </summary>
        public static FlyerInteractionOutcome TrySummon(World world, Character caller, Character taxi)
        {
            if (taxi == null || !taxi.Has<FlyerProfile>() || !taxi.Get<FlyerProfile>().Summonable)
                return FlyerInteractionOutcome.Fail("Target cannot be summoned");
            if (!taxi.Has<Flight>())
                return FlyerInteractionOutcome.Fail("Target is not a flyer");

            var p = taxi.Get<FlyerProfile>();
            var callerLoc = caller.Get<WorldLocation>();
            var taxiLoc = taxi.Get<WorldLocation>();
            if (PlanarDistance(callerLoc, taxiLoc) > p.SignalRange)
                return FlyerInteractionOutcome.Fail("Target is out of signal range");

            var flight = taxi.Get<Flight>();
            int approachBand = Math.Clamp(flight.CruiseBand, flight.MinBand, flight.MaxBand);

            taxi.Set(new FlightPlan
            {
                Source = FlightPlanSource.AdHoc,
                Loop = LoopMode.Once,
                Legs = new List<WorldLocation> { new WorldLocation(callerLoc.X, callerLoc.Y, approachBand) },
                Cursor = 0,
                Complete = false
            });
            return FlyerInteractionOutcome.Ok($"{Describe(p)} inbound");
        }

        /// <summary>
        /// Attack/shoot a flyer: gated by planar range AND a small band delta, so a grounded attacker can hit a
        /// low-air drone but not a flyer in a high band. Decrements the target's <see cref="Health"/> when present.
        /// </summary>
        public static FlyerInteractionOutcome TryAttack(World world, Character attacker, Character flyer)
        {
            if (flyer == null || !flyer.Has<FlyerProfile>() || !flyer.Get<FlyerProfile>().Attackable)
                return FlyerInteractionOutcome.Fail("Target cannot be attacked");

            var p = flyer.Get<FlyerProfile>();
            var atk = attacker.Get<WorldLocation>();
            var fl = flyer.Get<WorldLocation>();
            if (BandDelta(atk, fl) > p.MaxReachBandDelta)
                return FlyerInteractionOutcome.Fail("Target is too high to reach");
            if (PlanarDistance(atk, fl) > p.WeaponRange)
                return FlyerInteractionOutcome.Fail("Target is out of weapon range");

            if (flyer.Has<Health>())
            {
                var h = flyer.Get<Health>();
                if (h.Level > 0)
                    h.Level--;
            }
            return FlyerInteractionOutcome.Ok($"Hit {Describe(p)}");
        }

        private static string Describe(FlyerProfile p) => string.IsNullOrEmpty(p.Kind) ? "flyer" : p.Kind;
    }

    /// <summary>Convenience factories for the stock flyer kinds, used at spawn and in tests.</summary>
    public static class FlyerProfiles
    {
        public static FlyerProfile Satellite() => new() { Kind = "satellite", Hackable = true, UplinkRange = 256 };
        public static FlyerProfile AirTaxi() => new() { Kind = "air-taxi", Summonable = true, SignalRange = 64 };
        public static FlyerProfile Drone() => new() { Kind = "drone", Attackable = true, WeaponRange = 5, MaxReachBandDelta = 3, Hackable = true, UplinkRange = 12 };
        public static FlyerProfile Bird() => new() { Kind = "bird", Attackable = true, WeaponRange = 4, MaxReachBandDelta = 2 };
        public static FlyerProfile Aircraft() => new() { Kind = "aircraft" };

        /// <summary>Maps a creature-type name to its stock profile, or null when the type has no interaction profile.</summary>
        public static FlyerProfile? ForCreatureType(string? creatureType)
        {
            switch ((creatureType ?? string.Empty).ToLowerInvariant())
            {
                case "satellite": return Satellite();
                case "drone": return Drone();
                case "bird": return Bird();
                case "airtaxi":
                case "air-taxi":
                case "taxi":
                case "dropship":
                case "airship": return AirTaxi();
                case "aircraft":
                case "airplane":
                case "helicopter":
                case "spaceship": return Aircraft();
                default: return null;
            }
        }
    }
}
