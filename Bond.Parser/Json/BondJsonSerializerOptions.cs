using System.Text.Json;

namespace Bond.Parser.Json;

/// <summary>JsonSerializerOptions matching the upstream Bond schema JSON format.</summary>
public static class BondJsonSerializerOptions
{
    public static JsonSerializerOptions GetOptions()
    {
        var options = new JsonSerializerOptions();

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

    public static JsonSerializerOptions GetPrettyOptions()
    {
        var options = GetOptions();
        options.WriteIndented = true;
        return options;
    }
}
