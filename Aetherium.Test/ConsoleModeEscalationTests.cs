extern alias Console;
using System.Collections.Generic;
using System.Drawing;
using NUnit.Framework;
using Aetherium.Model;
using ClientConsoleMapView = Console::Aetherium.Views.ClientConsoleMapView;

namespace Aetherium.Test
{
    /// <summary>
    /// Section 5.2 of add-adaptive-depth-visualization (console half): mode escalation. Once the local column's
    /// vertical complexity crosses the threshold, the console auto-surfaces the elevation (cross-section) view;
    /// the manual X toggle always wins. Verified through the pure complexity/effective-mode predicates.
    /// </summary>
    public class ConsoleModeEscalationTests
    {
        private static VisualDto Terrain(int z) => new VisualDto
        {
            Location = new WorldLocationDto(0, 0, z),
            LightLevel = 1.0,
            TileTypeId = "t", // resolved against the palette registered in ViewWithBands
        };

        private static ClientConsoleMapView ViewWithBands(params int[] bands)
        {
            var p = new PerceptionDto { PlayerLocation = new WorldLocationDto(0, 0, 0) };
            p.TileTypes["t"] = new TileTypeDto { Name = "t", Settings = new Dictionary<string, string> { ["MapCharacter"] = "#" } };
            foreach (var z in bands)
                p.Visuals[$"0,0,{z}"] = Terrain(z);

            var view = new ClientConsoleMapView(new Point(0, 0), new Size(20, 10), hasFrame: false);
            view.Perception = p;
            view.WorldLocation = p.PlayerLocation;
            return view;
        }

        [Test]
        public void VerticalComplexity_CountsOccupiedBands()
        {
            Assert.AreEqual(4, ViewWithBands(0, 1, 2, 3).VerticalComplexity(), "Bands {0,1,2,3}");
            Assert.AreEqual(1, ViewWithBands(0).VerticalComplexity(), "Flat column is just the focus band");
        }

        [Test]
        public void ShouldAutoSurface_HonorsThreshold()
        {
            var flat = ViewWithBands(0);
            var tall = ViewWithBands(0, 1, 2, 3);
            Assert.AreEqual(4, flat.CrossSectionEscalationThreshold, "Default threshold");

            Assert.IsFalse(flat.ShouldAutoSurfaceCrossSection());
            Assert.IsTrue(tall.ShouldAutoSurfaceCrossSection());
        }

        [Test]
        public void EffectiveCrossSection_ManualToggle_AlwaysWins()
        {
            var flat = ViewWithBands(0);
            Assert.IsFalse(flat.EffectiveCrossSection, "Plan view by default");

            flat.CrossSectionMode = true;
            Assert.IsTrue(flat.EffectiveCrossSection, "Manual toggle surfaces elevation regardless of complexity");
        }

        [Test]
        public void EffectiveCrossSection_AutoEscalation_SurfacesOnlyWhenTall()
        {
            var tall = ViewWithBands(0, 1, 2, 3);
            var flat = ViewWithBands(0);

            tall.AutoEscalateCrossSection = true;
            flat.AutoEscalateCrossSection = true;

            Assert.IsTrue(tall.EffectiveCrossSection, "Tall interchange auto-surfaces the elevation view");
            Assert.IsFalse(flat.EffectiveCrossSection, "Flat street stays in plan view");
        }

        [Test]
        public void EffectiveCrossSection_AutoOff_IgnoresComplexity()
        {
            var tall = ViewWithBands(0, 1, 2, 3); // auto-escalation left off
            Assert.IsFalse(tall.EffectiveCrossSection, "Without opt-in, complexity does not auto-surface");
        }
    }
}
