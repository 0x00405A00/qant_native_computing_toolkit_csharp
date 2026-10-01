using Qant.NativeComputing;
using Xunit;

namespace Qant.NativeComputing.Tests;

public class BFloat16Tests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(-2.5f)]
    [InlineData(0.5f)]
    public void ExactValuesRoundTrip(float v) => Assert.Equal(v, BFloat16.FromSingle(v).ToSingle());

    [Fact]
    public void RoundsToNearestEven()
    {
        // 1 + 2^-8 is exactly halfway between 1 and 1+2^-7 -> ties to even (1.0)
        Assert.Equal(1f, BFloat16.FromSingle(1f + 1f / 256f).ToSingle());
        Assert.Equal(1f + 1f / 128f, BFloat16.FromSingle(1f + 1.5f / 256f).ToSingle());
    }

    [Fact]
    public void NaNAndInfinityArePreserved()
    {
        Assert.True(float.IsNaN(BFloat16.FromSingle(float.NaN).ToSingle()));
        Assert.True(float.IsPositiveInfinity(BFloat16.FromSingle(float.PositiveInfinity).ToSingle()));
    }
}

public class TensorTests
{
    [Fact]
    public void FromArray2DIsRowMajor()
    {
        var t = Tensor.FromArray(new float[,] { { 1, 2, 3 }, { 4, 5, 6 } });
        Assert.Equal(new[] { 2, 3 }, t.Shape.ToArray());
        Assert.Equal(6f, t[1, 2].ToSingle());
        Assert.Equal(new float[] { 1, 2, 3, 4, 5, 6 }, t.ToSingleArray());
    }

    [Fact]
    public void ShapeMismatchThrows() =>
        Assert.Throws<ArgumentException>(() => Tensor.FromArray(new float[] { 1, 2, 3 }, 2, 2));

    [Fact]
    public void OutOfRangeIndexThrows() =>
        Assert.Throws<IndexOutOfRangeException>(() => Tensor.Zeros(2, 2)[2, 0]);
}
