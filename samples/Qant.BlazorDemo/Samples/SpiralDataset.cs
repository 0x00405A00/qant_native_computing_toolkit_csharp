namespace Qant.BlazorDemo.Samples;

/// <summary>Classic 2D spiral classification problem (not linearly separable).</summary>
public static class SpiralDataset
{
    public const int Dimensions = 2;

    /// <returns>Row-major features (n·classes × 2, in about [-1, 1]) and class labels.</returns>
    public static (float[] X, int[] Y) Generate(int perClass, int classes, float noise, int seed)
    {
        var rng = new Random(seed);
        int n = perClass * classes;
        var x = new float[n * Dimensions];
        var y = new int[n];
        for (int k = 0; k < classes; k++)
            for (int i = 0; i < perClass; i++)
            {
                int idx = k * perClass + i;
                float r = (i + 0.5f) / perClass;
                float t = k * 4f + 4f * r + Gaussian(rng) * noise;
                x[idx * 2] = r * MathF.Sin(t);
                x[idx * 2 + 1] = r * MathF.Cos(t);
                y[idx] = k;
            }
        return (x, y);
    }

    private static float Gaussian(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }
}
