using System.Collections.Generic;
using System.Linq;

namespace Aetherium.WorldBuilders.Validation
{
    /// <summary>
    /// Result of a map validation run, containing all errors found and overall success status.
    /// </summary>
    public sealed class MapValidationReport
    {
        /// <summary>
        /// List of all validation errors found.
        /// </summary>
        public List<MapValidationError> Errors { get; init; } = new List<MapValidationError>();

        /// <summary>
        /// Whether the validation passed (no errors).
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Summary message describing the validation result.
        /// </summary>
        public string Summary => IsValid
            ? "Validation passed with no errors."
            : $"Validation failed with {Errors.Count} error(s).";

        public void AddError(string category, string message, string? location = null)
        {
            Errors.Add(new MapValidationError(category, message, location));
        }

        public void AddError(MapValidationError error)
        {
            Errors.Add(error);
        }

        public override string ToString()
        {
            if (IsValid)
                return Summary;

            var errorDetails = string.Join("\n", Errors.Select(e => $"  - {e}"));
            return $"{Summary}\nErrors:\n{errorDetails}";
        }
    }
}


