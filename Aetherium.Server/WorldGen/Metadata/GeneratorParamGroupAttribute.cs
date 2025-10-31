using System;

namespace Aetherium.WorldGen.Metadata
{
    /// <summary>
    /// Attribute for defining parameter groups with display order.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class GeneratorParamGroupAttribute : Attribute
    {
        public string Name { get; }
        public int Order { get; set; }
        public string? Description { get; set; }

        public GeneratorParamGroupAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}

