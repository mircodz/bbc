using Bond.Parser.Parser;
using Bond.Parser.Syntax;
using FluentAssertions;

namespace Bond.Parser.Tests;

public class ParserTests
{
    private async Task<ParseResult> Parse(string input, ImportResolver? importResolver = null)
    {
        return await BondParserFacade.ParseStringAsync(input, importResolver);
    }

    private static Task<(string, string)> MockImportResolver(string currentFile, string importPath)
    {
        // Return empty Bond file for any import
        return Task.FromResult((importPath, "namespace Mock"));
    }

    #region Comments

    [Fact]
    public async Task SingleLineComments_AreParsed()
    {
        var input = """
            namespace Test
            // This is a comment
            struct User {
                0: required string id; // Field comment
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        result.Ast!.Declarations.Should().ContainSingle();
    }

    [Fact]
    public async Task MultiLineComments_AreParsed()
    {
        var input = """
            namespace Test
            /* This is a
               multi-line comment */
            struct User {
                0: required string id; /* inline */
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        result.Ast!.Declarations.Should().ContainSingle();
    }

    #endregion

    #region String Escapes

    [Fact]
    public async Task StringWithEscapedQuotes_IsParsed()
    {
        var input = """
            namespace Test
            struct User {
                0: required string name = "John \"Doe\"";
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.DefaultValue.Should().BeOfType<Default.String>();
    }

    [Fact]
    public async Task StringWithBackslash_IsParsed()
    {
        var input = """
            namespace Test
            struct User {
                0: required string path = "C:\Users\test";
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task StringWithNewline_IsParsed()
    {
        var input = """
            namespace Test
            struct User {
                0: required string text = "line1\nline2";
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task StringWithTab_IsParsed()
    {
        var input = """
            namespace Test
            struct User {
                0: required string text = "col1\tcol2";
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
    }

    #endregion

    #region Optional Semicolons

    [Fact]
    public async Task NamespaceWithoutSemicolon_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required string id; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        result.Ast!.Namespaces.Should().ContainSingle();
    }

    [Fact]
    public async Task NamespaceWithSemicolon_IsParsed()
    {
        var input = """
            namespace Test;
            struct User { 0: required string id; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        result.Ast!.Namespaces.Should().ContainSingle();
    }

    [Fact]
    public async Task StructWithoutSemicolon_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required string id; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task StructWithSemicolon_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required string id; };
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task FieldWithoutSemicolon_Fails()
    {
        var input = """
            namespace Test
            struct User {
                0: required string id
                1: required string name;
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task EnumConstantCommaSeparated_IsParsed()
    {
        var input = """
            namespace Test
            enum Status {
                Active = 0,
                Inactive = 1
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var enumDecl = result.Ast!.Declarations[0] as EnumDeclaration;
        enumDecl!.Constants.Should().HaveCount(2);
    }

    [Fact]
    public async Task EnumConstantSemicolonSeparated_IsParsed()
    {
        var input = """
            namespace Test
            enum Status {
                Active = 0;
                Inactive = 1;
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
    }

    #endregion

    #region Field Modifiers

    [Fact]
    public async Task RequiredField_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required string id; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.Modifier.Should().Be(FieldModifier.Required);
    }

    [Fact]
    public async Task OptionalField_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: optional string name; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.Modifier.Should().Be(FieldModifier.Optional);
    }

    [Fact]
    public async Task RequiredOptionalField_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required_optional string name; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.Modifier.Should().Be(FieldModifier.RequiredOptional);
    }

    #endregion

    #region Default Values

    [Fact]
    public async Task IntegerDefault_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required int32 age = 25; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.DefaultValue.Should().BeOfType<Default.Integer>()
            .Which.Value.Should().Be(25);
    }

    [Fact]
    public async Task NegativeIntegerDefault_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required int32 value = -100; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.DefaultValue.Should().BeOfType<Default.Integer>()
            .Which.Value.Should().Be(-100);
    }

    [Fact]
    public async Task FloatDefault_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required float rate = 3.14; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.DefaultValue.Should().BeOfType<Default.Float>();
    }

    [Fact]
    public async Task BooleanTrueDefault_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required bool active = true; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.DefaultValue.Should().BeOfType<Default.Bool>()
            .Which.Value.Should().BeTrue();
    }

    [Fact]
    public async Task BooleanFalseDefault_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required bool active = false; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.DefaultValue.Should().BeOfType<Default.Bool>()
            .Which.Value.Should().BeFalse();
    }

    [Fact]
    public async Task NothingDefault_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: optional vector<string> tags = nothing; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.DefaultValue.Should().BeOfType<Default.Nothing>();
    }

    [Fact]
    public async Task EnumDefault_IsParsed()
    {
        var input = """
            namespace Test
            enum Status { Active = 0 }
            struct User { 0: required Status status = Active; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[1] as StructDeclaration)!.Fields[0];
        field.DefaultValue.Should().BeOfType<Default.Enum>()
            .Which.Identifier.Should().Be("Active");
    }

    #endregion

    #region Generics

    [Fact]
    public async Task GenericStruct_IsParsed()
    {
        var input = """
            namespace Test
            struct Box<T> { 0: required T value; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var structDecl = result.Ast!.Declarations[0] as StructDeclaration;
        structDecl!.TypeParameters.Should().ContainSingle()
            .Which.Name.Should().Be("T");
    }

    [Fact]
    public async Task GenericStructWithMultipleParams_IsParsed()
    {
        var input = """
            namespace Test
            struct Map<K, V> {
                0: required K key;
                1: required V value;
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var structDecl = result.Ast!.Declarations[0] as StructDeclaration;
        structDecl!.TypeParameters.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenericStructWithValueConstraint_IsParsed()
    {
        var input = """
            namespace Test
            struct NumericBox<T : value> { 0: required T value; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var structDecl = result.Ast!.Declarations[0] as StructDeclaration;
        structDecl!.TypeParameters.Should().ContainSingle()
            .Which.Constraint.Should().Be(TypeConstraint.Value);
    }

    #endregion

    #region Container Types

    [Fact]
    public async Task VectorType_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required vector<string> tags; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.Type.Should().BeOfType<BondType.Vector>();
    }

    [Fact]
    public async Task ListType_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required list<string> tags; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.Type.Should().BeOfType<BondType.List>();
    }

    [Fact]
    public async Task SetType_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required set<string> uniqueTags; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.Type.Should().BeOfType<BondType.Set>();
    }

    [Fact]
    public async Task MapType_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required map<string, int32> metadata; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.Type.Should().BeOfType<BondType.Map>();
    }

    [Fact]
    public async Task NullableType_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required nullable<string> middleName; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.Type.Should().BeOfType<BondType.Nullable>();
    }

    [Fact]
    public async Task BondedType_IsParsed()
    {
        var input = """
            namespace Test
            struct Envelope { 0: required bonded<User> payload; }
            struct User { 0: required string id; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.Type.Should().BeOfType<BondType.Bonded>();
    }

    #endregion

    #region Language-Qualified Namespaces

    [Fact]
    public async Task CppNamespace_IsParsed()
    {
        var input = """
            namespace cpp Test
            struct User { 0: required string id; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        result.Ast!.Namespaces.Should().ContainSingle()
            .Which.LanguageQualifier.Should().Be(Language.Cpp);
    }

    [Fact]
    public async Task CsNamespace_IsParsed()
    {
        var input = """
            namespace cs Test
            struct User { 0: required string id; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        result.Ast!.Namespaces.Should().ContainSingle()
            .Which.LanguageQualifier.Should().Be(Language.Cs);
    }

    [Fact]
    public async Task JavaNamespace_IsParsed()
    {
        var input = """
            namespace java Test
            struct User { 0: required string id; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        result.Ast!.Namespaces.Should().ContainSingle()
            .Which.LanguageQualifier.Should().Be(Language.Java);
    }

    #endregion

    #region Services

    [Fact]
    public async Task ServiceWithMethod_IsParsed()
    {
        var input = """
            namespace Test
            struct Request { 0: required string id; }
            struct Response { 0: required string result; }
            service MyService {
                Response GetData(Request);
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var serviceDecl = result.Ast!.Declarations[2] as ServiceDeclaration;
        serviceDecl!.Methods.Should().ContainSingle()
            .Which.Name.Should().Be("GetData");
    }

    [Fact]
    public async Task ServiceWithVoidMethod_IsParsed()
    {
        var input = """
            namespace Test
            service MyService {
                void Ping(void);
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ServiceWithEventMethod_IsParsed()
    {
        var input = """
            namespace Test
            struct Event { 0: required string type; }
            service MyService {
                nothing OnEvent(Event);
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var serviceDecl = result.Ast!.Declarations[1] as ServiceDeclaration;
        serviceDecl!.Methods.Should().ContainSingle()
            .Which.Should().BeOfType<EventMethod>();
    }

    #endregion

    #region Special Field Names

    [Fact]
    public async Task FieldNamedValue_IsParsed()
    {
        var input = """
            namespace Test
            struct User { 0: required string value; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.Name.Should().Be("value");
    }

    #endregion

    #region Imports

    [Fact]
    public async Task Import_IsParsed()
    {
        var input = """
            import "common.bond"
            namespace Test
            struct User { 0: required string id; }
        """;

        var result = await Parse(input, MockImportResolver);

        result.Success.Should().BeTrue();
        result.Ast!.Imports.Should().ContainSingle()
            .Which.FilePath.Should().Be("common.bond");
    }

    [Fact]
    public async Task MultipleImports_AreParsed()
    {
        var input = """
            import "common.bond"
            import "types.bond"
            namespace Test
            struct User { 0: required string id; }
        """;

        var result = await Parse(input, MockImportResolver);

        result.Success.Should().BeTrue();
        result.Ast!.Imports.Should().HaveCount(2);
    }

    #endregion

    #region Aliases

    [Fact]
    public async Task TypeAlias_IsParsed()
    {
        var input = """
            namespace Test
            using UserID = string;
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var aliasDecl = result.Ast!.Declarations[0] as AliasDeclaration;
        aliasDecl!.Name.Should().Be("UserID");
        aliasDecl.AliasedType.Should().BeOfType<BondType.String>();
    }

    [Fact]
    public async Task GenericTypeAlias_IsParsed()
    {
        var input = """
            namespace Test
            using StringMap<T> = map<string, T>;
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var aliasDecl = result.Ast!.Declarations[0] as AliasDeclaration;
        aliasDecl!.TypeParameters.Should().ContainSingle();
    }

    #endregion

    #region Forward Declarations

    [Fact]
    public async Task ForwardDeclaration_IsParsed()
    {
        var input = """
            namespace Test
            struct User;
            struct Profile { 0: required User user; }
            struct User { 0: required string id; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
    }

    #endregion

    #region Attributes

    [Fact]
    public async Task StructWithAttribute_IsParsed()
    {
        var input = """
            namespace Test
            [Deprecated("Use NewUser instead")]
            struct User { 0: required string id; }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var structDecl = result.Ast!.Declarations[0] as StructDeclaration;
        structDecl!.Attributes.Should().ContainSingle()
            .Which.QualifiedName.Should().BeEquivalentTo(new[] { "Deprecated" });
    }

    [Fact]
    public async Task FieldWithAttribute_IsParsed()
    {
        var input = """
            namespace Test
            struct User {
                [JsonName("user_id")]
                0: required string id;
            }
        """;

        var result = await Parse(input);

        result.Success.Should().BeTrue();
        var field = (result.Ast!.Declarations[0] as StructDeclaration)!.Fields[0];
        field.Attributes.Should().ContainSingle();
    }

    #endregion
}
