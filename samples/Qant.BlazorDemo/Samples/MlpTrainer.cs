using Qant.NativeComputing;

namespace Qant.BlazorDemo.Samples;

/// <summary>Gradients of all parameters of <see cref="MlpTrainer"/>.</summary>
public sealed record Gradients(float[] W1, float[] B1, float[] W2, float[] B2);

/// <summary>
/// Two-layer perceptron (Linear → ReLU → Linear → Softmax) trained with SGD + momentum.
/// The toolkit only offers forward operations, so training is split:
/// <list type="bullet">
/// <item>Forward pass and all matrix products of the backward pass run on the device (Linear, AddBias, Relu, Multiply, Softmax).</item>
/// <item>Loss, ReLU masks, bias gradients, transposes and the optimizer run on the host.</item>
/// <item>Parameters are kept as float32 master copies; the device sees them rounded to bfloat16.</item>
/// </list>
/// </summary>
public sealed class MlpTrainer
{
    public int InDim { get; }
    public int Hidden { get; }
    public int Classes { get; }

    // W1 (Hidden × InDim), W2 (Classes × Hidden): row = output unit, as Linear expects.
    public float[] W1 { get; }
    public float[] B1 { get; }
    public float[] W2 { get; }
    public float[] B2 { get; }

    private readonly float[] _vW1, _vB1, _vW2, _vB2; // momentum buffers

    public MlpTrainer(int inDim, int hidden, int classes, int seed)
    {
        InDim = inDim; Hidden = hidden; Classes = classes;
        var rng = new Random(seed);
        W1 = HeInit(hidden * inDim, inDim, rng);
        W2 = HeInit(classes * hidden, hidden, rng);
        B1 = new float[hidden];
        B2 = new float[classes];
        _vW1 = new float[W1.Length]; _vB1 = new float[B1.Length];
        _vW2 = new float[W2.Length]; _vB2 = new float[B2.Length];
    }

    // ---------------- training ----------------

    /// <summary>One SGD step on a mini-batch. Returns the mean cross-entropy loss before the update.</summary>
    public float Step(IQantDevice dev, float[] x, int[] y, float learningRate, float momentum)
    {
        var g = ComputeGradients(dev, x, y, out float loss);
        Apply(W1, _vW1, g.W1, learningRate, momentum);
        Apply(B1, _vB1, g.B1, learningRate, momentum);
        Apply(W2, _vW2, g.W2, learningRate, momentum);
        Apply(B2, _vB2, g.B2, learningRate, momentum);
        return loss;
    }

    /// <summary>Forward and backward pass; the parameters are not changed.</summary>
    public Gradients ComputeGradients(IQantDevice dev, float[] x, int[] y, out float loss)
    {
        int B = y.Length, H = Hidden, C = Classes, In = InDim;

        // ---- forward on the device ----
        var (z1, a1, logits) = ForwardDevice(dev, x, B);

        // softmax: the toolkit supports shape (1, N) only, so one call per sample
        var probs = new float[B * C];
        for (int b = 0; b < B; b++)
        {
            var row = Tensor.FromArray(logits.AsSpan(b * C, C), 1, C);
            dev.Softmax(row).ToSingleArray().CopyTo(probs, b * C);
        }

        // ---- loss and output gradient on the host: dL = (p - onehot) / B ----
        double sum = 0;
        var dL = new float[B * C];
        for (int b = 0; b < B; b++)
        {
            sum -= Math.Log(Math.Max(probs[b * C + y[b]], 1e-7f));
            for (int c = 0; c < C; c++)
                dL[b * C + c] = (probs[b * C + c] - (c == y[b] ? 1f : 0f)) / B;
        }
        loss = (float)(sum / B);

        // ---- backward: matrix products on the device ----
        // Linear(a (m×k), w (n×k)) = a · wᵀ  →  (m×n)
        // dW2 (C×H) = dLᵀ (C×B) · A1 (B×H)  = Linear(dLᵀ, A1ᵀ)
        var dW2 = MatMulT(dev, Transpose(dL, B, C), C, B, Transpose(a1, B, H), H, B);
        // dA1 (B×H) = dL (B×C) · W2 (C×H)   = Linear(dL, W2ᵀ)
        var dA1 = MatMulT(dev, dL, B, C, Transpose(W2, C, H), H, C);
        // ReLU backward: dZ1 = dA1 ⊙ [z1 > 0]  (elementwise product on the device)
        var mask = new float[B * H];
        for (int i = 0; i < mask.Length; i++) mask[i] = z1[i] > 0 ? 1f : 0f;
        var dZ1 = dev.Multiply(Tensor.FromArray(dA1), Tensor.FromArray(mask)).ToSingleArray();
        // dW1 (H×In) = dZ1ᵀ (H×B) · X (B×In) = Linear(dZ1ᵀ, Xᵀ)
        var dW1 = MatMulT(dev, Transpose(dZ1, B, H), H, B, Transpose(x, B, In), In, B);

        return new Gradients(dW1, ColumnSums(dZ1, B, H), dW2, ColumnSums(dL, B, C));
    }

    // ---------------- inference ----------------

    /// <summary>Predicted classes using the device (one batched call per layer).</summary>
    public int[] PredictDevice(IQantDevice dev, float[] x, int n)
    {
        var (_, _, logits) = ForwardDevice(dev, x, n);
        return ArgMax(logits, n, Classes);
    }

    /// <summary>Predicted classes using plain float32 math on the host (reference).</summary>
    public int[] PredictHost(float[] x, int n) => ArgMax(ForwardHost(x, n).logits, n, Classes);

    /// <summary>Mean cross-entropy on the host in float32/double precision (for gradient checks).</summary>
    public float LossHost(float[] x, int[] y)
    {
        int B = y.Length, C = Classes;
        var logits = ForwardHost(x, B).logits;
        double sum = 0;
        for (int b = 0; b < B; b++)
        {
            double max = double.NegativeInfinity;
            for (int c = 0; c < C; c++) max = Math.Max(max, logits[b * C + c]);
            double denom = 0;
            for (int c = 0; c < C; c++) denom += Math.Exp(logits[b * C + c] - max);
            sum -= logits[b * C + y[b]] - max - Math.Log(denom);
        }
        return (float)(sum / B);
    }

    // ---------------- internals ----------------

    private (float[] z1, float[] a1, float[] logits) ForwardDevice(IQantDevice dev, float[] x, int B)
    {
        int H = Hidden, C = Classes;
        var z1 = dev.AddBias(
            dev.Linear(Tensor.FromArray(x, B, InDim), Tensor.FromArray(W1, H, InDim)),
            Tensor.FromArray(B1)).ToSingleArray();
        // Relu works on 1D tensors, so the (B×H) activations are passed flattened
        var a1 = dev.Relu(Tensor.FromArray(z1)).ToSingleArray();
        var logits = dev.AddBias(
            dev.Linear(Tensor.FromArray(a1, B, H), Tensor.FromArray(W2, C, H)),
            Tensor.FromArray(B2)).ToSingleArray();
        return (z1, a1, logits);
    }

    private (float[] z1, float[] a1, float[] logits) ForwardHost(float[] x, int B)
    {
        int H = Hidden, C = Classes, In = InDim;
        var z1 = new float[B * H];
        var a1 = new float[B * H];
        var logits = new float[B * C];
        for (int b = 0; b < B; b++)
        {
            for (int h = 0; h < H; h++)
            {
                float s = B1[h];
                for (int i = 0; i < In; i++) s += x[b * In + i] * W1[h * In + i];
                z1[b * H + h] = s;
                a1[b * H + h] = MathF.Max(0, s);
            }
            for (int c = 0; c < C; c++)
            {
                float s = B2[c];
                for (int h = 0; h < H; h++) s += a1[b * H + h] * W2[c * H + h];
                logits[b * C + c] = s;
            }
        }
        return (z1, a1, logits);
    }

    // a (m×k) · wᵀ with w (n×k) → (m×n), computed by the device
    private static float[] MatMulT(IQantDevice dev, float[] a, int m, int k, float[] w, int n, int k2)
    {
        if (k != k2) throw new ArgumentException("Inner dimensions differ.");
        return dev.Linear(Tensor.FromArray(a, m, k), Tensor.FromArray(w, n, k)).ToSingleArray();
    }

    private static float[] Transpose(float[] a, int rows, int cols)
    {
        var t = new float[a.Length];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                t[c * rows + r] = a[r * cols + c];
        return t;
    }

    private static float[] ColumnSums(float[] a, int rows, int cols)
    {
        var s = new float[cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                s[c] += a[r * cols + c];
        return s;
    }

    private static int[] ArgMax(float[] logits, int n, int classes)
    {
        var result = new int[n];
        for (int b = 0; b < n; b++)
        {
            int best = 0;
            for (int c = 1; c < classes; c++)
                if (logits[b * classes + c] > logits[b * classes + best]) best = c;
            result[b] = best;
        }
        return result;
    }

    private static void Apply(float[] w, float[] v, float[] g, float lr, float momentum)
    {
        for (int i = 0; i < w.Length; i++)
        {
            v[i] = momentum * v[i] + g[i];
            w[i] -= lr * v[i];
        }
    }

    private static float[] HeInit(int count, int fanIn, Random rng)
    {
        var w = new float[count];
        float std = MathF.Sqrt(2f / fanIn);
        for (int i = 0; i < count; i++)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            w[i] = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)) * std;
        }
        return w;
    }
}
