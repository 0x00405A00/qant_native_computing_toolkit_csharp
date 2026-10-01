using Qant.NativeComputing;
using Xunit;

namespace Qant.NativeComputing.Tests;

/// <summary>
/// Compares Conv2d, ConvTranspose2d, BatchNorm2d and KanLayer against naive managed reference
/// implementations. Needs the real library (QANT_NATIVE_LIB_PATH). The KAN numerics additionally
/// require the CPU backend, because the hardware's "tcos" is only cosine-like.
/// </summary>
public class OperationReferenceTests
{
    private static readonly bool Available =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("QANT_NATIVE_LIB_PATH"));

    private static IQantDevice Open() => QantToolkit.OpenDevice(0);

    // deterministic values in {-1.0, -0.75, ..., 1.0}: exactly representable in bfloat16
    private static Tensor Pattern(int seed, params int[] shape)
    {
        int n = shape.Aggregate(1, (a, b) => a * b);
        var v = new float[n];
        for (int i = 0; i < n; i++) v[i] = ((i * 7 + seed * 5) % 9 - 4) * 0.25f;
        return Tensor.FromArray(v, shape);
    }

    private static void AssertClose(float[] expected, Tensor actual)
    {
        var a = actual.ToSingleArray();
        Assert.Equal(expected.Length, a.Length);
        for (int i = 0; i < a.Length; i++)
        {
            float tol = 0.02f + 0.01f * MathF.Abs(expected[i]); // bfloat16 has ~3 significant digits
            Assert.True(MathF.Abs(expected[i] - a[i]) <= tol, $"[{i}] expected {expected[i]}, got {a[i]}");
        }
    }

    // ---- references ----

    private static (float[] data, int[] shape) RefConv(Tensor x, Tensor k, int pad, int stride, int dil)
    {
        int B = x.Shape[0], Ci = x.Shape[1], H = x.Shape[2], W = x.Shape[3];
        int Co = k.Shape[0], kh = k.Shape[2], kw = k.Shape[3];
        int Ho = (H + 2 * pad - dil * (kh - 1) - 1) / stride + 1;
        int Wo = (W + 2 * pad - dil * (kw - 1) - 1) / stride + 1;
        var xs = x.ToSingleArray(); var ks = k.ToSingleArray();
        var o = new float[B * Co * Ho * Wo];
        for (int b = 0; b < B; b++)
            for (int co = 0; co < Co; co++)
                for (int oy = 0; oy < Ho; oy++)
                    for (int ox = 0; ox < Wo; ox++)
                    {
                        float s = 0;
                        for (int ci = 0; ci < Ci; ci++)
                            for (int ky = 0; ky < kh; ky++)
                                for (int kx = 0; kx < kw; kx++)
                                {
                                    int iy = oy * stride - pad + ky * dil, ix = ox * stride - pad + kx * dil;
                                    if (iy < 0 || iy >= H || ix < 0 || ix >= W) continue;
                                    s += xs[((b * Ci + ci) * H + iy) * W + ix] * ks[((co * Ci + ci) * kh + ky) * kw + kx];
                                }
                        o[((b * Co + co) * Ho + oy) * Wo + ox] = s;
                    }
        return (o, new[] { B, Co, Ho, Wo });
    }

    private static (float[] data, int[] shape) RefConvTranspose(Tensor x, Tensor k, int pad, int stride, int dil, int outPad)
    {
        int B = x.Shape[0], Ci = x.Shape[1], H = x.Shape[2], W = x.Shape[3];
        int Co = k.Shape[1], kh = k.Shape[2], kw = k.Shape[3];
        int Ho = (H - 1) * stride - 2 * pad + dil * (kh - 1) + outPad + 1;
        int Wo = (W - 1) * stride - 2 * pad + dil * (kw - 1) + outPad + 1;
        var xs = x.ToSingleArray(); var ks = k.ToSingleArray();
        var o = new float[B * Co * Ho * Wo];
        for (int b = 0; b < B; b++)
            for (int ci = 0; ci < Ci; ci++)
                for (int iy = 0; iy < H; iy++)
                    for (int ix = 0; ix < W; ix++)
                        for (int co = 0; co < Co; co++)
                            for (int ky = 0; ky < kh; ky++)
                                for (int kx = 0; kx < kw; kx++)
                                {
                                    int oy = iy * stride - pad + ky * dil, ox = ix * stride - pad + kx * dil;
                                    if (oy < 0 || oy >= Ho || ox < 0 || ox >= Wo) continue;
                                    o[((b * Co + co) * Ho + oy) * Wo + ox] +=
                                        xs[((b * Ci + ci) * H + iy) * W + ix] * ks[((ci * Co + co) * kh + ky) * kw + kx];
                                }
        return (o, new[] { B, Co, Ho, Wo });
    }

    // ---- Conv2d ----

    [SkippableTheory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 1)]
    [InlineData(2, 2, 1)]
    public void Conv2dMatchesReference(int pad, int stride, int dil)
    {
        Skip.IfNot(Available);
        using var dev = Open();
        var x = Pattern(1, 2, 3, 8, 8);
        var k = Pattern(2, 4, 3, 3, 3);
        var (expected, shape) = RefConv(x, k, pad, stride, dil);

        var y = dev.Conv2d(x, k, new ConvOptions(pad, stride, dil));

        Assert.Equal(shape, y.Shape.ToArray());
        AssertClose(expected, y);
    }

    [SkippableFact]
    public void Conv2dDefaultOptionsAreNoPaddingUnitStrideUnitDilation()
    {
        Skip.IfNot(Available);
        using var dev = Open();
        var x = Pattern(3, 1, 2, 5, 5);
        var k = Pattern(4, 3, 2, 2, 2);
        var (expected, shape) = RefConv(x, k, 0, 1, 1);

        var y = dev.Conv2d(x, k);

        Assert.Equal(shape, y.Shape.ToArray());
        AssertClose(expected, y);
    }

    // Toolkit 2.3 limitation: "dilation != 1 not implemented yet"
    [SkippableFact]
    public void Conv2dWithDilationIsNotSupportedByToolkit()
    {
        Skip.IfNot(Available);
        using var dev = Open();
        Assert.Throws<QantException>(() => dev.Conv2d(Pattern(1, 1, 1, 8, 8), Pattern(2, 1, 1, 3, 3), new ConvOptions(0, 1, 2)));
    }

    [SkippableFact]
    public void Conv2dRejectsWrongRank()
    {
        Skip.IfNot(Available);
        using var dev = Open();
        Assert.Throws<ArgumentException>(() => dev.Conv2d(Tensor.Zeros(3, 3), Tensor.Zeros(1, 1, 3, 3)));
    }

    // ---- ConvTranspose2d ----

    // Toolkit 2.3 limitations: batch size 1, dilation 1, output_padding 0.
    [SkippableTheory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    public void ConvTranspose2dMatchesReference(int pad, int stride)
    {
        const int dil = 1, outPad = 0;
        Skip.IfNot(Available);
        using var dev = Open();
        var x = Pattern(5, 1, 3, 4, 4);
        var k = Pattern(6, 3, 2, 3, 3); // (c_in, c_out, kh, kw)
        var (expected, shape) = RefConvTranspose(x, k, pad, stride, dil, outPad);

        var y = dev.ConvTranspose2d(x, k, new ConvOptions(pad, stride, dil), outPad);

        Assert.Equal(shape, y.Shape.ToArray());
        AssertClose(expected, y);
    }

    [SkippableFact]
    public void ConvTranspose2dUnsupportedParametersThrow()
    {
        Skip.IfNot(Available);
        using var dev = Open();
        var k = Pattern(6, 3, 2, 3, 3);
        Assert.Throws<QantException>(() => dev.ConvTranspose2d(Pattern(5, 1, 3, 4, 4), k, new ConvOptions(0, 1, 2)));  // dilation
        Assert.Throws<QantException>(() => dev.ConvTranspose2d(Pattern(5, 1, 3, 4, 4), k, new ConvOptions(1, 2, 1), 1)); // output_padding
        Assert.Throws<QantException>(() => dev.ConvTranspose2d(Pattern(5, 2, 3, 4, 4), k));                              // batch > 1
    }

    // ---- BatchNorm2d ----

    [SkippableFact]
    public void BatchNorm2dMatchesReference()
    {
        Skip.IfNot(Available);
        using var dev = Open();
        const int B = 1, C = 3, H = 4, W = 4; // toolkit 2.3: batch size 1 only
        const float eps = 1e-5f;
        var x = Pattern(7, B, C, H, W);
        var mean = Tensor.FromArray(new[] { 0.25f, -0.5f, 0f });
        var variance = Tensor.FromArray(new[] { 1f, 0.5f, 2f });
        var weight = Tensor.FromArray(new[] { 1f, 2f, 0.5f });
        var bias = Tensor.FromArray(new[] { 0f, 0.25f, -0.25f });

        var xs = x.ToSingleArray();
        var m = mean.ToSingleArray(); var v = variance.ToSingleArray();
        var w = weight.ToSingleArray(); var b = bias.ToSingleArray();
        var expected = new float[xs.Length];
        for (int i = 0; i < xs.Length; i++)
        {
            int c = i / (H * W) % C;
            expected[i] = (xs[i] - m[c]) / MathF.Sqrt(v[c] + eps) * w[c] + b[c];
        }

        var y = dev.BatchNorm2d(x, mean, variance, weight, bias, eps);

        Assert.Equal(new[] { B, C, H, W }, y.Shape.ToArray());
        AssertClose(expected, y);
    }

    [SkippableFact]
    public void BatchNorm2dBatchedInputThrows()
    {
        Skip.IfNot(Available);
        using var dev = Open();
        var c = Tensor.FromArray(new[] { 1f, 1f });
        Assert.Throws<QantException>(() => dev.BatchNorm2d(Pattern(1, 2, 2, 3, 3), c, c, c, c));
    }

    // ---- KAN layer ----

    [SkippableFact]
    public void KanLayerMatchesReferenceOnCpuBackend()
    {
        Skip.IfNot(Available);
        Skip.IfNot(QantToolkit.GetAvailableDevices().Any(d => d.SerialNumber == "cpu_backend"),
            "tcos is only a true cosine on the CPU backend");
        using var dev = Open();
        const int B = 3, In = 4, Out = 2, K = 3;
        var x = Pattern(8, B, In);
        var phis = Pattern(9, Out, In, K);
        var ampls = Pattern(10, Out, In, K);
        var ks = Tensor.FromArray(new[] { 0.5f, 1f, 2f });

        var xs = x.ToSingleArray(); var ph = phis.ToSingleArray();
        var am = ampls.ToSingleArray(); var kk = ks.ToSingleArray();
        var expected = new float[B * Out];
        for (int b = 0; b < B; b++)
            for (int j = 0; j < Out; j++)
            {
                float s = 0;
                for (int i = 0; i < In; i++)
                    for (int l = 0; l < K; l++)
                        s += MathF.Cos(kk[l] * xs[b * In + i] + ph[(j * In + i) * K + l]) * am[(j * In + i) * K + l];
                expected[b * Out + j] = s;
            }

        var y = dev.KanLayer(x, phis, ampls, ks);

        Assert.Equal(new[] { B, Out }, y.Shape.ToArray());
        AssertClose(expected, y);
    }

    [SkippableFact]
    public void KanLayerRejectsMismatchedPhiAndAmplShapes()
    {
        Skip.IfNot(Available);
        using var dev = Open();
        Assert.Throws<ArgumentException>(() =>
            dev.KanLayer(Tensor.Zeros(1, 2), Tensor.Zeros(1, 2, 3), Tensor.Zeros(1, 2, 4), Tensor.Zeros(3)));
    }
}
