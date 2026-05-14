namespace Bond.Parser.Syntax;

public abstract record MethodType
{
    private MethodType() { }

    public sealed record Void : MethodType
    {
        public static readonly Void Instance = new();
        private Void() { }
        public override string ToString() => "void";
    }

    public sealed record Unary(BondType Type) : MethodType
    {
        public override string ToString() => Type.ToString();
    }

    public sealed record Streaming(BondType Type) : MethodType
    {
        public override string ToString() => $"stream {Type}";
    }
}

public abstract record Method
{
    public required Attribute[] Attributes { get; init; }
    public required string Name { get; init; }
    public SourceLocation Location { get; init; } = SourceLocation.Unknown;
}

public sealed record FunctionMethod : Method
{
    public required MethodType ResultType { get; init; }
    public required MethodType InputType { get; init; }

    public override string ToString() => $"{ResultType} {Name}({InputType})";
}

/// <summary>Method with `nothing` return type and non-streaming input.</summary>
public sealed record EventMethod : Method
{
    public required MethodType InputType { get; init; }

    public override string ToString() => $"nothing {Name}({InputType})";
}
