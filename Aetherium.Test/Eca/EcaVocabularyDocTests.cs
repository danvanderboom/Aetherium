using System;
using System.IO;
using NUnit.Framework;
using Aetherium.Server.Eca;

namespace Aetherium.Test.Eca
{
    /// <summary>
    /// Verifies "Generated Language Documentation"
    /// (openspec/changes/add-eca-scripting/specs/eca-scripting/spec.md): the committed
    /// docs/eca-scripting.md vocabulary reference matches what EcaVocabularyDoc generates, so the guide
    /// cannot drift from the tile definitions.
    /// </summary>
    [TestFixture]
    public class EcaVocabularyDocTests
    {
        private static string DocPath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "docs", "eca-scripting.md");
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
            Assert.Fail("Could not locate docs/eca-scripting.md from the test output path.");
            throw new InvalidOperationException("unreachable");
        }

        [Test]
        public void CommittedDoc_MatchesGeneratedVocabularyReference()
        {
            var committed = File.ReadAllText(DocPath()).Replace("\r\n", "\n");
            var expected = EcaVocabularyDoc.Embed(committed).Replace("\r\n", "\n");

            Assert.That(committed, Is.EqualTo(expected),
                "docs/eca-scripting.md vocabulary reference is stale. Re-run the Explicit " +
                "'RegenerateDoc' test (or paste EcaVocabularyDoc.GenerateMarkdown() between the markers).");
        }

        /// <summary>Rewrites the generated section of the committed doc. Explicit so it never runs in the
        /// normal suite; run it once after changing a tile definition to refresh the guide.</summary>
        [Test, Explicit("Regenerates docs/eca-scripting.md from EcaVocabulary.")]
        public void RegenerateDoc()
        {
            var path = DocPath();
            var committed = File.ReadAllText(path).Replace("\r\n", "\n");
            File.WriteAllText(path, EcaVocabularyDoc.Embed(committed));
        }
    }
}
