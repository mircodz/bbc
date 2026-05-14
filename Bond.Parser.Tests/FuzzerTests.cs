using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bond.Fuzzer;
using Bond.Parser.Parser;
using FluentAssertions;

namespace Bond.Parser.Tests;

public class FuzzerTests
{
    public static IEnumerable<object[]> SupportedMutations() =>
        Enum.GetValues<MutationKind>()
            .Where(kind => kind != MutationKind.Random)
            .Select(kind => new object[] { kind });

    [Fact]
    public async Task Generator_IsDeterministic_ForSameSeed()
    {
        var options = new FuzzerOptions(
            Seed: 123456,
            Mutation: MutationKind.WidenNumericField,
            StructCount: 3,
            MaxFieldsPerStruct: 4,
            MaxTypeDepth: 2);

        var first = await SchemaPairGenerator.GenerateAsync(options);
        var second = await SchemaPairGenerator.GenerateAsync(options);

        first.BeforeSchema.Should().Be(second.BeforeSchema);
        first.AfterSchema.Should().Be(second.AfterSchema);
        first.Mutation.Should().Be(second.Mutation);
        first.ObservedCategory.Should().Be(second.ObservedCategory);
    }

    [Theory]
    [MemberData(nameof(SupportedMutations))]
    public async Task Generator_ProducesParseableSchemas_ForSupportedMutations(MutationKind kind)
    {
        var pair = await SchemaPairGenerator.GenerateAsync(new FuzzerOptions(
            Seed: 5000 + (int)kind,
            Mutation: kind,
            StructCount: 3,
            MaxFieldsPerStruct: 5,
            MaxTypeDepth: 2));

        var before = await ParserFacade.ParseStringAsync(pair.BeforeSchema);
        var after = await ParserFacade.ParseStringAsync(pair.AfterSchema);

        before.Success.Should().BeTrue();
        after.Success.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(SupportedMutations))]
    public async Task Generator_ReportsExpectedCompatibility_ForSupportedMutations(MutationKind kind)
    {
        var pair = await SchemaPairGenerator.GenerateAsync(new FuzzerOptions(
            Seed: 7000 + (int)kind,
            Mutation: kind,
            StructCount: 3,
            MaxFieldsPerStruct: 5,
            MaxTypeDepth: 2));

        pair.ObservedCategory.Should().Be(pair.Mutation.ExpectedCategory,
            $"mutation {kind} should align with the current compatibility checker");
    }

    [Fact]
    public async Task ChangeAliasType_NormalizesTransitiveAliasDefaults()
    {
        var pair = await SchemaPairGenerator.GenerateAsync(new FuzzerOptions(
            Seed: 3309,
            Mutation: MutationKind.ChangeAliasType,
            EnumCount: 3,
            AliasCount: 2,
            StructCount: 4,
            MaxFieldsPerStruct: 5,
            MaxTypeDepth: 2));

        var before = await ParserFacade.ParseStringAsync(pair.BeforeSchema);
        var after = await ParserFacade.ParseStringAsync(pair.AfterSchema);

        before.Success.Should().BeTrue();
        after.Success.Should().BeTrue();
    }
}
