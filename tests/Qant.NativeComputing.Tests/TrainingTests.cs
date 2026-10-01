using Qant.BlazorDemo.Samples;
using Qant.NativeComputing;
using Xunit;

namespace Qant.NativeComputing.Tests;

public class TrainingTests
{
    private static readonly bool Available =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("QANT_NATIVE_LIB_PATH"));

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return dot / Math.Sqrt(na * nb);
    }

    [SkippableFact]
    public void DeviceGradientsMatchNumericalGradients()
    {
        Skip.IfNot(Available);
        using var dev = QantToolkit.OpenDevice(0);
        var (x, y) = SpiralDataset.Generate(8, 3, 0.1f, 1);
        var mlp = new MlpTrainer(2, 8, 3, seed: 3);

        var g = mlp.ComputeGradients(dev, x, y, out float loss);

        Assert.True(Math.Abs(loss - mlp.LossHost(x, y)) < 0.05f, "device loss deviates from host loss");
        foreach (var (name, w, grad) in new[] { ("W1", mlp.W1, g.W1), ("B1", mlp.B1, g.B1), ("W2", mlp.W2, g.W2), ("B2", mlp.B2, g.B2) })
        {
            var numeric = new float[w.Length];
            const float h = 1e-2f;
            for (int i = 0; i < w.Length; i++)
            {
                float orig = w[i];
                w[i] = orig + h; float lp = mlp.LossHost(x, y);
                w[i] = orig - h; float lm = mlp.LossHost(x, y);
                w[i] = orig;
                numeric[i] = (lp - lm) / (2 * h);
            }
            double cos = Cosine(grad, numeric);
            Assert.True(cos > 0.97, $"{name}: cosine similarity {cos:F4}");
        }
    }

    [SkippableFact]
    public void TrainingLearnsTheSpiral()
    {
        Skip.IfNot(Available);
        using var dev = QantToolkit.OpenDevice(0);
        var (x, y) = SpiralDataset.Generate(100, 3, 0.1f, 1);
        var (tx, ty) = SpiralDataset.Generate(50, 3, 0.1f, 2);
        var mlp = new MlpTrainer(2, 32, 3, seed: 7);
        int n = y.Length, batch = 30;
        var order = Enumerable.Range(0, n).ToArray();
        var rng = new Random(5);
        float first = 0, last = 0;

        for (int epoch = 0; epoch < 150; epoch++)
        {
            rng.Shuffle(order);
            double sum = 0; int steps = 0;
            for (int s = 0; s + batch <= n; s += batch)
            {
                var xb = new float[batch * 2]; var yb = new int[batch];
                for (int i = 0; i < batch; i++)
                {
                    xb[i * 2] = x[order[s + i] * 2]; xb[i * 2 + 1] = x[order[s + i] * 2 + 1]; yb[i] = y[order[s + i]];
                }
                sum += mlp.Step(dev, xb, yb, 0.1f, 0.9f); steps++;
            }
            if (epoch == 0) first = (float)(sum / steps);
            last = (float)(sum / steps);
        }

        var pred = mlp.PredictDevice(dev, tx, ty.Length);
        double acc = pred.Zip(ty, (p, t) => p == t ? 1.0 : 0.0).Average();
        Assert.True(last < first * 0.5f, $"loss did not fall: {first} -> {last}");
        Assert.True(acc > 0.85, $"test accuracy only {acc:P0}");
    }
}
