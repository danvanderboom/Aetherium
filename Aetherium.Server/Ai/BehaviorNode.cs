using Aetherium.Core;

namespace Aetherium.Server.Ai
{
    public enum BehaviorStatus
    {
        Success,
        Failure,
        Running,
    }

    /// <summary>Everything a node needs to evaluate one tick: the world, the acting entity, and its blackboard.</summary>
    public class BehaviorContext
    {
        public World World { get; }
        public Entity Self { get; }
        public Blackboard Blackboard { get; }

        public BehaviorContext(World world, Entity self, Blackboard blackboard)
        {
            World = world;
            Self = self;
            Blackboard = blackboard;
        }
    }

    /// <summary>
    /// A behavior-tree node (engine gap-analysis §4.5). One tree instance is owned per NPC, so
    /// composite nodes may hold their own execution-progress state (e.g. which child is running)
    /// directly as fields rather than round-tripping through the blackboard — sharing one
    /// immutable tree definition across many NPCs of the same type is a future optimization, not
    /// needed for Phase 1's cheap-brain goal.
    /// </summary>
    public abstract class BehaviorNode
    {
        public abstract BehaviorStatus Tick(BehaviorContext context);
    }
}
