using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Metadata;
using NJsonSchema;
using NJsonSchema.Generation;
using Aetherium.Model.Pcg;

namespace WorldGenCLI.Services
{
    /// <summary>
    /// Builds constraint schemas from generator parameter metadata.
    /// </summary>
    public sealed class ConstraintSchemaBuilder
    {
        private readonly MapGeneratorRegistry _registry;

        public ConstraintSchemaBuilder(MapGeneratorRegistry registry)
        {
            _registry = registry;
        }

        public ConstraintDescriptor BuildSchema(string generatorId)
        {
            var generator = _registry.GetGenerator(generatorId);
            if (generator == null)
                return new ConstraintDescriptor { GeneratorId = generatorId };

            var descriptor = new ConstraintDescriptor
            {
                GeneratorId = generatorId,
                Parameters = new List<ParameterDefinition>()
            };

            // Use reflection to extract parameter metadata from generator
            var generatorType = generator.GetType();
            var properties = generatorType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var paramAttr = prop.GetCustomAttribute<GeneratorParamAttribute>();
                if (paramAttr == null)
                    continue;

                var paramDef = new ParameterDefinition
                {
                    Name = prop.Name,
                    Description = paramAttr.Description,
                    Group = paramAttr.Group,
                    DefaultValue = paramAttr.DefaultValue ?? (prop.PropertyType.IsValueType ? Activator.CreateInstance(prop.PropertyType) : null),
                    MinValue = paramAttr.MinValue,
                    MaxValue = paramAttr.MaxValue,
                    Step = paramAttr.Step,
                    MaxLength = paramAttr.MaxLength,
                    Pattern = paramAttr.Pattern
                };

                // Determine parameter type from property type
                if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(int?))
                {
                    paramDef.Type = paramAttr.MinValue.HasValue && paramAttr.MaxValue.HasValue
                        ? ParameterType.IntegerRange
                        : ParameterType.Integer;
                    paramDef.DefaultValue = paramAttr.DefaultValue ?? 0;
                }
                else if (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(double) || 
                         prop.PropertyType == typeof(float?) || prop.PropertyType == typeof(double?))
                {
                    paramDef.Type = paramAttr.MinValue.HasValue && paramAttr.MaxValue.HasValue
                        ? ParameterType.FloatRange
                        : ParameterType.Float;
                    paramDef.DefaultValue = paramAttr.DefaultValue ?? 0.0;
                }
                else if (prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(bool?))
                {
                    paramDef.Type = ParameterType.Boolean;
                    paramDef.DefaultBool = paramAttr.DefaultValue != null ? (bool)paramAttr.DefaultValue : false;
                }
                else if (prop.PropertyType == typeof(string))
                {
                    paramDef.Type = ParameterType.String;
                    paramDef.DefaultValue = paramAttr.DefaultValue ?? string.Empty;
                }
                else if (prop.PropertyType.IsEnum)
                {
                    paramDef.Type = ParameterType.Choice;
                    paramDef.Choices = Enum.GetValues(prop.PropertyType)
                        .Cast<object>()
                        .Select(v => new ChoiceOption
                        {
                            Value = v.ToString() ?? string.Empty,
                            Label = v.ToString() ?? string.Empty,
                            Description = null
                        })
                        .ToList();
                }
                else
                {
                    paramDef.Type = ParameterType.String;
                }

                descriptor.Parameters.Add(paramDef);
            }

            // Generate JSON Schema
            try
            {
                var schema = JsonSchema.FromType(generatorType);
                descriptor.JsonSchema = schema.ToJson();
            }
            catch (Exception ex)
            {
                // Fallback to empty schema if generation fails
                descriptor.JsonSchema = "{}";
                Console.WriteLine($"Warning: Failed to generate JSON schema for {generatorId}: {ex.Message}");
            }

            return descriptor;
        }
    }
}

