using Qant.NativeComputing;
using Xunit;

namespace Qant.NativeComputing.Tests;

/// <summary>
/// Runs against the real toolkit library (CPU backend is enough).
/// Set QANT_NATIVE_LIB_PATH to the shared library; otherwise the tests are skipped.
/// </summary>
public class IntegrationTests
{
    private static readonly bool Available =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("QANT_NATIVE_LIB_PATH"));

    private static IQantDevice Open() => QantToolkit.OpenDevice(0);

    private static void AssertClose(float[] expected, Tensor actual, float tol = 0.05f)
    {
        var a = actual.ToSingleArray();
        Assert.Equal(expected.Length, a.Length);
        for (int i = 0; i < a.Length; i++)
            Assert.True(MathF.Abs(expected[i] - a[i]) <= tol, $"[{i}] expected {expected[i]}, got {a[i]}");
    }

    [SkippableFact]
    public void Multiply()
    {
        Skip.IfNot(Available);
        using var dev = Open();
        var r = dev.Multiply(Tensor.FromArray(new[] { 0.5f, 0.2f, 0.3f, 0.4f }), Tensor.FromArray(new[] { 0.5f, 0.3f, 0.2f, 0.1f }));
        AssertClose(new[] { 0.25f, 0.06f, 0.06f, 0.04f }, r, 0.01f);
    }

    [SkippableFact]
    public void LinearUsesTransposedWeights()
    {
        Skip.IfNot(Available);
        using var dev = Open();
        var x = Tensor.FromArray(new float[,] { { 0.5f, 0.25f, 0.125f } });
        var w = Tensor.FromArray(new float[,] { { 1, 0, 0 }, { 0, 1, 1 } });
        var y = dev.Linear(x, w);
        Assert.Equal(new[] { 1, 2 }, y.Shape.ToArray());
        AssertClose(new[] { 0.5f, 0.375f }, y);
    }

    [SkippableFact]
    public void ReluAndAddBias()
    {
        Skip.IfNot(Available);
        using var dev = Open();
        AssertClose(new[] { 0f, 0.5f, 0f, 0.25f }, dev.Relu(Tensor.FromArray(new[] { -0.5f, 0.5f, -0.1f, 0.25f })));
        var y = dev.AddBias(Tensor.FromArray(new float[,] { { 0.25f, 0.5f } }), Tensor.FromArray(new[] { 0.25f, -0.25f }));
        AssertClose(new[] { 0.5f, 0.25f }, y);
    }

    [SkippableFact]
    public void MaxPool2d()
    {
        Skip.IfNot(Available);
        using var dev = Open();
        var x = Tensor.FromArray(new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }, 1, 1, 4, 4);
        var y = dev.MaxPool2d(x, new PoolOptions(2, 0, 2));
        Assert.Equal(new[] { 1, 1, 2, 2 }, y.Shape.ToArray());
        AssertClose(new float[] { 6, 8, 14, 16 }, y, 0.1f);
    }

    [SkippableFact]
    public void Diagnostics()
    {
        Skip.IfNot(Available);
        var devices = QantToolkit.GetAvailableDevices();
        using var dev = Open();
        Assert.NotNull(dev.GetDriverInfo());
        dev.ResetPerformanceCounters();
        _ = dev.GetPerformanceCounters();
        _ = devices;
    }

    [SkippableFact]
    public void InvalidShapeFailsCleanly()
    {
        Skip.IfNot(Available);
        using var dev = Open();
        Assert.Throws<ArgumentException>(() => dev.Linear(Tensor.Zeros(2), Tensor.Zeros(2, 2)));
    }
}
