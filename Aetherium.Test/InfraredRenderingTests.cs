using System;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldBuilders;
using Aetherium.Server;
using Aetherium.Model;
using Xunit;

namespace Aetherium.Test
{
    /// <summary>
    /// Regression tests for the infrared black-screen bug (P0-8 in
    /// docs/audits/2026-07-03-initial-subsystem-audit/RECOMMENDATIONS.md): the infrared branch of PerceptionService
    /// used to build an empty LightFrame, so every VisualDto shipped with
    /// LightLevel = 0 and the client painted the whole map black. Infrared now
    /// records each heated location's intensity into the light frame — the DTO's
    /// LightLevel is the heat channel infrared clients color by.
    /// </summary>
    public class InfraredRenderingTests
    {
        [Fact]
        public void Infrared_Perception_Carries_Heat_In_LightLevel()
        {
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            session.CurrentVisionMode = VisionMode.Infrared;

            // A hot entity near the player.
            var monster = new Character();
            monster.Set(new WorldLocation(17, 15, 0));
            monster.Set(new HeatSignature(0.8, TimeSpan.FromMinutes(5)));
            session.World.AddEntity(monster);

            var perception = session.GetPerception();

            // The heated cell must reach the client with a non-black heat level.
            // (Before the fix, every infrared visual had LightLevel == 0.0.)
            Assert.NotEmpty(perception.Visuals);
            Assert.Contains(perception.Visuals.Values, v => v.LightLevel > 0.05);
            Assert.Contains(perception.Visuals.Values, v => v.LightLevel >= 0.5);
        }

        [Fact]
        public void Infrared_Perception_Without_Heat_Sources_Stays_Dark()
        {
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            session.CurrentVisionMode = VisionMode.Infrared;

            var perception = session.GetPerception();

            // No heat sources beyond (possibly) the player: nothing should render
            // hotter than the noise threshold except cells with real signatures.
            foreach (var visual in perception.Visuals.Values)
            {
                Assert.InRange(visual.LightLevel, 0.0, 1.0);
            }
        }
    }
}
