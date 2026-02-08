using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bond.Parser.Json;

/// <summary>
/// Provides JsonSerializerOptions configured for Bond AST serialization
/// matching the official Bond schema JSON format
/// </summary>
public static class BondJsonSerializerOptions
{
    /// <summary>
    /// Gets JsonSerializerOptions configured with all Bond JSON converters
    /// </summary>
    public static JsonSerializerOptions GetOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,  // Compact JSON by default
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            PropertyNamingPolicy = null  // Use exact property names from converters
        };

        // Register all custom converters
        options.Converters.Add(new BondJsonConverter());
        options.Converters.Add(new BondTypeJsonConverter());
        options.Converters.Add(new DefaultJsonConverter());
        options.Converters.Add(new DeclarationJsonConverter());
        options.Converters.Add(new FieldJsonConverter());
        options.Converters.Add(new AttributeJsonConverter());
        options.Converters.Add(new NamespaceJsonConverter());
        options.Converters.Add(new TypeParamJsonConverter());
        options.Converters.Add(new ConstantJsonConverter());
        options.Converters.Add(new ImportJsonConverter());
        options.Converters.Add(new MethodTypeJsonConverter());
        options.Converters.Add(new MethodJsonConverter());

        return options;
    }

    /// <summary>
    /// Gets JsonSerializerOptions for pretty-printed output
    /// </summary>
    public static JsonSerializerOptions GetPrettyOptions()
    {
        var options = GetOptions();
        options.WriteIndented = true;
        return options;
    }
}
