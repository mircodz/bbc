using System.Text.Json;
using Bond.Parser.Syntax;

namespace Bond.Parser.Json;

internal sealed class AttributeJsonConverter : WriteOnlyJsonConverter<Attribute>
{
    public override void Write(Utf8JsonWriter writer, Attribute value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("attrName");
        JsonSerializer.Serialize(writer, value.QualifiedName, options);

        writer.WritePropertyName("attrValue");
        writer.WriteStringValue(value.Value);

        writer.WriteEndObject();
    }
}

internal sealed class NamespaceJsonConverter : WriteOnlyJsonConverter<Namespace>
{
    public override void Write(Utf8JsonWriter writer, Namespace value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value.LanguageQualifier != null)
        {
            writer.WritePropertyName("language");
            writer.WriteStringValue(value.LanguageQualifier.Value.ToString().ToLower());
        }

        writer.WritePropertyName("name");
        JsonSerializer.Serialize(writer, value.Name, options);

        writer.WriteEndObject();
    }
}

internal sealed class TypeParamJsonConverter : WriteOnlyJsonConverter<TypeParam>
{
    public override void Write(Utf8JsonWriter writer, TypeParam value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("paramName");
        writer.WriteStringValue(value.Name);

        writer.WritePropertyName("paramConstraint");
        if (value.Constraint == TypeConstraint.Value)
        {
            writer.WriteStringValue("value");
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WriteEndObject();
    }
}

internal sealed class ConstantJsonConverter : WriteOnlyJsonConverter<Constant>
{
    public override void Write(Utf8JsonWriter writer, Constant value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("constantName");
        writer.WriteStringValue(value.Name);

        writer.WritePropertyName("constantValue");
        if (value.Value.HasValue)
        {
            writer.WriteNumberValue(value.Value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WriteEndObject();
    }
}

internal sealed class ImportJsonConverter : WriteOnlyJsonConverter<Import>
{
    // The upstream Bond JSON schema represents imports as plain strings.
    public override void Write(Utf8JsonWriter writer, Import value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.FilePath);
}
