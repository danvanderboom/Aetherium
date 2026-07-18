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

            // Only characters (players/monsters) ever carry Dying. Iterate the Characters index
            // (a handful of entries) rather than world.Entities, which on a large outdoor map is
            // ~150k tile entities — scanning all of them every tick cost seconds and starved player
            // input on the single-threaded map grain.
            foreach (var entity in world.Characters.Values)
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
