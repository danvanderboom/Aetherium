using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetherium.WorldGen.Prefabs
{
    internal sealed class PrefabTile2DConverter : JsonConverter<PrefabTile[,]>
    {
        public override PrefabTile[,]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Expected start of array for 2D tiles");
            }

            // Read jagged array
            var rows = new System.Collections.Generic.List<PrefabTile[]>();

            reader.Read();
            while (reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    throw new JsonException("Expected start of inner array for tiles row");
                }

                var row = new System.Collections.Generic.List<PrefabTile>();

                reader.Read();
                while (reader.TokenType != JsonTokenType.EndArray)
                {
                    var tile = JsonSerializer.Deserialize<PrefabTile>(ref reader, options);
                    if (tile == null)
                    {
                        throw new JsonException("Tile element deserialized to null");
                    }
                    row.Add(tile);
                    reader.Read();
                }

                rows.Add(row.ToArray());
                reader.Read();
            }

            // Convert to 2D array
            var height = rows.Count;
            var width = height > 0 ? rows[0].Length : 0;
            var result = new PrefabTile[width, height];
            for (int y = 0; y < height; y++)
            {
                if (rows[y].Length != width)
                {
                    throw new JsonException("All rows in tiles array must have the same length");
                }
                for (int x = 0; x < width; x++)
                {
                    result[x, y] = rows[y][x];
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, PrefabTile[,] value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            int width = value.GetLength(0);
            int height = value.GetLength(1);

            writer.WriteStartArray();
            for (int y = 0; y < height; y++)
            {
                writer.WriteStartArray();
                for (int x = 0; x < width; x++)
                {
                    JsonSerializer.Serialize(writer, value[x, y], options);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }
    }
}



