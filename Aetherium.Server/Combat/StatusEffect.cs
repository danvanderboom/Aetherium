using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;

namespace Aetherium.Server.Combat
{
    /// <summary>
    /// An entity-on-entity effect with its own tick behavior (engine gap-analysis §4.2). Identified
    /// by <see cref="Id"/> for stacking purposes — applying an effect whose <see cref="Id"/> matches
    /// an already-active one refreshes its duration (the "refresh" stacking rule; unique/N-stack/
    /// additive rules are a future extension point, not needed by the current three effects).
    /// </summary>
    public abstract class StatusEffect
    {
        public string Id { get; }
        public int RemainingTicks { get; internal set; }

        protected StatusEffect(string id, int durationTicks)
        {
            Id = id;
            RemainingTicks = durationTicks;
        }

        /// <summary>Applies this tick's effect to <paramref name="target"/>. Called once per world tick
        /// while <see cref="RemainingTicks"/> is positive.</summary>
        public abstract void OnTick(Entity target);
    }

    /// <summary>Deals <see cref="DamagePerTick"/> thermal-style damage each tick, then expires.</summary>
    public class BurningEffect : StatusEffect
    {
        public double DamagePerTick { get; }

        public BurningEffect(int durationTicks, double damagePerTick) : base("burning", durationTicks)
        {
            DamagePerTick = damagePerTick;
        }

        public override void OnTick(Entity target)
        {
            if (!target.Has<Aetherium.Components.Health>())
                return;

            var health = target.Get<Aetherium.Components.Health>();
            health.Level = System.Math.Max(0, health.Level - (int)System.Math.Round(DamagePerTick));
        }
    }

    /// <summary>Marker effect: reduces the afflicted actor's effective action speed. Carries the
    /// multiplier for consumers (e.g. a future ActionSystem/NPC-AI integration) to read; applying it
    /// here has no side effect of its own beyond ticking down.</summary>
    public class SlowedEffect : StatusEffect
    {
        public double SpeedMultiplier { get; }

        public SlowedEffect(int durationTicks, double speedMultiplier) : base("slowed", durationTicks)
        {
            SpeedMultiplier = speedMultiplier;
        }

        public override void OnTick(Entity target) { }
    }

    /// <summary>Marker effect: restricts the afflicted actor's available action types. Consumers (NPC
    /// AI, a future action-type gate) check for its presence; this effect itself has no tick side effect.</summary>
    public class ProneEffect : StatusEffect
    {
        public ProneEffect(int durationTicks) : base("prone", durationTicks) { }

        public override void OnTick(Entity target) { }
    }

    /// <summary>An actor's active status effects.</summary>
    public class StatusEffects : Component
    {
        private readonly List<StatusEffect> _active = new();

        public IReadOnlyList<StatusEffect> Active => _active;

        /// <summary>Applies <paramref name="effect"/>; an existing effect with the same <see cref="StatusEffect.Id"/>
        /// is replaced (refreshing its duration) rather than stacking.</summary>
        public void Apply(StatusEffect effect)
        {
            _active.RemoveAll(e => e.Id == effect.Id);
            _active.Add(effect);
        }

        public bool Has(string id) => _active.Any(e => e.Id == id);

        public bool TryGet(string id, out StatusEffect? effect)
        {
            effect = _active.FirstOrDefault(e => e.Id == id);
            return effect is not null;
        }

        internal void RemoveExpired() => _active.RemoveAll(e => e.RemainingTicks <= 0);
    }
}
