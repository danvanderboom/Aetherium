using System;

namespace Aetherium.Server.Ai
{
    /// <summary>Evaluates a boolean predicate — never returns <see cref="BehaviorStatus.Running"/>.</summary>
    public class ConditionNode : BehaviorNode
    {
        private readonly Func<BehaviorContext, bool> _predicate;

        public ConditionNode(Func<BehaviorContext, bool> predicate)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        public override BehaviorStatus Tick(BehaviorContext context)
            => _predicate(context) ? BehaviorStatus.Success : BehaviorStatus.Failure;
    }

    /// <summary>Performs work and reports the outcome — the tree's actual gameplay effect (movement, attack, etc).</summary>
    public class ActionNode : BehaviorNode
    {
        private readonly Func<BehaviorContext, BehaviorStatus> _action;

        public ActionNode(Func<BehaviorContext, BehaviorStatus> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override BehaviorStatus Tick(BehaviorContext context) => _action(context);
    }

    /// <summary>Runs for a fixed number of ticks (Running), then Success.</summary>
    public class WaitNode : BehaviorNode
    {
        private readonly int _totalTicks;
        private int _remaining;
        private bool _started;

        public WaitNode(int ticks)
        {
            _totalTicks = ticks;
        }

        public override BehaviorStatus Tick(BehaviorContext context)
        {
            if (!_started)
            {
                _remaining = _totalTicks;
                _started = true;
            }

            if (_remaining <= 0)
            {
                _started = false;
                return BehaviorStatus.Success;
            }

            _remaining--;
            return _remaining <= 0 ? Complete() : BehaviorStatus.Running;
        }

        private BehaviorStatus Complete()
        {
            _started = false;
            return BehaviorStatus.Success;
        }
    }
}
