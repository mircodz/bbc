using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bond.Parser.Json;

/// <summary>
/// JSON converter for Bond root AST node
/// </summary>
public class BondJsonConverter : JsonConverter<Syntax.Bond>
{
    public override Syntax.Bond? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization not implemented");
    }

    public override void Write(Utf8JsonWriter writer, Syntax.Bond value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("imports");
        JsonSerializer.Serialize(writer, value.Imports, options);

        writer.WritePropertyName("namespaces");
        JsonSerializer.Serialize(writer, value.Namespaces, options);

        writer.WritePropertyName("declarations");
        JsonSerializer.Serialize(writer, value.Declarations, options);

        writer.WriteEndObject();
    }
}
