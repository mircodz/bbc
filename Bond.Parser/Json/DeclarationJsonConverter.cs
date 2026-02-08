using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bond.Parser.Syntax;

namespace Bond.Parser.Json;

/// <summary>
/// JSON converter for Declaration that matches the official Bond schema AST format
/// Uses "tag" discriminator for different declaration types
/// </summary>
public class DeclarationJsonConverter : JsonConverter<Declaration>
{
    public override Declaration? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization not implemented");
    }

    public override void Write(Utf8JsonWriter writer, Declaration value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("declName");
        writer.WriteStringValue(value.Name);

        writer.WritePropertyName("declNamespaces");
        JsonSerializer.Serialize(writer, value.Namespaces, options);

        // Type-specific properties
        switch (value)
        {
            case StructDeclaration structDecl:
                writer.WritePropertyName("declAttributes");
                JsonSerializer.Serialize(writer, structDecl.Attributes, options);

                writer.WriteString("tag", "Struct");

                writer.WritePropertyName("declParams");
                JsonSerializer.Serialize(writer, structDecl.TypeParameters, options);

                writer.WritePropertyName("structBase");
                if (structDecl.BaseType != null)
                {
                    JsonSerializer.Serialize(writer, structDecl.BaseType, options);
                }
                else
                {
                    writer.WriteNullValue();
                }

                writer.WritePropertyName("structFields");
                JsonSerializer.Serialize(writer, structDecl.Fields, options);
                break;

            case EnumDeclaration enumDecl:
                writer.WritePropertyName("declAttributes");
                JsonSerializer.Serialize(writer, enumDecl.Attributes, options);

                writer.WriteString("tag", "Enum");

                writer.WritePropertyName("enumConstants");
                JsonSerializer.Serialize(writer, enumDecl.Constants, options);
                break;

            case ForwardDeclaration forwardDecl:
                writer.WriteString("tag", "Forward");

                writer.WritePropertyName("declParams");
                JsonSerializer.Serialize(writer, forwardDecl.TypeParameters, options);
                break;

            case AliasDeclaration aliasDecl:
                writer.WriteString("tag", "Alias");

                writer.WritePropertyName("declParams");
                JsonSerializer.Serialize(writer, aliasDecl.TypeParameters, options);

                writer.WritePropertyName("aliasType");
                JsonSerializer.Serialize(writer, aliasDecl.AliasedType, options);
                break;

            case ServiceDeclaration serviceDecl:
                writer.WritePropertyName("declAttributes");
                JsonSerializer.Serialize(writer, serviceDecl.Attributes, options);

                writer.WriteString("tag", "Service");

                writer.WritePropertyName("declParams");
                JsonSerializer.Serialize(writer, serviceDecl.TypeParameters, options);

                writer.WritePropertyName("serviceBase");
                if (serviceDecl.BaseType != null)
                {
                    JsonSerializer.Serialize(writer, serviceDecl.BaseType, options);
                }
                else
                {
                    writer.WriteNullValue();
                }

                writer.WritePropertyName("serviceMethods");
                JsonSerializer.Serialize(writer, serviceDecl.Methods, options);
                break;

            default:
                throw new JsonException($"Unknown Declaration type: {value.GetType().Name}");
        }

        writer.WriteEndObject();
    }
}
