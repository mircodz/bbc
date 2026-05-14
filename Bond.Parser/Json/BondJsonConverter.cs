using System.Text.Json;

namespace Bond.Parser.Json;

internal sealed class BondJsonConverter : WriteOnlyJsonConverter<Syntax.Bond>
{
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
