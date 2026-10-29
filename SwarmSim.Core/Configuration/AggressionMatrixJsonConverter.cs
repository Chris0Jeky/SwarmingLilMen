using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwarmSim.Core.Configuration;

/// <summary>Stores a rectangular matrix as JSON rows without changing the public C# matrix type.</summary>
internal sealed class AggressionMatrixJsonConverter : JsonConverter<float[,]>
{
    public AggressionMatrixJsonConverter() { }

    public override float[,] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        float[][] rows = JsonSerializer.Deserialize<float[][]>(ref reader, options)
            ?? throw new JsonException("AggressionMatrix must contain an array of rows.");
        int columns = rows.Length == 0 ? 0 : rows[0]?.Length
            ?? throw new JsonException("AggressionMatrix rows must not be null.");

        for (int row = 0; row < rows.Length; row++)
        {
            if (rows[row] is null || rows[row].Length != columns)
                throw new JsonException("AggressionMatrix rows must be non-null and have equal lengths.");
        }

        var matrix = new float[rows.Length, columns];
        for (int row = 0; row < rows.Length; row++)
            for (int column = 0; column < columns; column++)
                matrix[row, column] = rows[row][column];

        return matrix;
    }

    public override void Write(Utf8JsonWriter writer, float[,] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        for (int row = 0; row < value.GetLength(0); row++)
        {
            writer.WriteStartArray();
            for (int column = 0; column < value.GetLength(1); column++)
                writer.WriteNumberValue(value[row, column]);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }
}
