using Aetherium.Core;

namespace Aetherium.Server.Combat
{
    /// <summary>Each world tick, applies every active <see cref="StatusEffect"/>'s <c>OnTick</c>, decrements
    /// its remaining duration, and removes it once expired.</summary>
    public class StatusEffectSystem
    {
        public void Tick(World world)
        {
            if (world == null) return;

            foreach (var entity in world.Entities.Values)
            {
                if (!entity.Has<StatusEffects>())
                    continue;

                var effects = entity.Get<StatusEffects>();
                foreach (var effect in effects.Active)
                {
                    if (effect.RemainingTicks <= 0)
                        continue;

                    effect.OnTick(entity);
                    effect.RemainingTicks--;
                }

                effects.RemoveExpired();
            }
        }
    }
}
