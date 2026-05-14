using System.Text;
using Bond.Parser.Syntax;
using BondSchema = Bond.Parser.Syntax.Bond;

namespace Bond.Fuzzer;

internal static class BondSchemaWriter
{
    public static string Write(BondSchema schema)
    {
        var sb = new StringBuilder();

        foreach (var import in schema.Imports)
        {
            sb.Append("import \"").Append(import.FilePath).AppendLine("\";");
        }

        if (schema.Imports.Length > 0 && schema.Namespaces.Length > 0)
        {
            sb.AppendLine();
        }

        for (var i = 0; i < schema.Namespaces.Length; i++)
        {
            sb.Append(schema.Namespaces[i]).AppendLine();
        }

        if (schema.Namespaces.Length > 0 && schema.Declarations.Length > 0)
        {
            sb.AppendLine();
        }

        for (var i = 0; i < schema.Declarations.Length; i++)
        {
            WriteDeclaration(sb, schema.Declarations[i]);
            if (i < schema.Declarations.Length - 1)
            {
                sb.AppendLine().AppendLine();
            }
        }

        return sb.ToString();
    }

    private static void WriteDeclaration(StringBuilder sb, Declaration declaration)
    {
        switch (declaration)
        {
            case StructDeclaration structDeclaration:
                WriteStruct(sb, structDeclaration);
                return;
            case EnumDeclaration enumDeclaration:
                WriteEnum(sb, enumDeclaration);
                return;
            case AliasDeclaration aliasDeclaration:
                WriteAlias(sb, aliasDeclaration);
                return;
            default:
                throw new InvalidOperationException($"Unsupported declaration type: {declaration.GetType().Name}");
        }
    }

    private static void WriteStruct(StringBuilder sb, StructDeclaration structDeclaration)
    {
        sb.Append("struct ").Append(structDeclaration.Name).AppendLine();
        sb.AppendLine("{");

        foreach (var field in structDeclaration.Fields.OrderBy(f => f.Ordinal))
        {
            sb.Append("    ")
                .Append(field.Ordinal)
                .Append(": ")
                .Append(FormatModifier(field.Modifier))
                .Append(field.Type)
                .Append(' ')
                .Append(field.Name);

            if (field.DefaultValue is not null)
            {
                sb.Append(" = ").Append(field.DefaultValue);
            }

            sb.AppendLine(";");
        }

        sb.Append('}');
    }

    private static void WriteAlias(StringBuilder sb, AliasDeclaration aliasDeclaration)
    {
        sb.Append("using ")
            .Append(aliasDeclaration.Name)
            .Append(" = ")
            .Append(aliasDeclaration.AliasedType)
            .Append(';');
    }

    private static void WriteEnum(StringBuilder sb, EnumDeclaration enumDeclaration)
    {
        sb.Append("enum ").Append(enumDeclaration.Name).AppendLine();
        sb.AppendLine("{");

        for (var i = 0; i < enumDeclaration.Constants.Length; i++)
        {
            var constant = enumDeclaration.Constants[i];
            sb.Append("    ").Append(constant.Name);
            if (constant.Value.HasValue)
            {
                sb.Append(" = ").Append(constant.Value.Value);
            }

            if (i < enumDeclaration.Constants.Length - 1)
            {
                sb.Append(',');
            }

            sb.AppendLine();
        }

        sb.Append('}');
    }

    private static string FormatModifier(FieldModifier modifier) =>
        modifier switch
        {
            FieldModifier.Required => "required ",
            FieldModifier.RequiredOptional => "required_optional ",
            _ => "optional "
        };
}
