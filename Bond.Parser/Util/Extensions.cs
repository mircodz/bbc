namespace Bond.Parser.Util;

public static class Extensions
{
    /// <summary>
    /// Checks if an integer value is within the bounds of a specific integral type
    /// </summary>
    public static bool IsInBounds<T>(this long value) where T : struct
    {
        return value >= Convert.ToInt64(GetMinValue<T>()) &&
               value <= Convert.ToInt64(GetMaxValue<T>());
    }

    private static object GetMinValue<T>() where T : struct
    {
        var type = typeof(T);
        if (type == typeof(sbyte))
        {
            return sbyte.MinValue;
        }

        if (type == typeof(byte))
        {
            return byte.MinValue;
        }

        if (type == typeof(short))
        {
            return short.MinValue;
        }

        if (type == typeof(ushort))
        {
            return ushort.MinValue;
        }

        if (type == typeof(int))
        {
            return int.MinValue;
        }

        if (type == typeof(uint))
        {
            return uint.MinValue;
        }

        if (type == typeof(long))
        {
            return long.MinValue;
        }

        if (type == typeof(ulong))
        {
            return (long)0;
        }

        throw new NotSupportedException($"Type {type} is not supported");
    }

    private static object GetMaxValue<T>() where T : struct
    {
        var type = typeof(T);
        if (type == typeof(sbyte))
        {
            return sbyte.MaxValue;
        }

        if (type == typeof(byte))
        {
            return byte.MaxValue;
        }

        if (type == typeof(short))
        {
            return short.MaxValue;
        }

        if (type == typeof(ushort))
        {
            return ushort.MaxValue;
        }

        if (type == typeof(int))
        {
            return int.MaxValue;
        }

        if (type == typeof(uint))
        {
            return uint.MaxValue;
        }

        if (type == typeof(long))
        {
            return long.MaxValue;
        }

        if (type == typeof(ulong))
        {
            return long.MaxValue; // Approximation
        }

        throw new NotSupportedException($"Type {type} is not supported");
    }
}
