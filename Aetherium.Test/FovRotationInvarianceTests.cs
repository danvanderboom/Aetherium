using System.Collections.Generic;
using System.Linq;
using Aetherium.Server;
using Aetherium.WorldBuilders;
using NUnit.Framework;

namespace Aetherium.Test
{
    /// <summary>
    /// Regression tests for the historical FOV rotation bug (P2-4).
    ///
    /// The bug class: a client (or server) rotating the coordinate frame so that
    /// the visible set or the relative keys shift when the player turns in place.
    /// The current architecture eliminates it by construction — FOV is computed in
    /// world coordinates, perception is emitted with unrotated player-relative keys,
    /// and heading only matters as the directional-cone filter when directional
    /// vision is ON. These tests pin that contract so it can't silently regress.
    /// </summary>
    [TestFixture]
    public class FovRotationInvarianceTests
    {
        private static GameSession CreateOpenSpaceSession(string connectionId)
        {
            var builder = new FovDiagnosticWorldBuilder("open_space");
            return new GameSession(connectionId, builder);
        }

        [Test]
        public void Omnidirectional_VisibleSet_Is_Identical_At_Every_Heading()
        {
            var session = CreateOpenSpaceSession("fov-rotation-omni");
            session.DirectionalVisionMode = false;

            var baseline = session.GetPerception();
            var baselineKeys = baseline.Visuals.Keys.ToList();
            Assert.That(baselineKeys, Is.Not.Empty, "open_space should have visible cells");

            // Sweep through headings, including non-cardinal ones; the visible set
            // (relative-coordinate keys) must never change while directional mode
            // is off, because heading must not participate in FOV at all.
            foreach (var rotation in new[] { 90, 90, 90, 90, 45, 30, 180, 15 })
            {
                session.RotateView(rotation);
                var perception = session.GetPerception();

                Assert.That(perception.Visuals.Keys, Is.EquivalentTo(baselineKeys),
                    $"Visible set changed after rotating to heading {session.HeadingDegrees}° — " +
                    "rotation is leaking into omnidirectional FOV");
            }
        }

        [Test]
        public void Omnidirectional_Visual_Content_Is_Stable_Across_Rotation()
        {
            var session = CreateOpenSpaceSession("fov-rotation-content");
            session.DirectionalVisionMode = false;

            var before = session.GetPerception();
            session.RotateView(90);
            var after = session.GetPerception();

            // Not just the key set: what's AT each relative key must match, or a
            // rotated-tile-under-unrotated-mask bug (the dead legacy view's failure
            // mode) could reappear undetected.
            foreach (var kvp in before.Visuals)
            {
                Assert.That(after.Visuals.ContainsKey(kvp.Key), Is.True,
                    $"Relative key {kvp.Key} disappeared after rotation");
                Assert.That(after.Visuals[kvp.Key].Terrain?.Name, Is.EqualTo(kvp.Value.Terrain?.Name),
                    $"Terrain at relative key {kvp.Key} changed after rotation");
            }
        }

        [Test]
        public void Directional_Cone_Rotates_With_Heading_And_Stays_Within_Omni_Set()
        {
            var session = CreateOpenSpaceSession("fov-rotation-directional");

            session.DirectionalVisionMode = false;
            var omniKeys = new HashSet<string>(session.GetPerception().Visuals.Keys);

            session.DirectionalVisionMode = true;
            var north = session.GetPerception();
            var northKeys = new HashSet<string>(north.Visuals.Keys);

            Assert.That(north.IsDirectionalVision, Is.True);
            Assert.That(northKeys, Is.Not.Empty, "the forward cone should see something");
            Assert.That(northKeys.Count, Is.LessThan(omniKeys.Count),
                "a 120° cone should see strictly less than omnidirectional vision");
            Assert.That(northKeys.IsSubsetOf(omniKeys),
                "the directional cone must be a filter over the omni set, never additive");

            session.RotateView(180);
            var southKeys = new HashSet<string>(session.GetPerception().Visuals.Keys);

            // In open space a 120° cone facing north and one facing south overlap
            // only near the origin — the memberships must differ, proving heading
            // actually drives the cone (and only the cone).
            Assert.That(southKeys.SetEquals(northKeys), Is.False,
                "rotating 180° should change which cells the cone admits");
            Assert.That(southKeys.IsSubsetOf(omniKeys),
                "the rotated cone must also stay within the omni set");
        }

        [Test]
        public void Toggling_Directional_Mode_Off_Restores_The_Full_Set_Regardless_Of_Heading()
        {
            var session = CreateOpenSpaceSession("fov-rotation-toggle");
            session.DirectionalVisionMode = false;
            var baseline = new HashSet<string>(session.GetPerception().Visuals.Keys);

            // Face an arbitrary non-cardinal heading, look through the cone, then
            // toggle back — the full set must return exactly.
            session.RotateView(135);
            session.DirectionalVisionMode = true;
            _ = session.GetPerception();
            session.DirectionalVisionMode = false;

            var restored = new HashSet<string>(session.GetPerception().Visuals.Keys);
            Assert.That(restored, Is.EquivalentTo(baseline),
                "leaving directional mode at heading 135° must restore the identical omni set");
        }
    }
}
