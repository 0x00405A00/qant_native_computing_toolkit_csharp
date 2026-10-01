namespace Qant.BlazorDemo.Samples;

/// <summary>Naive managed implementations used to cross-check the device results.</summary>
internal static class Reference
{
    public static float[] Conv(float[] x, int[] xs, float[] k, int[] ks, int pad, int stride)
    {
        int B = xs[0], Ci = xs[1], H = xs[2], W = xs[3], Co = ks[0], kh = ks[2], kw = ks[3];
        int Ho = (H + 2 * pad - kh) / stride + 1, Wo = (W + 2 * pad - kw) / stride + 1;
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
                                    int iy = oy * stride - pad + ky, ix = ox * stride - pad + kx;
                                    if (iy < 0 || iy >= H || ix < 0 || ix >= W) continue;
                                    s += x[((b * Ci + ci) * H + iy) * W + ix] * k[((co * Ci + ci) * kh + ky) * kw + kx];
                                }
                        o[((b * Co + co) * Ho + oy) * Wo + ox] = s;
                    }
        return o;
    }

    public static float[] Pool(float[] x, int[] xs, int kernel, int stride, bool max)
    {
        int N = xs[0] * xs[1], H = xs[2], W = xs[3];
        int Ho = (H - kernel) / stride + 1, Wo = (W - kernel) / stride + 1;
        var o = new float[N * Ho * Wo];
        for (int n = 0; n < N; n++)
            for (int oy = 0; oy < Ho; oy++)
                for (int ox = 0; ox < Wo; ox++)
                {
                    float acc = max ? float.NegativeInfinity : 0;
                    for (int ky = 0; ky < kernel; ky++)
                        for (int kx = 0; kx < kernel; kx++)
                        {
                            float v = x[(n * H + oy * stride + ky) * W + ox * stride + kx];
                            acc = max ? MathF.Max(acc, v) : acc + v;
                        }
                    o[(n * Ho + oy) * Wo + ox] = max ? acc : acc / (kernel * kernel);
                }
        return o;
    }

    public static float[] ConvTranspose(float[] x, int[] xs, float[] k, int[] ks, int pad, int stride)
    {
        int B = xs[0], Ci = xs[1], H = xs[2], W = xs[3], Co = ks[1], kh = ks[2], kw = ks[3];
        int Ho = (H - 1) * stride - 2 * pad + kh, Wo = (W - 1) * stride - 2 * pad + kw;
        var o = new float[B * Co * Ho * Wo];
        for (int b = 0; b < B; b++)
            for (int ci = 0; ci < Ci; ci++)
                for (int iy = 0; iy < H; iy++)
                    for (int ix = 0; ix < W; ix++)
                        for (int co = 0; co < Co; co++)
                            for (int ky = 0; ky < kh; ky++)
                                for (int kx = 0; kx < kw; kx++)
                                {
                                    int oy = iy * stride - pad + ky, ox = ix * stride - pad + kx;
                                    if (oy < 0 || oy >= Ho || ox < 0 || ox >= Wo) continue;
                                    o[((b * Co + co) * Ho + oy) * Wo + ox] +=
                                        x[((b * Ci + ci) * H + iy) * W + ix] * k[((ci * Co + co) * kh + ky) * kw + kx];
                                }
        return o;
    }
}
