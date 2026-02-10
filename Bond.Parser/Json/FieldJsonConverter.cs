using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bond.Parser.Syntax;

namespace Bond.Parser.Json;

/// <summary>
/// JSON converter for Field that matches the official Bond schema AST format
/// </summary>
public class FieldJsonConverter : JsonConverter<Field>
{
    public override Field Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization not implemented");
    }

    public override void Write(Utf8JsonWriter writer, Field value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("fieldAttributes");
        JsonSerializer.Serialize(writer, value.Attributes, options);

        writer.WritePropertyName("fieldOrdinal");
        writer.WriteNumberValue(value.Ordinal);

        writer.WritePropertyName("fieldModifier");
        writer.WriteStringValue(value.Modifier switch
        {
            FieldModifier.Optional => "Optional",
            FieldModifier.Required => "Required",
            FieldModifier.RequiredOptional => "RequiredOptional",
            _ => throw new JsonException($"Unknown FieldModifier: {value.Modifier}")
        });

        writer.WritePropertyName("fieldType");
        JsonSerializer.Serialize(writer, value.Type, options);

        writer.WritePropertyName("fieldName");
        writer.WriteStringValue(value.Name);

        writer.WritePropertyName("fieldDefault");
        if (value.DefaultValue != null)
        {
            JsonSerializer.Serialize(writer, value.DefaultValue, options);
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WriteEndObject();
    }
}
