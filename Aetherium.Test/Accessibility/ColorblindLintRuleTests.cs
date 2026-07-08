using NUnit.Framework;
using Aetherium.Server.Accessibility;

namespace Aetherium.Test.Accessibility
{
    /// <summary>Verifies "Colorblind Contract Enforcement" (openspec/changes/add-accessibility-contract/specs/accessibility-contract/spec.md).</summary>
    [TestFixture]
    public class ColorblindLintRuleTests
    {
        [Test]
        public void ColorOnly_Distinction_IsAViolation()
        {
            var distinction = new SemanticDistinction("lava_vs_water", "Lava tiles vs water tiles");
            distinction.MarkEncodedBy(AccessibilityChannel.Color);

            var violations = new ColorblindLintRule().FindViolations(new[] { distinction });

            Assert.That(violations, Does.Contain("lava_vs_water"));
        }

        [Test]
        public void ColorPlusShape_Distinction_IsNotAViolation()
        {
            var distinction = new SemanticDistinction("lava_vs_water", "Lava tiles vs water tiles");
            distinction.MarkEncodedBy(AccessibilityChannel.Color);
            distinction.MarkEncodedBy(AccessibilityChannel.Shape);

            var violations = new ColorblindLintRule().FindViolations(new[] { distinction });

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ColorPlusAudioTag_Distinction_IsNotAViolation()
        {
            var distinction = new SemanticDistinction("danger_zone", "A hazardous area");
            distinction.MarkEncodedBy(AccessibilityChannel.Color);
            distinction.SetAudioTag("warning_drone");

            Assert.That(distinction.IsEncodedBy(AccessibilityChannel.Audio), Is.True);
            var violations = new ColorblindLintRule().FindViolations(new[] { distinction });

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void NoColorChannel_Distinction_IsNeverAViolation()
        {
            var distinction = new SemanticDistinction("silent_and_shapeless", "Encoded by label only");
            distinction.MarkEncodedBy(AccessibilityChannel.Label);

            var violations = new ColorblindLintRule().FindViolations(new[] { distinction });

            Assert.That(violations, Is.Empty, "A distinction that never used color at all is not what this lint rule checks for.");
        }

        [Test]
        public void FindViolations_ChecksEachDistinctionIndependently()
        {
            var violatingOne = new SemanticDistinction("color_only", "Bad");
            violatingOne.MarkEncodedBy(AccessibilityChannel.Color);

            var compliantOne = new SemanticDistinction("color_and_label", "Good");
            compliantOne.MarkEncodedBy(AccessibilityChannel.Color);
            compliantOne.MarkEncodedBy(AccessibilityChannel.Label);

            var violations = new ColorblindLintRule().FindViolations(new[] { violatingOne, compliantOne });

            Assert.That(violations, Is.EquivalentTo(new[] { "color_only" }));
        }
    }
}
