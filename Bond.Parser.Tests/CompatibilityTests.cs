using Bond.Parser.Compatibility;
using Bond.Parser.Parser;
using Bond.Parser.Syntax;
using FluentAssertions;

namespace Bond.Parser.Tests;

public class CompatibilityTests
{
    private readonly CompatibilityChecker _checker = new();

    private async Task<Syntax.Bond> ParseSchema(string input)
    {
        var result = await BondParserFacade.ParseStringAsync(input);
        result.Success.Should().BeTrue($"parsing should succeed but got errors: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        return result.Ast!;
    }

    #region Field Changes - Breaking

    [Fact]
    public async Task AddingRequiredField_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string id; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User {
                0: required string id;
                1: required string email;
            }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.ToLower().Contains("required") &&
            c.Description.Contains("email"));
    }

    [Fact]
    public async Task RemovingRequiredField_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User {
                0: required string id;
                1: required string email;
            }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string id; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.Contains("removed"));
    }

    [Fact]
    public async Task ChangingFieldOrdinal_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string id; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 1: required string id; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        // Changing ordinals is detected as remove + add
        changes.Should().HaveCount(2);
        changes.Should().Contain(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.ToLower().Contains("removed"));
        changes.Should().Contain(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.ToLower().Contains("added"));
    }

    [Fact]
    public async Task ChangingFieldType_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required int32 age; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string age; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.ToLower().Contains("type"));
    }

    [Fact]
    public async Task ChangingDefaultValue_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required int32 status = 0; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required int32 status = 1; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.ToLower().Contains("default"));
    }

    [Fact]
    public async Task DirectOptionalToRequired_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: optional string name; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string name; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.ToLower().Contains("modifier"));
    }

    [Fact]
    public async Task DirectRequiredToOptional_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string name; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: optional string name; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.ToLower().Contains("modifier"));
    }

    #endregion

    #region Field Changes - Compatible

    [Fact]
    public async Task AddingOptionalField_IsCompatible()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string id; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User {
                0: required string id;
                1: optional string email;
            }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Compatible &&
            c.Description.Contains("email"));
    }

    [Fact]
    public async Task RemovingOptionalField_IsCompatible()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User {
                0: required string id;
                1: optional string email;
            }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string id; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Compatible &&
            c.Description.Contains("removed"));
    }

    [Fact]
    public async Task OptionalToRequiredOptional_IsCompatible()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: optional string name; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required_optional string name; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Compatible &&
            c.Description.ToLower().Contains("modifier"));
    }

    [Fact]
    public async Task RequiredOptionalToRequired_IsCompatible()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required_optional string name; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string name; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Compatible &&
            c.Description.ToLower().Contains("modifier"));
    }

    [Fact]
    public async Task Int32ToInt64Promotion_IsCompatible()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required int32 value; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required int64 value; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Compatible &&
            c.Description.Contains("int32") &&
            c.Description.Contains("int64"));
    }

    [Fact]
    public async Task FloatToDoublePromotion_IsCompatible()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required float value; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required double value; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Compatible &&
            c.Description.Contains("float") &&
            c.Description.Contains("double"));
    }

    [Fact]
    public async Task VectorToList_IsCompatible()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required vector<string> tags; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required list<string> tags; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Compatible &&
            c.Description.Contains("vector") &&
            c.Description.Contains("list"));
    }

    [Fact]
    public async Task Int32ToEnum_IsCompatible()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required int32 status; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            enum Status { Active = 0 }
            struct User { 0: required Status status; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Compatible &&
            c.Description.Contains("int32"));
    }

    #endregion

    #region Enum Changes

    [Fact]
    public async Task AddingEnumConstant_IsCompatible()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            enum Status { Active = 0 }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            enum Status { Active = 0, Inactive = 1 }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Compatible &&
            c.Description.Contains("Inactive"));
    }

    [Fact]
    public async Task ChangingEnumConstantValue_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            enum Status { Active = 0, Inactive = 1 }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            enum Status { Active = 0, Inactive = 5 }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.Contains("value") &&
            c.Description.Contains("Inactive"));
    }

    [Fact]
    public async Task RemovingEnumConstant_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            enum Status { Active = 0, Inactive = 1 }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            enum Status { Active = 0 }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.Contains("removed") &&
            c.Description.Contains("Inactive"));
    }

    #endregion

    #region Inheritance Changes

    [Fact]
    public async Task ChangingBaseStruct_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct Base1 { 0: required string id; }
            struct Base2 { 0: required string id; }
            struct User : Base1 { 1: required string name; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct Base1 { 0: required string id; }
            struct Base2 { 0: required string id; }
            struct User : Base2 { 1: required string name; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.Contains("Inheritance"));
    }

    [Fact]
    public async Task AddingBaseStruct_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct Base { 0: required string id; }
            struct User { 1: required string name; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct Base { 0: required string id; }
            struct User : Base { 1: required string name; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.Contains("Inheritance"));
    }

    #endregion

    #region Declaration Changes

    [Fact]
    public async Task RemovingDeclaration_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string id; }
            struct Profile { 0: required string bio; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string id; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.Contains("removed") &&
            c.Description.Contains("Profile"));
    }

    [Fact]
    public async Task AddingDeclaration_IsCompatible()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string id; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            struct User { 0: required string id; }
            struct Profile { 0: required string bio; }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Compatible &&
            c.Description.Contains("added") &&
            c.Description.Contains("Profile"));
    }

    [Fact]
    public async Task ChangingDeclarationType_IsBreaking()
    {
        var oldSchema = await ParseSchema("""
            namespace Test
            struct Status { 0: required int32 value; }
        """);
        var newSchema = await ParseSchema("""
            namespace Test
            enum Status { Active = 0 }
        """);

        var changes = _checker.CheckCompatibility(oldSchema, newSchema);

        changes.Should().ContainSingle(c =>
            c.Category == ChangeCategory.Breaking &&
            c.Description.Contains("kind changed"));
    }

    #endregion

    #region No Changes

    [Fact]
    public async Task IdenticalSchemas_NoChanges()
    {
        var schema = await ParseSchema("""
            namespace Test
            struct User {
                0: required string id;
                1: optional string name;
            }
        """);

        var changes = _checker.CheckCompatibility(schema, schema);

        changes.Should().BeEmpty();
    }

    #endregion
}
