extern alias Console;

using NUnit.Framework;
using Aetherium.Model.Accessibility;
using ConsoleRendererDistinctions = Console::Aetherium.Accessibility.ConsoleRendererDistinctions;

namespace Aetherium.Test.Accessibility
{
    /// <summary>
    /// Verifies "Colorblind Lint Enforced Against Real Renderer Distinctions"
    /// (openspec/changes/wire-accessibility-live/specs/accessibility-contract/spec.md): the
    /// distinctions the console renderer actually draws, run through the Phase 1
    /// <see cref="ColorblindLintRule"/>, produce zero violations — the CI gate the design doc
    /// asked for now has something real to check.
    /// </summary>
    [TestFixture]
    public class ConsoleRendererDistinctionsTests
    {
        [Test]
        public void RealDistinctions_ProduceNoColorblindViolations()
        {
            var distinctions = ConsoleRendererDistinctions.Build();

            var violations = new ColorblindLintRule().FindViolations(distinctions);

            Assert.That(violations, Is.Empty,
                "Every registered console-renderer distinction must pair color with a non-color channel.");
        }

        [Test]
        public void RealDistinctions_CoverTerrainPlayerAndItemKeyColor()
        {
            var distinctions = ConsoleRendererDistinctions.Build();

            Assert.That(distinctions, Has.Some.Matches<SemanticDistinction>(d => d.Id == "terrain-type"));
            Assert.That(distinctions, Has.Some.Matches<SemanticDistinction>(d => d.Id == "player-marker"));
            Assert.That(distinctions, Has.Some.Matches<SemanticDistinction>(d => d.Id == "item-key-color"));
        }
    }
}
