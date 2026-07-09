namespace Aetherium.Server.Ai
{
    /// <summary>An NPC's behavior tree instance: a root node plus the blackboard it carries across ticks.</summary>
    public class BehaviorTree
    {
        private readonly BehaviorNode _root;

        public Blackboard Blackboard { get; }

        public BehaviorTree(BehaviorNode root, Blackboard? blackboard = null)
        {
            _root = root;
            Blackboard = blackboard ?? new Blackboard();
        }

        public BehaviorStatus Tick(Aetherium.Core.World world, Aetherium.Core.Entity self)
            => _root.Tick(new BehaviorContext(world, self, Blackboard));
    }
}
