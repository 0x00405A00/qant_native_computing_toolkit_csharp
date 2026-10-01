using System.Runtime.CompilerServices;

namespace Qant.NativeComputing;

/// <summary>
/// 16-bit brain floating point number (1 sign, 8 exponent, 7 mantissa bits).
/// This is the data type the toolkit operates on.
/// </summary>
public readonly struct BFloat16 : IEquatable<BFloat16>
{
    private readonly ushort _bits;

    private BFloat16(ushort bits) => _bits = bits;

    /// <summary>The raw IEEE-style bit pattern.</summary>
    public ushort Bits => _bits;

    public static BFloat16 FromBits(ushort bits) => new(bits);

    /// <summary>Converts from float32 using round-to-nearest-even.</summary>
    public static BFloat16 FromSingle(float value)
    {
        uint u = BitConverter.SingleToUInt32Bits(value);
        if (float.IsNaN(value))
            return new BFloat16((ushort)((u >> 16) | 0x0040)); // keep NaN a (quiet) NaN
        uint rounding = 0x7FFF + ((u >> 16) & 1);
        return new BFloat16((ushort)((u + rounding) >> 16));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ToSingle() => BitConverter.UInt32BitsToSingle((uint)_bits << 16);

    public static explicit operator BFloat16(float value) => FromSingle(value);
    public static explicit operator BFloat16(double value) => FromSingle((float)value);
    public static implicit operator float(BFloat16 value) => value.ToSingle();

    public bool Equals(BFloat16 other) => ToSingle().Equals(other.ToSingle());
    public override bool Equals(object? obj) => obj is BFloat16 other && Equals(other);
    public override int GetHashCode() => ToSingle().GetHashCode();
    public override string ToString() => ToSingle().ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static bool operator ==(BFloat16 a, BFloat16 b) => a.Equals(b);
    public static bool operator !=(BFloat16 a, BFloat16 b) => !a.Equals(b);
}
