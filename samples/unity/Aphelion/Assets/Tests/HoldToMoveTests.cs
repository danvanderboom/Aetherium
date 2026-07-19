using Aetherium.Client.Contracts;
using Aetherium.Unity.Input;
using NUnit.Framework;

namespace Aphelion.Tests
{
    /// <summary>
    /// Covers the pure "hold to move" logic that drives the faster movement: stick/key → direction
    /// resolution and the repeat-clock cadence. Runs in EditMode without a device, so the speed cap
    /// and tap-still-moves-once guarantees are verified independently of the MonoBehaviour.
    /// </summary>
    public sealed class HoldToMoveTests
    {
        [Test]
        public void FromStick_FullForward_IsNorth()
        {
            Assert.AreEqual(WorldDirection.North, DirectionalInput.FromStick(0f, 1f, 0.5f));
        }

        [Test]
        public void FromStick_FullRight_IsEast()
        {
            Assert.AreEqual(WorldDirection.East, DirectionalInput.FromStick(1f, 0f, 0.5f));
        }

        [Test]
        public void FromStick_InsideDeadzone_IsNull()
        {
            // magnitude ~0.42 < 0.5 deadzone.
            Assert.IsNull(DirectionalInput.FromStick(0.3f, 0.3f, 0.5f));
        }

        [Test]
        public void FromStick_Diagonal_ResolvesToDominantAxis()
        {
            Assert.AreEqual(WorldDirection.East, DirectionalInput.FromStick(0.9f, 0.4f, 0.5f));
            Assert.AreEqual(WorldDirection.South, DirectionalInput.FromStick(-0.4f, -0.9f, 0.5f));
        }

        [Test]
        public void FromKeys_SingleKey_IsThatDirection()
        {
            Assert.AreEqual(WorldDirection.West, DirectionalInput.FromKeys(false, false, false, true));
        }

        [Test]
        public void FromKeys_OpposingPair_Cancels()
        {
            Assert.IsNull(DirectionalInput.FromKeys(north: true, south: true, east: false, west: false));
        }

        [Test]
        public void FromKeys_NothingHeld_IsNull()
        {
            Assert.IsNull(DirectionalInput.FromKeys(false, false, false, false));
        }

        [Test]
        public void Repeater_FreshPress_StepsImmediately()
        {
            var repeater = new HeldMoveRepeater(0.1f);
            Assert.IsTrue(repeater.Tick(WorldDirection.North, 0f, out var step));
            Assert.AreEqual(WorldDirection.North, step);
        }

        [Test]
        public void Repeater_HeldBelowInterval_DoesNotStepAgain()
        {
            var repeater = new HeldMoveRepeater(0.1f);
            repeater.Tick(WorldDirection.North, 0f, out _);            // immediate first step
            Assert.IsFalse(repeater.Tick(WorldDirection.North, 0.05f, out _)); // 0.05 < 0.1
        }

        [Test]
        public void Repeater_HeldPastInterval_StepsAgain()
        {
            var repeater = new HeldMoveRepeater(0.1f);
            repeater.Tick(WorldDirection.North, 0f, out _);
            repeater.Tick(WorldDirection.North, 0.05f, out _);
            Assert.IsTrue(repeater.Tick(WorldDirection.North, 0.06f, out _)); // 0.11 >= 0.1
        }

        [Test]
        public void Repeater_ReleaseThenPress_StepsImmediatelyAgain()
        {
            var repeater = new HeldMoveRepeater(0.1f);
            repeater.Tick(WorldDirection.North, 0f, out _);
            repeater.Tick(null, 0.2f, out _);                          // released
            Assert.IsTrue(repeater.Tick(WorldDirection.North, 0f, out _)); // fresh press → immediate
        }

        [Test]
        public void Repeater_DirectionChange_StepsImmediately()
        {
            var repeater = new HeldMoveRepeater(0.1f);
            repeater.Tick(WorldDirection.North, 0f, out _);
            Assert.IsTrue(repeater.Tick(WorldDirection.East, 0.01f, out var step)); // changed before interval
            Assert.AreEqual(WorldDirection.East, step);
        }
    }
}
