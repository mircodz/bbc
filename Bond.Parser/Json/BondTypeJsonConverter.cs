using System.Text.Json;
using Bond.Parser.Syntax;

namespace Bond.Parser.Json;

internal sealed class BondTypeJsonConverter : WriteOnlyJsonConverter<BondType>
{
    public override void Write(Utf8JsonWriter writer, BondType value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case BondType.Int8:
                writer.WriteStringValue("int8");
                break;
            case BondType.Int16:
                writer.WriteStringValue("int16");
                break;
            case BondType.Int32:
                writer.WriteStringValue("int32");
                break;
            case BondType.Int64:
                writer.WriteStringValue("int64");
                break;
            case BondType.UInt8:
                writer.WriteStringValue("uint8");
                break;
            case BondType.UInt16:
                writer.WriteStringValue("uint16");
                break;
            case BondType.UInt32:
                writer.WriteStringValue("uint32");
                break;
            case BondType.UInt64:
                writer.WriteStringValue("uint64");
                break;
            case BondType.Float:
                writer.WriteStringValue("float");
                break;
            case BondType.Double:
                writer.WriteStringValue("double");
                break;
            case BondType.Bool:
                writer.WriteStringValue("bool");
                break;
            case BondType.String:
                writer.WriteStringValue("string");
                break;
            case BondType.WString:
                writer.WriteStringValue("wstring");
                break;
            case BondType.Blob:
                writer.WriteStringValue("blob");
                break;
            case BondType.MetaName:
                writer.WriteStringValue("bond_meta::name");
                break;
            case BondType.MetaFullName:
                writer.WriteStringValue("bond_meta::full_name");
                break;

            case BondType.List list:
                writer.WriteStartObject();
                writer.WriteString("type", "list");
                writer.WritePropertyName("element");
                JsonSerializer.Serialize(writer, list.ElementType, options);
                writer.WriteEndObject();
                break;

            case BondType.Vector vector:
                writer.WriteStartObject();
                writer.WriteString("type", "vector");
                writer.WritePropertyName("element");
                JsonSerializer.Serialize(writer, vector.ElementType, options);
                writer.WriteEndObject();
                break;

            case BondType.Set set:
                writer.WriteStartObject();
                writer.WriteString("type", "set");
                writer.WritePropertyName("element");
                JsonSerializer.Serialize(writer, set.KeyType, options);
                writer.WriteEndObject();
                break;

            case BondType.Map map:
                writer.WriteStartObject();
                writer.WriteString("type", "map");
                writer.WritePropertyName("key");
                JsonSerializer.Serialize(writer, map.KeyType, options);
                writer.WritePropertyName("element");
                JsonSerializer.Serialize(writer, map.ValueType, options);
                writer.WriteEndObject();
                break;

            case BondType.Nullable nullable:
                writer.WriteStartObject();
                writer.WriteString("type", "nullable");
                writer.WritePropertyName("element");
                JsonSerializer.Serialize(writer, nullable.ElementType, options);
                writer.WriteEndObject();
                break;

            case BondType.Maybe maybe:
                writer.WriteStartObject();
                writer.WriteString("type", "maybe");
                writer.WritePropertyName("element");
                JsonSerializer.Serialize(writer, maybe.ElementType, options);
                writer.WriteEndObject();
                break;

            case BondType.Bonded bonded:
                writer.WriteStartObject();
                writer.WriteString("type", "bonded");
                writer.WritePropertyName("element");
                JsonSerializer.Serialize(writer, bonded.StructType, options);
                writer.WriteEndObject();
                break;

            case BondType.TypeReference typeReference:
                writer.WriteStartObject();
                writer.WriteString("type", "user");
                writer.WritePropertyName("declaration");
                JsonSerializer.Serialize(writer, typeReference.Declaration, options);
                if (typeReference.TypeArguments.Length > 0)
                {
                    writer.WritePropertyName("arguments");
                    JsonSerializer.Serialize(writer, typeReference.TypeArguments, options);
                }
                writer.WriteEndObject();
                break;

            case BondType.TypeParameter typeParam:
                writer.WriteStartObject();
                writer.WriteString("type", "parameter");
                writer.WritePropertyName("value");
                JsonSerializer.Serialize(writer, typeParam.Param, options);
                writer.WriteEndObject();
                break;

            case BondType.IntTypeArg intTypeArg:
                writer.WriteStartObject();
                writer.WriteString("type", "constant");
                writer.WritePropertyName("value");
                writer.WriteNumberValue(intTypeArg.Value);
                writer.WriteEndObject();
                break;

            case BondType.UnresolvedType unresolvedType:
                throw new JsonException($"Cannot serialize unresolved type '{string.Join(".", unresolvedType.QualifiedName)}' — schema must pass semantic analysis first.");

            default:
                throw new JsonException($"Unknown BondType: {value.GetType().Name}");
        }
    }
}
