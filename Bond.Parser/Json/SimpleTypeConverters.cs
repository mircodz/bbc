using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bond.Parser.Syntax;

namespace Bond.Parser.Json;

/// <summary>
/// JSON converter for Attribute
/// </summary>
public class AttributeJsonConverter : JsonConverter<Syntax.Attribute>
{
    public override Syntax.Attribute Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization not implemented");
    }

    public override void Write(Utf8JsonWriter writer, Syntax.Attribute value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("attrName");
        JsonSerializer.Serialize(writer, value.QualifiedName, options);

        writer.WritePropertyName("attrValue");
        writer.WriteStringValue(value.Value);

        writer.WriteEndObject();
    }
}

/// <summary>
/// JSON converter for Namespace
/// </summary>
public class NamespaceJsonConverter : JsonConverter<Namespace>
{
    public override Namespace Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization not implemented");
    }

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

/// <summary>
/// JSON converter for TypeParam
/// </summary>
public class TypeParamJsonConverter : JsonConverter<TypeParam>
{
    public override TypeParam Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization not implemented");
    }

    public override void Write(Utf8JsonWriter writer, TypeParam value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("paramName");
        writer.WriteStringValue(value.Name);

        if (value.Constraint != TypeConstraint.None)
        {
            writer.WritePropertyName("paramConstraint");
            writer.WriteStringValue(value.Constraint switch
            {
                TypeConstraint.Value => "value",
                _ => throw new JsonException($"Unknown TypeConstraint: {value.Constraint}")
            });
        }
        else
        {
            writer.WritePropertyName("paramConstraint");
            writer.WriteNullValue();
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// JSON converter for Constant (enum values)
/// </summary>
public class ConstantJsonConverter : JsonConverter<Constant>
{
    public override Constant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization not implemented");
    }

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

/// <summary>
/// JSON converter for Import
/// </summary>
public class ImportJsonConverter : JsonConverter<Import>
{
    public override Import Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization not implemented");
    }

    public override void Write(Utf8JsonWriter writer, Import value, JsonSerializerOptions options)
    {
        // Match original Bond JSON schema: imports are plain strings
        writer.WriteStringValue(value.FilePath);
    }
}
