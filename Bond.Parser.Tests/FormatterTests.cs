using Bond.Parser.Formatting;
using FluentAssertions;

namespace Bond.Parser.Tests;

public class FormatterTests
{
    [Fact]
    public void Format_ReindentsAndSpaces()
    {
        var input = "namespace Test struct User{0:required string id;1:optional list<string> tags;}";
        var expected = """
            namespace Test

            struct User {
                0: required string id;
                1: optional list<string> tags;
            }
            """;

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }

    [Fact]
    public void Format_PreservesComments()
    {
        var input = """
            namespace Test
            // user struct
            struct User { /* fields */ 0: required string id; }
            """;

        var expected = """
            namespace Test

            // user struct
            struct User {
                /* fields */
                0: required string id;
            }
            """;

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }

    [Fact]
    public void Format_TopLevelBlankLines()
    {
        var input = """
            import "a.bond";import "b.bond";namespace Test struct A{0:required int32 id;} struct B{0:required int32 id;}
            """;

        var expected = """
            import "a.bond";
            import "b.bond";

            namespace Test

            struct A {
                0: required int32 id;
            }

            struct B {
                0: required int32 id;
            }
            """;

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }
}
