using Qant.NativeComputing;

namespace Qant.BlazorDemo.Samples;

public static class DemoSamples
{
    private static float[] Q(Tensor t) => t.ToSingleArray(); // bfloat16-rounded values, as the device sees them

    private static Tensor Image(int h, int w) =>
        Tensor.FromArray(Enumerable.Range(0, h * w).Select(i => (i / w + i % w) % 4 * 0.25f).ToArray(), 1, 1, h, w);

    public static IReadOnlyList<DemoSample> All { get; } =
    [
        new("linear", "Linear (x · Wᵀ)",
            "Vollständig verbundene Schicht: (2×3) · (4×3)ᵀ → (2×4). Die Gewichte sind implizit transponiert.",
            ["Linear"],
            (dev, _) =>
            {
                var x = Tensor.FromArray(new float[,] { { 0.5f, 0.25f, -0.5f }, { 1f, 0f, 0.75f } });
                var w = Tensor.FromArray(new float[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 }, { 0.5f, 0.5f, 0.5f } });
                var y = dev.Linear(x, w);
                var xs = Q(x); var ws = Q(w);
                var exp = new float[8];
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < 4; j++)
                        for (int k = 0; k < 3; k++)
                            exp[i * 4 + j] += xs[i * 3 + k] * ws[j * 3 + k];
                return new SampleRun([new("x", x), new("W", w)], [new("y", y, exp)]);
            }),

        new("activations", "Aktivierungen (ReLU, Sigmoid, Softmax)",
            "Drei Aktivierungsfunktionen auf demselben Eingangsvektor.",
            ["Relu", "Sigmoid", "Softmax"],
            (dev, _) =>
            {
                var x = Tensor.FromArray(new[] { -2f, -0.5f, 0f, 0.5f, 1f, 2f });
                var xs = Q(x);
                var relu = dev.Relu(x);
                var sigmoid = dev.Sigmoid(x);
                var x2 = Tensor.FromArray(xs, 1, xs.Length);
                var soft = dev.Softmax(x2);
                float sum = xs.Sum(MathF.Exp);
                return new SampleRun([new("x", x)],
                [
                    new("ReLU", relu, xs.Select(v => MathF.Max(0, v)).ToArray()),
                    new("Sigmoid", sigmoid, xs.Select(v => 1 / (1 + MathF.Exp(-v))).ToArray()),
                    new("Softmax (1×N)", soft, xs.Select(v => MathF.Exp(v) / sum).ToArray()),
                ]);
            }),

        new("conv", "Conv2d (Kantenfilter)",
            "Faltung eines 8×8-Musters mit einem horizontalen Kantenfilter (3×3), Padding 0, Stride 1. Dilation wird vom Toolkit 2.3 nicht unterstützt.",
            ["Conv2d"],
            (dev, _) =>
            {
                var img = Image(8, 8);
                var kernel = Tensor.FromArray(new float[] { -1, 0, 1, -2, 0, 2, -1, 0, 1 }, 1, 1, 3, 3);
                var y = dev.Conv2d(img, kernel, new ConvOptions(Padding: 0, Stride: 1, Dilation: 1));
                var exp = Reference.Conv(Q(img), [1, 1, 8, 8], Q(kernel), [1, 1, 3, 3], 0, 1);
                return new SampleRun([new("Bild", img), new("Kernel", kernel)], [new("Ergebnis", y, exp)]);
            }),

        new("pooling", "Pooling (Max und Average)",
            "2×2-Pooling mit Stride 2 auf einem 8×8-Muster.",
            ["MaxPool2d", "AvgPool2d"],
            (dev, _) =>
            {
                var img = Image(8, 8);
                var max = dev.MaxPool2d(img, new PoolOptions(kernelSize: 2, padding: 0, stride: 2));
                var avg = dev.AvgPool2d(img, new PoolOptions(kernelSize: 2, padding: 0, stride: 2));
                return new SampleRun([new("Bild", img)],
                [
                    new("MaxPool", max, Reference.Pool(Q(img), [1, 1, 8, 8], 2, 2, true)),
                    new("AvgPool", avg, Reference.Pool(Q(img), [1, 1, 8, 8], 2, 2, false)),
                ]);
            }),

        new("batchnorm", "BatchNorm2d",
            "Normalisierung pro Kanal: (x − μ) / √(σ² + ε) · γ + β. Das Toolkit 2.3 akzeptiert nur Batchgröße 1.",
            ["BatchNorm2d"],
            (dev, _) =>
            {
                var x = Tensor.FromArray(Enumerable.Range(0, 2 * 3 * 3).Select(i => (i % 5 - 2) * 0.5f).ToArray(), 1, 2, 3, 3);
                var mean = Tensor.FromArray(new[] { 0f, 0.5f });
                var variance = Tensor.FromArray(new[] { 1f, 0.25f });
                var gamma = Tensor.FromArray(new[] { 1f, 2f });
                var beta = Tensor.FromArray(new[] { 0f, 0.5f });
                const float eps = 1e-5f;
                var y = dev.BatchNorm2d(x, mean, variance, gamma, beta, eps);
                float[] xs = Q(x), m = Q(mean), v = Q(variance), g = Q(gamma), b = Q(beta);
                var exp = xs.Select((e, i) => { int c = i / 9; return (e - m[c]) / MathF.Sqrt(v[c] + eps) * g[c] + b[c]; }).ToArray();
                return new SampleRun([new("x", x), new("μ", mean), new("σ²", variance), new("γ", gamma), new("β", beta)],
                    [new("y", y, exp)]);
            }),

        new("bias", "Bias addieren (Dense und Conv)",
            "Addiert pro Kanal einen Bias-Wert: auf 2D-Features (Batch × Kanäle) und auf Conv-Feature-Maps (Batch × Kanäle × H × W).",
            ["AddBias", "AddBiasConv2d"],
            (dev, _) =>
            {
                var x = Tensor.FromArray(new float[,] { { 0.5f, 1f, -1f }, { 0f, 0.25f, 0.5f } });
                var b = Tensor.FromArray(new[] { 0.25f, -0.5f, 1f });
                var y = dev.AddBias(x, b);
                var xs = Q(x); var bs = Q(b);

                var fm = Tensor.FromArray(new float[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 1, 2, 2, 2);
                var cb = Tensor.FromArray(new[] { 0.5f, -1f });
                var y2 = dev.AddBiasConv2d(fm, cb);
                var fs = Q(fm); var cs = Q(cb);
                return new SampleRun([new("x (2×3)", x), new("bias (3)", b), new("Feature-Map (1×2×2×2)", fm), new("bias (2)", cb)],
                [
                    new("AddBias", y, xs.Select((v, i) => v + bs[i % 3]).ToArray()),
                    new("AddBiasConv2d", y2, fs.Select((v, i) => v + cs[i / 4]).ToArray()),
                ]);
            }),

        new("convtranspose", "ConvTranspose2d (Hochskalieren)",
            "Transponierte Faltung: vergrößert ein 3×3-Bild mit einem 2×2-Kernel und Stride 2 auf 6×6. Nur Batchgröße 1, Dilation 1 und output_padding 0.",
            ["ConvTranspose2d"],
            (dev, _) =>
            {
                var x = Tensor.FromArray(new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 1, 1, 3, 3);
                var k = Tensor.FromArray(new float[] { 1, 0.5f, 0.5f, 0.25f }, 1, 1, 2, 2);
                var y = dev.ConvTranspose2d(x, k, new ConvOptions(Padding: 0, Stride: 2, Dilation: 1));
                var exp = Reference.ConvTranspose(Q(x), [1, 1, 3, 3], Q(k), [1, 1, 2, 2], 0, 2);
                return new SampleRun([new("Bild", x), new("Kernel", k)], [new("Ergebnis", y, exp)]);
            }),

        new("adaptive", "Adaptive Pooling (feste Ausgabegröße)",
            "Reduziert ein 8×8-Muster auf 2×2 bzw. 1×1, unabhängig von der Eingabegröße. Die Eingabe muss ein ganzzahliges Vielfaches der Ausgabe sein.",
            ["AdaptiveMaxPool2d", "AdaptiveAvgPool2d"],
            (dev, _) =>
            {
                var img = Image(8, 8);
                var max = dev.AdaptiveMaxPool2d(img, 2, 2);
                var avg = dev.AdaptiveAvgPool2d(img, 1, 1);
                return new SampleRun([new("Bild", img)],
                [
                    new("AdaptiveMax 2×2", max, Reference.Pool(Q(img), [1, 1, 8, 8], 4, 4, true)),
                    new("AdaptiveAvg 1×1 (Global Average Pooling)", avg, Reference.Pool(Q(img), [1, 1, 8, 8], 8, 8, false)),
                ]);
            }),

        new("elementwise", "Elementweise Operationen",
            "Produkt zweier Vektoren und die skalierte periodische Nichtlinearität w = tcos(u)·v. Der Referenzvergleich für tcos gilt nur auf dem CPU-Backend.",
            ["Multiply", "ScaledPeriodicNonlinearity"],
            (dev, cpuBackend) =>
            {
                var u = Tensor.FromArray(new[] { 0f, 0.5f, 1f, 1.5f, 2f, 3f });
                var v = Tensor.FromArray(new[] { 1f, 0.5f, -1f, 2f, 0.25f, 1f });
                float[] us = Q(u), vs = Q(v);
                var mul = dev.Multiply(u, v);
                var nl = dev.ScaledPeriodicNonlinearity(u, v);
                return new SampleRun([new("u", u), new("v", v)],
                [
                    new("u · v", mul, us.Zip(vs, (a, b) => a * b).ToArray()),
                    new("tcos(u) · v", nl, cpuBackend ? us.Zip(vs, (a, b) => MathF.Cos(a) * b).ToArray() : null),
                ], cpuBackend ? null : "Kein Referenzvergleich für tcos: auf der Hardware nur kosinusähnlich.");
            }),

        new("kan", "KAN-Schicht",
            "yⱼ = Σᵢₗ tcos(kₗ·xᵢ + φⱼᵢₗ) · aⱼᵢₗ. Der Referenzvergleich mit einem echten Kosinus gilt nur auf dem CPU-Backend; auf der Hardware ist tcos nur kosinusähnlich.",
            ["KanLayer"],
            (dev, cpuBackend) =>
            {
                const int B = 2, In = 3, Out = 2, K = 3;
                var x = Tensor.FromArray(new float[] { 0.25f, -0.5f, 1f, 0f, 0.5f, -1f }, B, In);
                var phis = Tensor.FromArray(Enumerable.Range(0, Out * In * K).Select(i => (i % 5 - 2) * 0.25f).ToArray(), Out, In, K);
                var ampls = Tensor.FromArray(Enumerable.Range(0, Out * In * K).Select(i => (i % 3 - 1) * 0.5f).ToArray(), Out, In, K);
                var ks = Tensor.FromArray(new[] { 0.5f, 1f, 2f });
                var y = dev.KanLayer(x, phis, ampls, ks);
                float[]? exp = null;
                if (cpuBackend)
                {
                    float[] xs = Q(x), ph = Q(phis), am = Q(ampls), kk = Q(ks);
                    exp = new float[B * Out];
                    for (int b = 0; b < B; b++)
                        for (int j = 0; j < Out; j++)
                            for (int i = 0; i < In; i++)
                                for (int l = 0; l < K; l++)
                                    exp[b * Out + j] += MathF.Cos(kk[l] * xs[b * In + i] + ph[(j * In + i) * K + l]) * am[(j * In + i) * K + l];
                }
                return new SampleRun([new("x", x), new("φ", phis), new("a", ampls), new("k", ks)], [new("y", y, exp)],
                    cpuBackend ? null : "Kein Referenzvergleich: tcos ist auf der Hardware nur kosinusähnlich.");
            }),
    ];
}
