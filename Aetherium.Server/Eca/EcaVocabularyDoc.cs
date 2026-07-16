using System;
using System.Linq;
using System.Text;
using Aetherium.Model.Eca;

namespace Aetherium.Server.Eca
{
    /// <summary>
    /// Renders <see cref="EcaVocabulary"/> as a Markdown reference (add-eca-scripting). This is the
    /// documentation half of the "define the vocabulary once" promise: <c>docs/eca-scripting.md</c>
    /// embeds this output between markers, and a test regenerates and compares, so the language guide
    /// can never drift from the tile definitions. Regenerate with the test's failure instructions, or
    /// by calling <see cref="GenerateMarkdown"/> and pasting between the markers.
    /// </summary>
    public static class EcaVocabularyDoc
    {
        public const string StartMarker = "<!-- eca:vocab:start -->";
        public const string EndMarker = "<!-- eca:vocab:end -->";

        /// <summary>The generated reference body (no surrounding markers), one section per tile role.</summary>
        public static string GenerateMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("_This section is generated from `EcaVocabulary`; edit the tile definitions, not this table._");
            sb.AppendLine();

            foreach (var role in new[] { EcaTileRole.Trigger, EcaTileRole.Condition, EcaTileRole.Action })
            {
                sb.Append("### ").Append(role).AppendLine("s");
                sb.AppendLine();

                foreach (var tile in EcaVocabulary.ByRole(role))
                {
                    sb.Append("#### `").Append(tile.Id).AppendLine("`");
                    sb.AppendLine();
                    sb.AppendLine(tile.Description);
                    sb.AppendLine();

                    if (tile.Parameters.Count == 0)
                    {
                        sb.AppendLine("_No parameters._");
                    }
                    else
                    {
                        sb.AppendLine("| Parameter | Type | Required | Description |");
                        sb.AppendLine("|---|---|---|---|");
                        foreach (var p in tile.Parameters)
                        {
                            var type = p.ValueType.ToString();
                            if (p.EnumChoices.Count > 0)
                                type += " (" + string.Join(" / ", p.EnumChoices) + ")";
                            sb.Append("| `").Append(p.Name).Append("` | ").Append(type).Append(" | ")
                              .Append(p.Required ? "yes" : "no").Append(" | ").Append(p.Description).AppendLine(" |");
                        }
                    }

                    if (tile.ValidTargets.Count > 0)
                    {
                        sb.AppendLine();
                        sb.Append("_Targets:_ ").AppendLine(string.Join(", ", tile.ValidTargets.Select(t => "`" + t + "`")));
                    }
                    sb.AppendLine();
                }
            }

            // Normalize to LF so the generated section is stable across platforms (StringBuilder's
            // AppendLine emits the environment newline) — the drift test compares LF-normalized text.
            return sb.ToString().Replace("\r\n", "\n").TrimEnd() + "\n";
        }

        /// <summary>Splices freshly generated content between the markers in a full doc string. Used by
        /// the drift test to compute the expected file and, if needed, to rewrite it.</summary>
        public static string Embed(string fullDoc)
        {
            int start = fullDoc.IndexOf(StartMarker, StringComparison.Ordinal);
            int end = fullDoc.IndexOf(EndMarker, StringComparison.Ordinal);
            if (start < 0 || end < 0 || end < start)
                throw new InvalidOperationException($"Doc is missing the {StartMarker}/{EndMarker} markers.");

            var before = fullDoc.Substring(0, start + StartMarker.Length);
            var after = fullDoc.Substring(end);
            return before + "\n\n" + GenerateMarkdown() + "\n" + after;
        }
    }
}
