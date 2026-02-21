using System;
using System.Collections.Generic;
using System.Numerics;

namespace Bond.Parser.Util;

public static class Extensions
{
    private static readonly Dictionary<Type, (BigInteger Min, BigInteger Max)> Bounds = new()
    {
        [typeof(sbyte)]  = (sbyte.MinValue,  sbyte.MaxValue),
        [typeof(byte)]   = (byte.MinValue,   byte.MaxValue),
        [typeof(short)]  = (short.MinValue,  short.MaxValue),
        [typeof(ushort)] = (ushort.MinValue, ushort.MaxValue),
        [typeof(int)]    = (int.MinValue,    int.MaxValue),
        [typeof(uint)]   = (uint.MinValue,   uint.MaxValue),
        [typeof(long)]   = (long.MinValue,   long.MaxValue),
        [typeof(ulong)]  = (0,               ulong.MaxValue),
    };

    /// <summary>
    /// Checks if a BigInteger value is within the bounds of a specific integral type.
    /// </summary>
    public static bool IsInBounds<T>(this BigInteger value) where T : struct =>
        Bounds.TryGetValue(typeof(T), out var b) && value >= b.Min && value <= b.Max;
}
