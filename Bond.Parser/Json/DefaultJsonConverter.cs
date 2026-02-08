using System.Text.Json;
using System.Text.Json.Serialization;
using Bond.Parser.Syntax;

namespace Bond.Parser.Json;

/// <summary>
/// JSON converter for Default values that matches the official Bond schema AST format
/// </summary>
public class DefaultJsonConverter : JsonConverter<Default>
{
    public override Default? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization not implemented");
    }

    public override void Write(Utf8JsonWriter writer, Default value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case Default.Bool boolValue:
                writer.WriteString("type", "bool");
                writer.WriteBoolean("value", boolValue.Value);
                break;

            case Default.Integer intValue:
                writer.WriteString("type", "integer");
                writer.WritePropertyName("value");
                // BigInteger can be arbitrarily large, write as raw JSON number
                writer.WriteRawValue(intValue.Value.ToString());
                break;

            case Default.Float floatValue:
                writer.WriteString("type", "float");
                var normalized = floatValue.Value == 0 ? 0 : floatValue.Value; // collapse -0 to 0 to match reference output
                writer.WriteNumber("value", normalized);
                break;

            case Default.String stringValue:
                writer.WriteString("type", "string");
                writer.WriteString("value", stringValue.Value);
                break;

            case Default.Enum enumValue:
                writer.WriteString("type", "enum");
                writer.WriteString("value", enumValue.Identifier);
                break;

            case Default.Nothing:
                writer.WriteString("type", "nothing");
                break;

            default:
                throw new JsonException($"Unknown Default type: {value.GetType().Name}");
        }

        writer.WriteEndObject();
    }
}
