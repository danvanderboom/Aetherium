using System;
using System.Collections.Generic;

namespace WorldGenCLI.Models
{
    /// <summary>
    /// Describes generator parameters with metadata for UI generation.
    /// </summary>
    public sealed class ConstraintDescriptor
    {
        public string GeneratorId { get; set; } = string.Empty;
        public List<ParameterDefinition> Parameters { get; set; } = new List<ParameterDefinition>();
        public string? JsonSchema { get; set; }
    }

    public sealed class ParameterDefinition
    {
        public string Name { get; set; } = string.Empty;
        public ParameterType Type { get; set; }
        public object? DefaultValue { get; set; }
        public string? Description { get; set; }
        public string? Group { get; set; }

        // For numeric types
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public double? Step { get; set; }

        // For enum/choice types
        public List<ChoiceOption>? Choices { get; set; }

        // For boolean
        public bool? DefaultBool { get; set; }

        // For string
        public int? MaxLength { get; set; }
        public string? Pattern { get; set; }
    }

    public enum ParameterType
    {
        Integer,
        Float,
        Boolean,
        String,
        Choice,
        IntegerRange,
        FloatRange
    }

    public sealed class ChoiceOption
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}

