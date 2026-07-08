using System;
using System.Collections.Generic;
using System.Linq;

namespace Aetherium.Server.Ai
{
    /// <summary>Runs children in order; a Failure fails the whole node; a Running child is resumed
    /// at the same index next tick; all children succeeding succeeds the whole node.</summary>
    public class SequenceNode : BehaviorNode
    {
        private readonly IReadOnlyList<BehaviorNode> _children;
        private int _index;

        public SequenceNode(params BehaviorNode[] children)
        {
            _children = children;
        }

        public override BehaviorStatus Tick(BehaviorContext context)
        {
            while (_index < _children.Count)
            {
                var status = _children[_index].Tick(context);
                if (status == BehaviorStatus.Running)
                    return BehaviorStatus.Running;

                if (status == BehaviorStatus.Failure)
                {
                    _index = 0;
                    return BehaviorStatus.Failure;
                }

                _index++;
            }

            _index = 0;
            return BehaviorStatus.Success;
        }
    }

    /// <summary>Runs children in order until one succeeds (fallback/selector); a Running child is
    /// resumed at the same index next tick; all children failing fails the whole node.</summary>
    public class SelectorNode : BehaviorNode
    {
        private readonly IReadOnlyList<BehaviorNode> _children;
        private int _index;

        public SelectorNode(params BehaviorNode[] children)
        {
            _children = children;
        }

        public override BehaviorStatus Tick(BehaviorContext context)
        {
            while (_index < _children.Count)
            {
                var status = _children[_index].Tick(context);
                if (status == BehaviorStatus.Running)
                    return BehaviorStatus.Running;

                if (status == BehaviorStatus.Success)
                {
                    _index = 0;
                    return BehaviorStatus.Success;
                }

                _index++;
            }

            _index = 0;
            return BehaviorStatus.Failure;
        }
    }

    /// <summary>Ticks every child every call. Succeeds once at least <see cref="RequiredSuccesses"/>
    /// children have succeeded (default: all); fails once enough children have failed that the
    /// required successes can no longer be reached; otherwise Running.</summary>
    public class ParallelNode : BehaviorNode
    {
        private readonly IReadOnlyList<BehaviorNode> _children;
        private readonly int _requiredSuccesses;

        public ParallelNode(IReadOnlyList<BehaviorNode> children, int? requiredSuccesses = null)
        {
            _children = children;
            _requiredSuccesses = requiredSuccesses ?? children.Count;
        }

        public override BehaviorStatus Tick(BehaviorContext context)
        {
            int successes = 0, failures = 0;
            foreach (var child in _children)
            {
                var status = child.Tick(context);
                if (status == BehaviorStatus.Success) successes++;
                else if (status == BehaviorStatus.Failure) failures++;
            }

            if (successes >= _requiredSuccesses)
                return BehaviorStatus.Success;

            int maxPossibleSuccesses = _children.Count - failures;
            if (maxPossibleSuccesses < _requiredSuccesses)
                return BehaviorStatus.Failure;

            return BehaviorStatus.Running;
        }
    }

    /// <summary>Picks one child uniformly at random each time it's (re-)entered (not while a child is
    /// Running), then ticks only that child.</summary>
    public class RandomSelectorNode : BehaviorNode
    {
        private readonly IReadOnlyList<BehaviorNode> _children;
        private readonly Random _random;
        private int? _selected;

        public RandomSelectorNode(Random random, params BehaviorNode[] children)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _children = children;
        }

        public override BehaviorStatus Tick(BehaviorContext context)
        {
            _selected ??= _random.Next(_children.Count);

            var status = _children[_selected.Value].Tick(context);
            if (status != BehaviorStatus.Running)
                _selected = null;

            return status;
        }
    }

    /// <summary>Scores every child via <paramref name="scorer"/> each time it's (re-)entered, picks the
    /// highest-scoring child, and ticks only that one — bridges tree authors who prefer weighted
    /// knobs over explicit branching.</summary>
    public class UtilitySelectorNode : BehaviorNode
    {
        private readonly IReadOnlyList<(BehaviorNode Node, Func<BehaviorContext, double> Score)> _children;
        private int? _selected;

        public UtilitySelectorNode(params (BehaviorNode Node, Func<BehaviorContext, double> Score)[] children)
        {
            _children = children;
        }

        public override BehaviorStatus Tick(BehaviorContext context)
        {
            if (_selected is null)
            {
                double bestScore = double.NegativeInfinity;
                for (int i = 0; i < _children.Count; i++)
                {
                    double score = _children[i].Score(context);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        _selected = i;
                    }
                }
            }

            var status = _children[_selected!.Value].Node.Tick(context);
            if (status != BehaviorStatus.Running)
                _selected = null;

            return status;
        }
    }
}
