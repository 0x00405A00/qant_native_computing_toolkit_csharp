using System.Text;
using Qant.NativeComputing.Native;

namespace Qant.NativeComputing;

/// <summary>
/// <see cref="IQantDevice"/> backed by the native toolkit library (Q.ANT NPU, or the
/// toolkit's CPU backend when built with <c>-F cpu-backend</c>).
/// </summary>
public sealed unsafe class QantDevice : IQantDevice
{
    private bool _disposed;

    public uint Id { get; }

    /// <summary>Opens (initialises) the NPU with the given id.</summary>
    public QantDevice(uint id = 0)
    {
        NativeMethods.EnsureLoaded();
        Id = id;
        Check(NativeMethods.init_npu(id), "init_npu");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeMethods.release_npu(Id);
    }

    // ---------------- native ----------------

    public Tensor Multiply(Tensor us, Tensor vs)
    {
        RequireRank(us, 1, nameof(us)); RequireRank(vs, 1, nameof(vs));
        RequireSameShape(us, vs, nameof(vs));
        using var u = Pin(us); using var v = Pin(vs);
        return Consume(NativeMethods.mul_npu(Id, u.Pointer, v.Pointer), "mul_npu");
    }

    public Tensor ScaledPeriodicNonlinearity(Tensor features, Tensor weights)
    {
        RequireRank(features, 1, nameof(features)); RequireRank(weights, 1, nameof(weights));
        RequireSameShape(features, weights, nameof(weights));
        using var f = Pin(features); using var w = Pin(weights);
        return Consume(NativeMethods.calc_scaled_periodic_nl_fprop(Id, f.Pointer, w.Pointer), "calc_scaled_periodic_nl_fprop");
    }

    public Tensor Linear(Tensor features, Tensor weights)
    {
        RequireRank(features, 2, nameof(features)); RequireRank(weights, 2, nameof(weights));
        if (features.Shape[1] != weights.Shape[1])
            throw new ArgumentException($"features has {features.Shape[1]} input channels, weights {weights.Shape[1]}.");
        using var f = Pin(features); using var w = Pin(weights);
        return Consume(NativeMethods.linear_fprop(Id, f.Pointer, w.Pointer), "linear_fprop");
    }

    // ---------------- ai ----------------

    public Tensor AddBias(Tensor features, Tensor bias)
    {
        RequireRank(features, 2, nameof(features)); RequireRank(bias, 1, nameof(bias));
        using var f = Pin(features); using var b = Pin(bias);
        return Consume(NativeMethods.add_bias_fprop(Id, f.Pointer, b.Pointer), "add_bias_fprop");
    }

    public Tensor AddBiasConv2d(Tensor features, Tensor bias)
    {
        RequireRank(features, 4, nameof(features)); RequireRank(bias, 1, nameof(bias));
        using var f = Pin(features); using var b = Pin(bias);
        return Consume(NativeMethods.add_bias_for_conv2d_fprop(Id, f.Pointer, b.Pointer), "add_bias_for_conv2d_fprop");
    }

    public Tensor Relu(Tensor features)
    {
        RequireRank(features, 1, nameof(features));
        using var f = Pin(features);
        return Consume(NativeMethods.relu_fprop(Id, f.Pointer), "relu_fprop");
    }

    public Tensor Sigmoid(Tensor features)
    {
        RequireRank(features, 1, nameof(features));
        using var f = Pin(features);
        return Consume(NativeMethods.sigmoid_fprop(Id, f.Pointer), "sigmoid_fprop");
    }

    public Tensor Softmax(Tensor features)
    {
        RequireRank(features, 2, nameof(features));
        using var f = Pin(features);
        return Consume(NativeMethods.softmax_fprop(Id, f.Pointer), "softmax_fprop");
    }

    public Tensor Conv2d(Tensor features, Tensor kernels, ConvOptions options = default)
    {
        options = Normalize(options);
        RequireRank(features, 4, nameof(features)); RequireRank(kernels, 4, nameof(kernels));
        using var f = Pin(features); using var k = Pin(kernels);
        return Consume(NativeMethods.conv_fprop(Id, f.Pointer, k.Pointer,
            Size(options.Padding), Size(options.Stride), Size(options.Dilation)), "conv_fprop");
    }

    public Tensor ConvTranspose2d(Tensor features, Tensor kernels, ConvOptions options = default, int outputPadding = 0)
    {
        options = Normalize(options);
        RequireRank(features, 4, nameof(features)); RequireRank(kernels, 4, nameof(kernels));
        using var f = Pin(features); using var k = Pin(kernels);
        return Consume(NativeMethods.conv_transpose_fprop(Id, f.Pointer, k.Pointer,
            Size(options.Padding), Size(options.Stride), Size(options.Dilation), Size(outputPadding)), "conv_transpose_fprop");
    }

    public Tensor BatchNorm2d(Tensor features, Tensor means, Tensor variances, Tensor weights, Tensor bias, float epsilon = 1e-5f)
    {
        RequireRank(features, 4, nameof(features));
        RequireRank(means, 1, nameof(means)); RequireRank(variances, 1, nameof(variances));
        RequireRank(weights, 1, nameof(weights)); RequireRank(bias, 1, nameof(bias));
        using var f = Pin(features); using var m = Pin(means); using var v = Pin(variances);
        using var w = Pin(weights); using var b = Pin(bias);
        return Consume(NativeMethods.batchnorm2d_fprop(Id, f.Pointer, m.Pointer, v.Pointer, w.Pointer, b.Pointer, epsilon), "batchnorm2d_fprop");
    }

    public Tensor MaxPool2d(Tensor features, PoolOptions options)
    {
        RequireRank(features, 4, nameof(features));
        using var f = Pin(features);
        return Consume(NativeMethods.maxpool2d_fprop(Id, f.Pointer,
            Size(options.KernelHeight), Size(options.KernelWidth), Size(options.Padding), Size(options.Stride)), "maxpool2d_fprop");
    }

    public Tensor AvgPool2d(Tensor features, PoolOptions options, bool countIncludePad = false)
    {
        RequireRank(features, 4, nameof(features));
        using var f = Pin(features);
        return Consume(NativeMethods.avgpool2d_fprop(Id, f.Pointer,
            Size(options.KernelHeight), Size(options.KernelWidth), Size(options.Padding), Size(options.Stride), countIncludePad), "avgpool2d_fprop");
    }

    public Tensor AdaptiveMaxPool2d(Tensor features, int outputHeight, int outputWidth)
    {
        RequireRank(features, 4, nameof(features));
        using var f = Pin(features);
        return Consume(NativeMethods.adaptive_maxpool2d_fprop(Id, f.Pointer, Size(outputHeight), Size(outputWidth)), "adaptive_maxpool2d_fprop");
    }

    public Tensor AdaptiveAvgPool2d(Tensor features, int outputHeight, int outputWidth)
    {
        RequireRank(features, 4, nameof(features));
        using var f = Pin(features);
        return Consume(NativeMethods.adaptive_avgpool2d_fprop(Id, f.Pointer, Size(outputHeight), Size(outputWidth)), "adaptive_avgpool2d_fprop");
    }

    public Tensor KanLayer(Tensor features, Tensor phis, Tensor ampls, Tensor ks)
    {
        RequireRank(features, 2, nameof(features)); RequireRank(phis, 3, nameof(phis));
        RequireRank(ampls, 3, nameof(ampls)); RequireRank(ks, 1, nameof(ks));
        RequireSameShape(phis, ampls, nameof(ampls));
        using var f = Pin(features); using var p = Pin(phis); using var a = Pin(ampls); using var k = Pin(ks);
        return Consume(NativeMethods.calc_kan_layer_fprop(Id, f.Pointer, p.Pointer, a.Pointer, k.Pointer), "calc_kan_layer_fprop");
    }

    // ---------------- diagnostics ----------------

    public string GetDriverInfo()
    {
        var buffer = new byte[NativeMethods.CharArrLength];
        fixed (byte* p = buffer)
            Check(NativeMethods.get_driver_info(Id, p, (nuint)buffer.Length), "get_driver_info");
        return ReadCString(buffer);
    }

    public SensorInfo GetSensorInfo()
    {
        var s = NativeMethods.get_sensor_info(Id);
        return new SensorInfo(s.TempPd1, s.TempPd2, s.TempPd3, s.TempDac1, s.TempDac2,
            s.V1V8, s.V3V3, s.V6V0_1, s.V6V0_2, s.V6V2, s.V6V5, s.V8V0_1, s.V8V0_2,
            s.V12V, s.V12V5, s.VN12V5, s.I12V);
    }

    public VersionInfo GetVersionInfo()
    {
        var v = NativeMethods.get_version_info(Id);
        return new VersionInfo(
            ReadCString(new ReadOnlySpan<byte>(v.FwVersion, NativeMethods.CharArrLength)),
            ReadCString(new ReadOnlySpan<byte>(v.ZephyrVersion, NativeMethods.CharArrLength)),
            ReadCString(new ReadOnlySpan<byte>(v.BoardSerialNo, NativeMethods.CharArrLength)));
    }

    public void ResetPerformanceCounters() => Check(NativeMethods.reset_perf_counter(Id), "reset_perf_counter");

    public PerformanceCounters GetPerformanceCounters()
    {
        NativePerfCounter c;
        Check(NativeMethods.get_perf_counter(Id, &c), "get_perf_counter");
        return new PerformanceCounters(c.Timebased, c.Stalling);
    }

    // ---------------- helpers ----------------

    internal static void Check(int code, string function)
    {
        if (code != 0)
            throw new QantException($"'{function}' failed with error code {code}.", code);
    }

    internal static string ReadCString(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? bytes : bytes[..end]);
    }

    private PinnedTensor Pin(Tensor t)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new PinnedTensor(t);
    }

    private static Tensor Consume(DLManagedTensorVersioned* p, string op) => TensorMarshaller.Consume(p, op);

    private static nuint Size(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Must not be negative.");
        return (nuint)value;
    }

    private static ConvOptions Normalize(ConvOptions o) =>
        o == default ? new ConvOptions(0, 1, 1) : o; // default(ConvOptions) means "unspecified"

    private static void RequireRank(Tensor t, int rank, string name)
    {
        ArgumentNullException.ThrowIfNull(t, name);
        if (t.Rank != rank)
            throw new ArgumentException($"{name} must be {rank}-dimensional, got {t.Rank}-dimensional.", name);
    }

    private static void RequireSameShape(Tensor a, Tensor b, string name)
    {
        if (!a.Shape.SequenceEqual(b.Shape))
            throw new ArgumentException($"{name} must have shape [{string.Join(", ", a.Shape.ToArray())}], got [{string.Join(", ", b.Shape.ToArray())}].", name);
    }
}
