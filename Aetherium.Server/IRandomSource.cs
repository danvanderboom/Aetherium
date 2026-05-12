using System;
using System.Collections.Generic;

namespace Aetherium.Server
{
    /// <summary>
    /// Abstraction over random number generation so probabilistic gameplay code
    /// (lockpicking, future skill checks) can be driven deterministically in tests
    /// while still using <see cref="System.Random.Shared"/> at runtime.
    ///
    /// <para>
    /// Per design D3 in
    /// <c>openspec/changes/extend-delta-vocabulary-for-use-disambiguation/design.md</c>,
    /// this interface is the seam where per-grain seeded RNG for replay determinism
    /// can later plug in. Today's implementation is single-shared-source.
    /// </para>
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Returns a value in [0.0, 1.0).</summary>
        double NextDouble();

        /// <summary>Returns a non-negative integer less than <paramref name="maxExclusive"/>.</summary>
        int NextInt(int maxExclusive);
    }

    /// <summary>
    /// Production <see cref="IRandomSource"/> backed by <see cref="System.Random.Shared"/>.
    /// Thread-safe; no per-call allocation.
    /// </summary>
    public sealed class DefaultRandomSource : IRandomSource
    {
        public double NextDouble() => Random.Shared.NextDouble();
        public int NextInt(int maxExclusive) => Random.Shared.Next(maxExclusive);
    }

    /// <summary>
    /// Test double that yields a pre-baked sequence of doubles. When the sequence
    /// is exhausted, repeats the last value (so tests don't have to count exact
    /// call counts). Integer draws are derived by clamping <c>NextDouble * max</c>.
    /// </summary>
    public sealed class FixedRandomSource : IRandomSource
    {
        private readonly double[] _values;
        private int _index;

        public FixedRandomSource(params double[] values)
        {
            if (values is null || values.Length == 0)
                throw new ArgumentException("FixedRandomSource requires at least one value.", nameof(values));
            _values = values;
        }

        public double NextDouble()
        {
            var v = _values[_index];
            if (_index < _values.Length - 1) _index++;
            return v;
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return Math.Min((int)(NextDouble() * maxExclusive), maxExclusive - 1);
        }
    }
}
