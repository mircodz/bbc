using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bond.Parser.Json;

/// <summary>
/// Base for converters that only serialize. Bond ASTs come from the parser,
/// not from JSON, so deserialization is unimplemented for every converter
/// in this module — this base centralizes that stub.
/// </summary>
internal abstract class WriteOnlyJsonConverter<T> : JsonConverter<T>
{
    public sealed override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException($"Deserialization of {typeof(T).Name} is not implemented");
}
