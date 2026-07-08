using System.Linq;
using Aetherium.Core;

namespace Aetherium.Server.Combat
{
    /// <summary>Each world tick, ages every <see cref="Corpse"/> entity that also carries a
    /// <see cref="CorpseAge"/> and removes it once <paramref name="policy"/>'s
    /// <see cref="DeathPolicy.CorpseRetentionTicks"/> is reached. A <see cref="Corpse"/> with no
    /// <see cref="CorpseAge"/> is left untouched — it persists forever, unaffected by this system.</summary>
    public class CorpseExpirySystem
    {
        public void Tick(World world, DeathPolicy policy)
        {
            if (world == null || policy == null) return;

            // Snapshot ids first: removing entries from World.Entities while enumerating it throws.
            var expired = world.Entities.Values
                .Where(e => e.Has<Corpse>() && e.Has<CorpseAge>())
                .Select(e =>
                {
                    var age = e.Get<CorpseAge>();
                    age.Ticks++;
                    return (Entity: e, age.Ticks);
                })
                .Where(x => x.Ticks >= policy.CorpseRetentionTicks)
                .Select(x => x.Entity.EntityId)
                .ToList();

            foreach (var entityId in expired)
                world.TryRemoveEntity(entityId);
        }
    }
}
