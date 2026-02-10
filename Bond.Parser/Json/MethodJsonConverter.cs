using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bond.Parser.Syntax;

namespace Bond.Parser.Json;

/// <summary>
/// JSON converter for MethodType
/// </summary>
public class MethodTypeJsonConverter : JsonConverter<MethodType>
{
    public override MethodType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization not implemented");
    }

    public override void Write(Utf8JsonWriter writer, MethodType value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case MethodType.Void:
                writer.WriteNullValue();
                break;

            case MethodType.Unary unary:
                JsonSerializer.Serialize(writer, unary.Type, options);
                break;

            case MethodType.Streaming streaming:
                JsonSerializer.Serialize(writer, streaming.Type, options);
                break;

            default:
                throw new JsonException($"Unknown MethodType: {value.GetType().Name}");
        }
    }
}

/// <summary>
/// JSON converter for Method (Event and Function)
/// </summary>
public class MethodJsonConverter : JsonConverter<Method>
{
    public override Method Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization not implemented");
    }

    public override void Write(Utf8JsonWriter writer, Method value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case EventMethod eventMethod:
                writer.WriteString("tag", "Event");

                writer.WritePropertyName("methodAttributes");
                JsonSerializer.Serialize(writer, eventMethod.Attributes, options);

                writer.WritePropertyName("methodName");
                writer.WriteStringValue(eventMethod.Name);

                writer.WritePropertyName("methodInput");
                JsonSerializer.Serialize(writer, eventMethod.InputType, options);
                break;

            case FunctionMethod functionMethod:
                writer.WriteString("tag", "Function");

                writer.WritePropertyName("methodAttributes");
                JsonSerializer.Serialize(writer, functionMethod.Attributes, options);

                writer.WritePropertyName("methodName");
                writer.WriteStringValue(functionMethod.Name);

                writer.WritePropertyName("methodResult");
                JsonSerializer.Serialize(writer, functionMethod.ResultType, options);

                writer.WritePropertyName("methodInput");
                JsonSerializer.Serialize(writer, functionMethod.InputType, options);

                writer.WritePropertyName("methodStreaming");
                writer.WriteStringValue(GetStreamingTag(functionMethod.InputType, functionMethod.ResultType));
                break;

            default:
                throw new JsonException($"Unknown Method type: {value.GetType().Name}");
        }

        writer.WriteEndObject();
    }

    private static string GetStreamingTag(MethodType input, MethodType result)
    {
        return (input, result) switch
        {
            (MethodType.Streaming, MethodType.Streaming) => "Duplex",
            (MethodType.Streaming, _) => "Client",
            (_, MethodType.Streaming) => "Server",
            _ => "Unary"
        };
    }
}
