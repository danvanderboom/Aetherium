using System;
using Aetherium.Core;

namespace Aetherium.Server.Combat
{
    public readonly struct HitResult
    {
        public bool Hit { get; }
        public bool Critical { get; }

        public HitResult(bool hit, bool critical)
        {
            Hit = hit;
            Critical = critical && hit;
        }

        public static readonly HitResult Miss = new(false, false);
    }

    /// <summary>Pluggable hit resolution (engine gap-analysis §4.2). The engine ships two: deterministic
    /// (always lands, matching the original melee MVP) and probabilistic (accuracy vs evasion, seedable RNG).</summary>
    public interface IHitResolver
    {
        HitResult ResolveHit(Entity attacker, Entity target);
    }

    /// <summary>Always hits, never crits — the original melee MVP's behavior, kept as an explicit resolver
    /// so games that want deterministic combat can opt into it rather than losing the option.</summary>
    public class AlwaysHitResolver : IHitResolver
    {
        public HitResult ResolveHit(Entity attacker, Entity target) => new(hit: true, critical: false);
    }

    /// <summary>Attacker <see cref="Accuracy"/> vs target <see cref="Evasion"/>, then an independent
    /// <see cref="CritChance"/> roll on a hit. Takes an injected <see cref="Random"/> so tests and
    /// per-tick world RNG stay seedable/deterministic given (seed, input-log).</summary>
    public class RollHitResolver : IHitResolver
    {
        public const double DefaultAccuracy = 0.9;
        public const double DefaultEvasion = 0.05;
        public const double DefaultCritChance = 0.05;

        private readonly Random _random;

        public RollHitResolver(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public HitResult ResolveHit(Entity attacker, Entity target)
        {
            double accuracy = attacker.Has<Accuracy>() ? attacker.Get<Accuracy>().Chance : DefaultAccuracy;
            double evasion = target.Has<Evasion>() ? target.Get<Evasion>().Chance : DefaultEvasion;
            double hitChance = Math.Clamp(accuracy - evasion, 0.05, 0.99);

            bool hit = _random.NextDouble() < hitChance;
            if (!hit)
                return HitResult.Miss;

            double critChance = attacker.Has<CritChance>() ? attacker.Get<CritChance>().Chance : DefaultCritChance;
            bool critical = _random.NextDouble() < critChance;

            return new HitResult(hit: true, critical);
        }
    }
}
