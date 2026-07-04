using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetherium.Model.Training
{
    internal sealed class JsonStringToIntConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n))
            {
                return n;
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (int.TryParse(s, out var v))
                {
                    return v;
                }
            }
            throw new JsonException($"Cannot convert token {reader.TokenType} to int");
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }
    }
}


