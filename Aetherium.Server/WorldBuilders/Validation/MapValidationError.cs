namespace Aetherium.WorldBuilders.Validation
{
    /// <summary>
    /// Represents a single validation error found during map validation.
    /// </summary>
    public sealed class MapValidationError
    {
        /// <summary>
        /// The category/type of validation error (e.g., "Boundary", "Lighting", "StartLocation").
        /// </summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>
        /// A human-readable message describing the error.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Optional location associated with the error (if applicable).
        /// </summary>
        public string? Location { get; init; }

        public MapValidationError(string category, string message, string? location = null)
        {
            Category = category;
            Message = message;
            Location = location;
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Location))
                return $"[{Category}] {Message}";
            return $"[{Category}] {Message} (at {Location})";
        }
    }
}


