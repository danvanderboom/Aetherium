using Aetherium.Core;

namespace Aetherium.Server.Combat
{
    /// <summary>Each world tick, counts down every <see cref="Dying"/> entity and converts it to
    /// <see cref="Corpse"/> once its window elapses.</summary>
    public class DeathSystem
    {
        public void Tick(World world)
        {
            if (world == null) return;

            foreach (var entity in world.Entities.Values)
            {
                if (!entity.Has<Dying>())
                    continue;

                var dying = entity.Get<Dying>();
                dying.TicksRemaining--;

                if (dying.TicksRemaining <= 0)
                {
                    entity.Clear<Dying>();
                    entity.Set(new Corpse());
                }
            }
        }
    }
}
