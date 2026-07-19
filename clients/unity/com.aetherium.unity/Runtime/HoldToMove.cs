using Aetherium.Client.Contracts;

namespace Aetherium.Unity.Input
{
    /// <summary>
    /// Resolves raw "hold to move" input — a held WASD key or a deflected gamepad stick — to a
    /// single compass <see cref="WorldDirection"/>. Pure and UnityEngine-free so it unit-tests in
    /// EditMode without a device; the MonoBehaviour samples the real device each frame and hands
    /// the numbers here. The world is 4-connected, so a diagonal stick still yields one clean
    /// cardinal step. Up-screen is North (the Aphelion camera keeps north up), so a full-forward
    /// stick maps to the same direction as W.
    /// </summary>
    public static class DirectionalInput
    {
        /// <summary>
        /// Map an analog stick to a compass direction. <paramref name="x"/> is right-positive,
        /// <paramref name="y"/> is up-positive (Unity gamepad convention). Inside the deadzone the
        /// stick reads as centered (returns null); otherwise the dominant axis wins so a diagonal
        /// push resolves to a single cardinal.
        /// </summary>
        public static WorldDirection? FromStick(float x, float y, float deadzone)
        {
            // Compare squared magnitudes to skip a sqrt.
            if (x * x + y * y < deadzone * deadzone)
                return null;
            if (System.Math.Abs(x) > System.Math.Abs(y))
                return x > 0f ? WorldDirection.East : WorldDirection.West;
            return y > 0f ? WorldDirection.North : WorldDirection.South;
        }

        /// <summary>
        /// Resolve held WASD keys. Opposing keys cancel; a fixed N/S/E/W precedence breaks the tie
        /// when two perpendicular keys are held (a diagonal can't be one step). Returns null when
        /// nothing — or only a canceling pair — is held.
        /// </summary>
        public static WorldDirection? FromKeys(bool north, bool south, bool east, bool west)
        {
            if (north && south) { north = false; south = false; }
            if (east && west) { east = false; west = false; }
            if (north) return WorldDirection.North;
            if (south) return WorldDirection.South;
            if (east) return WorldDirection.East;
            if (west) return WorldDirection.West;
            return null;
        }
    }

    /// <summary>
    /// Turns a sustained held direction into a paced stream of discrete move steps. The first frame
    /// a direction becomes held (or changes) emits a step immediately — so a quick tap still moves
    /// exactly one cell, 1:1, like the old <c>GetKeyDown</c> path — and while the direction stays
    /// held it then emits one step every <see cref="StepInterval"/> seconds.
    ///
    /// <para>This interval is the player's movement-speed knob: the server applies moves the instant
    /// they arrive (no per-tick gate, no movement cooldown), so the achievable rate is bounded only
    /// by this interval and the request round-trip. The caller must gate on "no request already in
    /// flight" — it should <see cref="Tick"/> only on frames where a new move may start. Skipping
    /// Tick while a request is outstanding simply pauses the clock, so the real cadence is
    /// StepInterval plus the round-trip, which self-limits the rate without extra bookkeeping.</para>
    /// </summary>
    public sealed class HeldMoveRepeater
    {
        /// <summary>Minimum seconds between steps while a direction is held (the speed cap).</summary>
        public float StepInterval;

        private WorldDirection? _active;
        private float _elapsed;

        public HeldMoveRepeater(float stepInterval)
        {
            StepInterval = stepInterval;
        }

        /// <summary>
        /// Advance the repeat clock by <paramref name="deltaTime"/> for the currently
        /// <paramref name="held"/> direction (null = nothing held) and report whether a step should
        /// be issued this frame, returning it in <paramref name="step"/>.
        /// </summary>
        public bool Tick(WorldDirection? held, float deltaTime, out WorldDirection step)
        {
            step = default;
            if (held is null)
            {
                // Released: forget the clock so the next press fires immediately.
                _active = null;
                _elapsed = 0f;
                return false;
            }

            if (_active != held)
            {
                // Fresh press or a change of direction — step now, then start repeating.
                _active = held;
                _elapsed = 0f;
                step = held.Value;
                return true;
            }

            _elapsed += deltaTime;
            if (_elapsed >= StepInterval)
            {
                // Reset (not carry the remainder): the in-flight gate already spaces steps by the
                // round-trip, and a hard reset avoids a catch-up burst after a frame-rate hitch.
                _elapsed = 0f;
                step = held.Value;
                return true;
            }
            return false;
        }
    }
}
