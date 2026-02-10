using Bond.Parser.Formatting;
using FluentAssertions;

namespace Bond.Parser.Tests;

public class FormatterTests
{
    private static string TrimEol(string text) => text.TrimEnd('\r', '\n');

    [Fact]
    public void Format_ReindentsAndSpaces()
    {
        var input = "namespace Test struct User{0:required string id;1:optional list<string> tags;}";
        var expected = TrimEol("""
            namespace Test

            struct User {
                0: required string id;
                1: optional list<string> tags;
            }
            """);

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }

    [Fact]
    public void Format_PreservesComments()
    {
        var input = TrimEol("""
            namespace Test
            // user struct
            struct User { /* fields */ 0: required string id; }
            """);

        var expected = TrimEol("""
            namespace Test

            // user struct
            struct User {
                /* fields */ 0: required string id;
            }
            """);

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }

    [Fact]
    public void Format_TopLevelBlankLines()
    {
        var input = TrimEol("""
            import "a.bond";import "b.bond";namespace Test struct A{0:required int32 id;} struct B{0:required int32 id;}
            """);

        var expected = TrimEol("""
            import "a.bond";
            import "b.bond";

            namespace Test

            struct A {
                0: required int32 id;
            }

            struct B {
                0: required int32 id;
            }
            """);

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }

    [Fact]
    public void Format_EmptyStructOnOneLine()
    {
        var input = TrimEol("""
            namespace Test
            struct Empty
            {}
            """);

        var expected = TrimEol("""
            namespace Test

            struct Empty {}
            """);

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }

    [Fact]
    public void Format_AttributesAndEmptyDerivedStruct()
    {
        var input = TrimEol("""
            namespace Test
            [StructAttribute1("one")][StructAttribute2("two")]
            struct DerivedEmpty:Foo
            {};
            """);

        var expected = TrimEol("""
            namespace Test

            [StructAttribute1("one")]
            [StructAttribute2("two")]
            struct DerivedEmpty : Foo {}
            """);

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }

    [Fact]
    public void Format_PreservesLiteralPrefix()
    {
        var input = "namespace Test struct Foo{0: optional wstring name = L\"hi\";}";
        var expected = TrimEol("""
            namespace Test

            struct Foo {
                0: optional wstring name = L"hi";
            }
            """);

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }

    [Fact]
    public void Format_UsingsStayTogether()
    {
        var input = "namespace Test using A = B;using C = D; struct Foo{}";
        var expected = TrimEol("""
            namespace Test

            using A = B;
            using C = D;

            struct Foo {}
            """);

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }

    [Fact]
    public void Format_RemovesStructSemicolon()
    {
        var input = TrimEol("""
            namespace Test
            struct Foo {};

            struct Empty
            {}
            """);

        var expected = TrimEol("""
            namespace Test

            struct Foo {}

            struct Empty {}
            """);

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }

    [Fact]
    public void Format_RemovesEnumSemicolon()
    {
        var input = TrimEol("""
            namespace Test
            enum Color { red, green }
            ;
            """);

        var expected = TrimEol("""
            namespace Test

            enum Color {
                red,
                green
            }
            """);

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }

    [Fact]
    public void Format_EnumValuesOnNewLines()
    {
        var input = TrimEol("""
            namespace Test
            enum Consts { Zero, One, Three = 3, Four, Six = 6 }
            """);

        var expected = TrimEol("""
            namespace Test

            enum Consts {
                Zero,
                One,
                Three = 3,
                Four,
                Six = 6
            }
            """);

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }

    [Fact]
    public void Format_DoesNotStripFieldSemicolons()
    {
        var input = TrimEol("""
            namespace Test
            struct Empty {}
            struct WithField { 0: required int32 id; }
            """);

        var expected = TrimEol("""
            namespace Test

            struct Empty {}

            struct WithField {
                0: required int32 id;
            }
            """);

        var result = BondFormatter.Format(input, "<inline>");

        result.Success.Should().BeTrue();
        result.FormattedText.Should().Be(expected);
    }
}
