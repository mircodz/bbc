using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Bond.Parser.Grammar;
using Bond.Parser.Syntax;

namespace Bond.Parser.Parser;

/// <summary>
/// Visitor that builds AST from ANTLR parse tree
/// </summary>
public class AstBuilder : BondBaseVisitor<object?>
{
    private readonly List<Namespace> _currentNamespaces = new();
    private readonly List<TypeParam> _currentTypeParams = new();

    public override Syntax.Bond VisitBond(BondParser.BondContext context)
    {
        var imports = context.import_()
            .Select(i => (Import)Visit(i)!)
            .ToArray();

        var namespaces = context.@namespace()
            .Select(n => (Namespace)Visit(n)!)
            .ToArray();

        _currentNamespaces.AddRange(namespaces);

        var declarations = context.declaration()
            .Select(d => (Declaration)Visit(d)!)
            .ToArray();

        return new Syntax.Bond(imports, namespaces, declarations);
    }

    public override Import VisitImport_(BondParser.Import_Context context)
    {
        var path = Unquote(context.STRING_LITERAL().GetText());
        return new Import(path);
    }

    public override Namespace VisitNamespace(BondParser.NamespaceContext context)
    {
        var lang = context.language() != null
            ? (Language?)Visit(context.language())
            : null;

        var name = (string[])Visit(context.qualifiedName())!;
        return new Namespace(lang, name);
    }

    public override object? VisitLanguage(BondParser.LanguageContext context)
    {
        return context.GetText() switch
        {
            "cpp" => Language.Cpp,
            "csharp" or "cs" => Language.Cs,
            "java" => Language.Java,
            _ => throw new InvalidOperationException($"Unknown language: {context.GetText()}")
        };
    }

    public override string[] VisitQualifiedName(BondParser.QualifiedNameContext context)
    {
        return context.IDENTIFIER().Select(id => id.GetText()).ToArray();
    }

    public override Declaration VisitDeclaration(BondParser.DeclarationContext context)
    {
        if (context.forward() != null)
            return (Declaration)Visit(context.forward())!;
        if (context.alias() != null)
            return (Declaration)Visit(context.alias())!;
        if (context.structDecl() != null)
            return (Declaration)Visit(context.structDecl())!;
        if (context.@enum() != null)
            return (Declaration)Visit(context.@enum())!;
        if (context.service() != null)
            return (Declaration)Visit(context.service())!;

        throw new InvalidOperationException("Unknown declaration type");
    }

    public override ForwardDeclaration VisitForward(BondParser.ForwardContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var typeParams = context.typeParameters() != null
            ? (TypeParam[])Visit(context.typeParameters())!
            : Array.Empty<TypeParam>();

        return new ForwardDeclaration
        {
            Namespaces = _currentNamespaces.ToArray(),
            Name = name,
            TypeParameters = typeParams
        };
    }

    public override AliasDeclaration VisitAlias(BondParser.AliasContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var typeParams = context.typeParameters() != null
            ? (TypeParam[])Visit(context.typeParameters())!
            : Array.Empty<TypeParam>();

        // Add type parameters to current scope for type resolution
        _currentTypeParams.AddRange(typeParams);

        var aliasedType = (BondType)Visit(context.type())!;

        // Remove type parameters from scope
        _currentTypeParams.RemoveRange(_currentTypeParams.Count - typeParams.Length, typeParams.Length);

        return new AliasDeclaration
        {
            Namespaces = _currentNamespaces.ToArray(),
            Name = name,
            TypeParameters = typeParams,
            AliasedType = aliasedType
        };
    }

    public override Declaration VisitStructDecl(BondParser.StructDeclContext context)
    {
        var attributes = context.attributes() != null
            ? (Syntax.Attribute[])Visit(context.attributes())!
            : Array.Empty<Syntax.Attribute>();

        var name = context.IDENTIFIER().GetText();
        var typeParams = context.typeParameters() != null
            ? (TypeParam[])Visit(context.typeParameters())!
            : Array.Empty<TypeParam>();

        // Add type parameters to current scope
        _currentTypeParams.AddRange(typeParams);

        Declaration result;
        if (context.structView() != null)
        {
            result = (Declaration)VisitStructView(context.structView(), name, typeParams, attributes)!;
        }
        else if (context.structDef() != null)
        {
            result = (Declaration)VisitStructDef(context.structDef(), name, typeParams, attributes)!;
        }
        else
        {
            throw new InvalidOperationException("Struct must have either view or definition");
        }

        // Remove type parameters from scope
        _currentTypeParams.RemoveRange(_currentTypeParams.Count - typeParams.Length, typeParams.Length);

        return result;
    }

    private StructDeclaration VisitStructView(BondParser.StructViewContext context, string name, TypeParam[] typeParams, Syntax.Attribute[] attributes)
    {
        var baseTypeName = (string[])Visit(context.qualifiedName())!;
        var fieldNames = context.viewFieldList().IDENTIFIER().Select(id => id.GetText()).ToHashSet();

        return new StructDeclaration
        {
            Namespaces = _currentNamespaces.ToArray(),
            Attributes = attributes,
            Name = name,
            TypeParameters = typeParams,
            BaseType = null,
            Fields = Array.Empty<Field>()
        };
    }

    private StructDeclaration VisitStructDef(BondParser.StructDefContext context, string name, TypeParam[] typeParams, Syntax.Attribute[] attributes)
    {
        var baseType = context.userType() != null
            ? (BondType)Visit(context.userType())!
            : null;

        var fields = context.field()
            .Select(f => (Field)Visit(f)!)
            .OrderBy(f => f.Ordinal)
            .ToArray();

        return new StructDeclaration
        {
            Namespaces = _currentNamespaces.ToArray(),
            Attributes = attributes,
            Name = name,
            TypeParameters = typeParams,
            BaseType = baseType,
            Fields = fields
        };
    }

    public override EnumDeclaration VisitEnum(BondParser.EnumContext context)
    {
        var attributes = context.attributes() != null
            ? (Syntax.Attribute[])Visit(context.attributes())!
            : Array.Empty<Syntax.Attribute>();

        var name = context.IDENTIFIER().GetText();
        var constants = context.enumConstant()
            .Select(c => (Constant)Visit(c)!)
            .ToArray();

        return new EnumDeclaration
        {
            Namespaces = _currentNamespaces.ToArray(),
            Attributes = attributes,
            Name = name,
            TypeParameters = Array.Empty<TypeParam>(),
            Constants = constants
        };
    }

    public override Constant VisitEnumConstant(BondParser.EnumConstantContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var value = context.INTEGER_LITERAL() != null
            ? (int)ParseInteger(context.INTEGER_LITERAL().GetText())
            : (int?)null;

        return new Constant(name, value);
    }

    public override ServiceDeclaration VisitService(BondParser.ServiceContext context)
    {
        var attributes = context.attributes() != null
            ? (Syntax.Attribute[])Visit(context.attributes())!
            : Array.Empty<Syntax.Attribute>();

        var name = context.IDENTIFIER().GetText();
        var typeParams = context.typeParameters() != null
            ? (TypeParam[])Visit(context.typeParameters())!
            : Array.Empty<TypeParam>();

        // Add type parameters to current scope
        _currentTypeParams.AddRange(typeParams);

        var baseType = context.serviceType() != null
            ? (BondType)Visit(context.serviceType())!
            : null;

        var methods = context.method()
            .Select(m => (Method)Visit(m)!)
            .ToArray();

        // Remove type parameters from scope
        _currentTypeParams.RemoveRange(_currentTypeParams.Count - typeParams.Length, typeParams.Length);

        return new ServiceDeclaration
        {
            Namespaces = _currentNamespaces.ToArray(),
            Attributes = attributes,
            Name = name,
            TypeParameters = typeParams,
            BaseType = baseType,
            Methods = methods
        };
    }

    public override Method VisitMethod(BondParser.MethodContext context)
    {
        var attributes = context.attributes() != null
            ? (Syntax.Attribute[])Visit(context.attributes())!
            : Array.Empty<Syntax.Attribute>();

        var name = context.IDENTIFIER().GetText();

        if (context.NOTHING() != null)
        {
            // Event method
            var inputType = context.methodParameter() != null
                ? (MethodType)Visit(context.methodParameter().methodInputType())!
                : MethodType.Void.Instance;

            return new EventMethod
            {
                Attributes = attributes,
                Name = name,
                InputType = inputType
            };
        }
        else
        {
            // Function method
            var resultType = (MethodType)Visit(context.methodResultType())!;
            var inputType = context.methodParameter() != null
                ? (MethodType)Visit(context.methodParameter().methodInputType())!
                : MethodType.Void.Instance;

            return new FunctionMethod
            {
                Attributes = attributes,
                Name = name,
                ResultType = resultType,
                InputType = inputType
            };
        }
    }

    public override MethodType VisitMethodResultType(BondParser.MethodResultTypeContext context)
    {
        if (context.VOID() != null)
            return MethodType.Void.Instance;
        if (context.methodTypeStreaming() != null)
            return (MethodType)Visit(context.methodTypeStreaming())!;
        if (context.methodTypeUnary() != null)
            return (MethodType)Visit(context.methodTypeUnary())!;

        throw new InvalidOperationException("Unknown method result type");
    }

    public override MethodType VisitMethodInputType(BondParser.MethodInputTypeContext context)
    {
        if (context.VOID() != null)
            return MethodType.Void.Instance;
        if (context.methodTypeStreaming() != null)
            return (MethodType)Visit(context.methodTypeStreaming())!;
        if (context.methodTypeUnary() != null)
            return (MethodType)Visit(context.methodTypeUnary())!;

        throw new InvalidOperationException("Unknown method input type");
    }

    public override MethodType VisitMethodTypeStreaming(BondParser.MethodTypeStreamingContext context)
    {
        var type = (BondType)Visit(context.userStructRef())!;
        return new MethodType.Streaming(type);
    }

    public override MethodType VisitMethodTypeUnary(BondParser.MethodTypeUnaryContext context)
    {
        var type = (BondType)Visit(context.userStructRef())!;
        return new MethodType.Unary(type);
    }

    public override Field VisitField(BondParser.FieldContext context)
    {
        var attributes = context.attributes() != null
            ? (Syntax.Attribute[])Visit(context.attributes())!
            : Array.Empty<Syntax.Attribute>();

        var ordinal = (ushort)ParseInteger(context.fieldOrdinal().INTEGER_LITERAL().GetText());

        var modifier = context.modifier() != null
            ? (FieldModifier)Visit(context.modifier())!
            : FieldModifier.Optional;

        var type = (BondType)Visit(context.fieldType())!;
        var name = context.fieldIdentifier().GetText();

        var defaultValue = context.default_() != null
            ? (Default?)Visit(context.default_())
            : null;

        // Handle 'nothing' default value - wrap type in Maybe
        if (defaultValue is Default.Nothing)
        {
            type = new BondType.Maybe(type);
        }

        return new Field(attributes, ordinal, modifier, type, name, defaultValue);
    }

    public override object? VisitModifier(BondParser.ModifierContext context)
    {
        if (context.REQUIRED_OPTIONAL() != null)
            return FieldModifier.RequiredOptional;
        if (context.REQUIRED() != null)
            return FieldModifier.Required;
        return FieldModifier.Optional;
    }

    public override BondType VisitFieldType(BondParser.FieldTypeContext context)
    {
        if (context.BOND_META_NAME() != null)
            return BondType.MetaName.Instance;
        if (context.BOND_META_FULL_NAME() != null)
            return BondType.MetaFullName.Instance;
        return (BondType)Visit(context.type())!;
    }

    public override BondType VisitType(BondParser.TypeContext context)
    {
        if (context.basicType() != null)
            return (BondType)Visit(context.basicType())!;
        if (context.complexType() != null)
            return (BondType)Visit(context.complexType())!;
        if (context.userType() != null)
            return (BondType)Visit(context.userType())!;

        throw new InvalidOperationException("Unknown type");
    }

    public override BondType VisitBasicType(BondParser.BasicTypeContext context)
    {
        return context.GetText() switch
        {
            "int8" => BondType.Int8.Instance,
            "int16" => BondType.Int16.Instance,
            "int32" => BondType.Int32.Instance,
            "int64" => BondType.Int64.Instance,
            "uint8" => BondType.UInt8.Instance,
            "uint16" => BondType.UInt16.Instance,
            "uint32" => BondType.UInt32.Instance,
            "uint64" => BondType.UInt64.Instance,
            "float" => BondType.Float.Instance,
            "double" => BondType.Double.Instance,
            "string" => BondType.String.Instance,
            "wstring" => BondType.WString.Instance,
            "bool" => BondType.Bool.Instance,
            _ => throw new InvalidOperationException($"Unknown basic type: {context.GetText()}")
        };
    }

    public override BondType VisitComplexType(BondParser.ComplexTypeContext context)
    {
        if (context.LIST() != null)
        {
            var elementType = (BondType)Visit(context.type())!;
            return new BondType.List(elementType);
        }
        if (context.BLOB() != null)
            return BondType.Blob.Instance;
        if (context.VECTOR() != null)
        {
            var elementType = (BondType)Visit(context.type())!;
            return new BondType.Vector(elementType);
        }
        if (context.NULLABLE() != null)
        {
            var elementType = (BondType)Visit(context.type())!;
            return new BondType.Nullable(elementType);
        }
        if (context.SET() != null)
        {
            var keyType = (BondType)Visit(context.keyType())!;
            return new BondType.Set(keyType);
        }
        if (context.MAP() != null)
        {
            var keyType = (BondType)Visit(context.keyType())!;
            var valueType = (BondType)Visit(context.type())!;
            return new BondType.Map(keyType, valueType);
        }
        if (context.BONDED() != null)
        {
            var structType = (BondType)Visit(context.userStructRef())!;
            return new BondType.Bonded(structType);
        }

        throw new InvalidOperationException("Unknown complex type");
    }

    public override BondType VisitKeyType(BondParser.KeyTypeContext context)
    {
        if (context.basicType() != null)
            return (BondType)Visit(context.basicType())!;
        if (context.userType() != null)
            return (BondType)Visit(context.userType())!;

        throw new InvalidOperationException("Unknown key type");
    }

    public override BondType VisitUserType(BondParser.UserTypeContext context)
    {
        var name = (string[])Visit(context.qualifiedName())!;

        // Check if this is a type parameter
        if (name.Length == 1)
        {
            var typeParam = _currentTypeParams.FirstOrDefault(p => p.Name == name[0]);
            if (typeParam != null)
            {
                return new BondType.TypeParameter(typeParam);
            }
        }

        var typeArgs = context.typeArgs() != null
            ? (BondType[])Visit(context.typeArgs())!
            : Array.Empty<BondType>();

        return new BondType.UnresolvedUserType(name, typeArgs);
    }

    public override BondType VisitUserStructRef(BondParser.UserStructRefContext context)
    {
        var name = (string[])Visit(context.qualifiedName())!;

        // Check if this is a type parameter
        if (name.Length == 1)
        {
            var typeParam = _currentTypeParams.FirstOrDefault(p => p.Name == name[0]);
            if (typeParam != null)
            {
                return new BondType.TypeParameter(typeParam);
            }
        }

        var typeArgs = context.typeArgs() != null
            ? (BondType[])Visit(context.typeArgs())!
            : Array.Empty<BondType>();

        return new BondType.UnresolvedUserType(name, typeArgs);
    }

    public override BondType VisitServiceType(BondParser.ServiceTypeContext context)
    {
        var name = (string[])Visit(context.qualifiedName())!;

        // Check if this is a type parameter
        if (name.Length == 1)
        {
            var typeParam = _currentTypeParams.FirstOrDefault(p => p.Name == name[0]);
            if (typeParam != null)
            {
                return new BondType.TypeParameter(typeParam);
            }
        }

        var typeArgs = context.typeArgs() != null
            ? (BondType[])Visit(context.typeArgs())!
            : Array.Empty<BondType>();

        return new BondType.UnresolvedUserType(name, typeArgs);
    }

    public override BondType[] VisitTypeArgs(BondParser.TypeArgsContext context)
    {
        return context.typeArg()
            .Select(a => (BondType)Visit(a)!)
            .ToArray();
    }

    public override BondType VisitTypeArg(BondParser.TypeArgContext context)
    {
        if (context.type() != null)
            return (BondType)Visit(context.type())!;
        if (context.INTEGER_LITERAL() != null)
        {
            var value = ParseInteger(context.INTEGER_LITERAL().GetText());
            return new BondType.IntTypeArg(value);
        }

        throw new InvalidOperationException("Unknown type argument");
    }

    public override TypeParam[] VisitTypeParameters(BondParser.TypeParametersContext context)
    {
        return context.typeParam()
            .Select(p => (TypeParam)Visit(p)!)
            .ToArray();
    }

    public override TypeParam VisitTypeParam(BondParser.TypeParamContext context)
    {
        var name = context.IDENTIFIER().GetText();
        var constraint = context.constraint() != null
            ? TypeConstraint.Value
            : TypeConstraint.None;

        return new TypeParam(name, constraint);
    }

    public override Default VisitDefault_(BondParser.Default_Context context)
    {
        if (context.TRUE() != null)
            return new Default.Bool(true);
        if (context.FALSE() != null)
            return new Default.Bool(false);
        if (context.NOTHING() != null)
            return Default.Nothing.Instance;
        if (context.STRING_LITERAL() != null)
        {
            var value = Unquote(context.STRING_LITERAL().GetText());
            return new Default.String(value);
        }
        if (context.FLOAT_LITERAL() != null)
        {
            var value = double.Parse(context.FLOAT_LITERAL().GetText());
            return new Default.Float(value);
        }
        if (context.INTEGER_LITERAL() != null)
        {
            var value = ParseInteger(context.INTEGER_LITERAL().GetText());
            return new Default.Integer(value);
        }
        if (context.IDENTIFIER() != null)
        {
            var identifier = context.IDENTIFIER().GetText();
            return new Default.Enum(identifier);
        }

        throw new InvalidOperationException("Unknown default value");
    }

    public override Syntax.Attribute[] VisitAttributes(BondParser.AttributesContext context)
    {
        return context.attribute()
            .Select(a => (Syntax.Attribute)Visit(a)!)
            .ToArray();
    }

    public override Syntax.Attribute VisitAttribute(BondParser.AttributeContext context)
    {
        var name = (string[])Visit(context.qualifiedName())!;
        var value = Unquote(context.STRING_LITERAL().GetText());
        return new Syntax.Attribute(name, value);
    }

    private static string Unquote(string str)
    {
        if (str.Length >= 2 && str[0] == '"' && str[^1] == '"')
        {
            return str[1..^1]
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t");
        }
        return str;
    }

    private static long ParseInteger(string str)
    {
        if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt64(str[2..], 16);
        }
        return long.Parse(str);
    }
}
