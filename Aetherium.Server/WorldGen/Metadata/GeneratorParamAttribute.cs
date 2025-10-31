using System;

namespace Aetherium.WorldGen.Metadata
{
    /// <summary>
    /// Attribute for annotating generator parameters with metadata for UI generation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class GeneratorParamAttribute : Attribute
    {
        /// <summary>
        /// Human-readable description of the parameter.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Group name for organizing parameters in UI.
        /// </summary>
        public string? Group { get; set; }

        /// <summary>
        /// Default value for the parameter.
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// Minimum value (for numeric types).
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Maximum value (for numeric types).
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Step increment (for numeric types).
        /// </summary>
        public double? Step { get; set; }

        /// <summary>
        /// Maximum length (for string types).
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Regex pattern for validation (for string types).
        /// </summary>
        public string? Pattern { get; set; }

        /// <summary>
        /// Whether this parameter is required.
        /// </summary>
        public bool Required { get; set; } = false;
    }
}

