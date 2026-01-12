namespace Bond.Parser.Syntax;

/// <summary>
/// Represents default values for struct fields
/// </summary>
public abstract record Default
{
    private Default() { }

    public sealed record Bool(bool Value) : Default
    {
        public override string ToString() => Value ? "true" : "false";
    }

    public sealed record Integer(long Value) : Default
    {
        public override string ToString() => Value.ToString();
    }

    public sealed record Float(double Value) : Default
    {
        public override string ToString() => Value.ToString("G");
    }

    public sealed record String(string Value) : Default
    {
        public override string ToString() => $"\"{Value}\"";
    }

    public sealed record Enum(string Identifier) : Default
    {
        public override string ToString() => Identifier;
    }

    public sealed record Nothing : Default
    {
        public static readonly Nothing Instance = new();
        private Nothing() { }
        public override string ToString() => "nothing";
    }
}
