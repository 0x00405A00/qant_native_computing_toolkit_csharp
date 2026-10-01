namespace Qant.NativeComputing;

/// <summary>
/// Backend-independent view of a Q.ANT processing unit. All tensors are bfloat16.
/// Code written against this interface can be tested with a fake or managed implementation.
/// </summary>
public interface IQantDevice : IDisposable
{
    uint Id { get; }

    // --- native operations ---

    /// <summary>Element-wise product of two 1D tensors of equal length.</summary>
    Tensor Multiply(Tensor us, Tensor vs);

    /// <summary>w = tcos(u) * v element-wise (1D, equal length).</summary>
    Tensor ScaledPeriodicNonlinearity(Tensor features, Tensor weights);

    /// <summary>features (batches, in) @ weights^T (out, in) -> (batches, out).</summary>
    Tensor Linear(Tensor features, Tensor weights);

    // --- AI operations ---

    /// <summary>features (batches, channels) + bias (channels).</summary>
    Tensor AddBias(Tensor features, Tensor bias);

    /// <summary>features (batches, channels, h, w) + bias (channels).</summary>
    Tensor AddBiasConv2d(Tensor features, Tensor bias);

    Tensor Relu(Tensor features);
    Tensor Sigmoid(Tensor features);

    /// <summary>features of shape (1, N).</summary>
    Tensor Softmax(Tensor features);

    /// <summary>features (b, c_in, h, w), kernels (c_out, c_in, kh, kw). Toolkit 2.3: dilation must be 1.</summary>
    Tensor Conv2d(Tensor features, Tensor kernels, ConvOptions options = default);

    /// <summary>features (b, c_in, h, w), kernels (c_in, c_out, kh, kw). Toolkit 2.3: batch 1, dilation 1, output padding 0.</summary>
    Tensor ConvTranspose2d(Tensor features, Tensor kernels, ConvOptions options = default, int outputPadding = 0);

    /// <summary>Toolkit 2.3: batch size 1 only.</summary>
    Tensor BatchNorm2d(Tensor features, Tensor means, Tensor variances, Tensor weights, Tensor bias, float epsilon = 1e-5f);
    Tensor MaxPool2d(Tensor features, PoolOptions options);
    Tensor AvgPool2d(Tensor features, PoolOptions options, bool countIncludePad = false);

    /// <summary>Input size must be an integer multiple of the output size.</summary>
    Tensor AdaptiveMaxPool2d(Tensor features, int outputHeight, int outputWidth);
    Tensor AdaptiveAvgPool2d(Tensor features, int outputHeight, int outputWidth);

    /// <summary>
    /// KAN layer: y_j = sum_{i,l} tcos(ks_l x_i + phi_jil) * ampl_jil.
    /// features (b, in), phis/ampls (out, in, n_ks), ks (n_ks).
    /// </summary>
    Tensor KanLayer(Tensor features, Tensor phis, Tensor ampls, Tensor ks);

    // --- diagnostics ---

    string GetDriverInfo();
    SensorInfo GetSensorInfo();
    VersionInfo GetVersionInfo();
    void ResetPerformanceCounters();
    PerformanceCounters GetPerformanceCounters();
}
