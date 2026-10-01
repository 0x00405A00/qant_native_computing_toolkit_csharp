using System.Reflection;
using System.Runtime.InteropServices;

namespace Qant.NativeComputing.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeSensorInfo
{
    public double TempPd1, TempPd2, TempPd3, TempDac1, TempDac2;
    public double V1V8, V3V3, V6V0_1, V6V0_2, V6V2, V6V5, V8V0_1, V8V0_2, V12V, V12V5, VN12V5;
    public double I12V;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeVersionInfo
{
    public fixed byte FwVersion[NativeMethods.CharArrLength];
    public fixed byte ZephyrVersion[NativeMethods.CharArrLength];
    public fixed byte BoardSerialNo[NativeMethods.CharArrLength];
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePerfCounter
{
    public double Timebased;
    public double Stalling;
}

/// <summary>P/Invoke declarations for libqant_native_computing_toolkit.</summary>
internal static unsafe class NativeMethods
{
    public const string Lib = "qant_native_computing_toolkit";
    public const int CharArrLength = 1024;

    /// <summary>Environment variable that may point to the shared library file.</summary>
    public const string LibPathEnvVar = "QANT_NATIVE_LIB_PATH";

    static NativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolve);
    }

    /// <summary>Forces the static constructor (and thus the resolver) to run.</summary>
    public static void EnsureLoaded() { }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? path)
    {
        if (name != Lib) return IntPtr.Zero;
        string? explicitPath = Environment.GetEnvironmentVariable(LibPathEnvVar);
        if (!string.IsNullOrEmpty(explicitPath))
            return NativeLibrary.Load(explicitPath);
        return NativeLibrary.TryLoad(name, assembly, path, out var handle) ? handle : IntPtr.Zero;
    }

    // --- info ---
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int setup_logging(byte* folderName, int loglevel);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int get_driver_info(uint npuId, byte* output, nuint len);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern NativeSensorInfo get_sensor_info(uint npuId);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern NativeVersionInfo get_version_info(uint npuId);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int get_available_npus(uint* idxsOut, byte* serialsOut, nuint capacity, nuint* outCount);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int reset_perf_counter(uint npuId);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int get_perf_counter(uint npuId, NativePerfCounter* @out);

    // --- generic ---
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int init_npu(uint id);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int release_npu(uint id);

    // --- native ---
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* mul_npu(uint npuId, DLManagedTensorVersioned* us, DLManagedTensorVersioned* vs);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* calc_scaled_periodic_nl_fprop(uint npuId, DLManagedTensorVersioned* features, DLManagedTensorVersioned* weights);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* linear_fprop(uint npuId, DLManagedTensorVersioned* features, DLManagedTensorVersioned* weights);

    // --- ai ---
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* add_bias_fprop(uint npuId, DLManagedTensorVersioned* features, DLManagedTensorVersioned* bias);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* add_bias_for_conv2d_fprop(uint npuId, DLManagedTensorVersioned* features, DLManagedTensorVersioned* bias);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* relu_fprop(uint npuId, DLManagedTensorVersioned* features);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* sigmoid_fprop(uint npuId, DLManagedTensorVersioned* features);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* softmax_fprop(uint npuId, DLManagedTensorVersioned* features);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* conv_fprop(uint npuId, DLManagedTensorVersioned* features, DLManagedTensorVersioned* kernels, nuint padding, nuint stride, nuint dilation);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* conv_transpose_fprop(uint npuId, DLManagedTensorVersioned* features, DLManagedTensorVersioned* kernels, nuint padding, nuint stride, nuint dilation, nuint outputPadding);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* batchnorm2d_fprop(uint npuId, DLManagedTensorVersioned* features, DLManagedTensorVersioned* means, DLManagedTensorVersioned* variances, DLManagedTensorVersioned* weights, DLManagedTensorVersioned* bias, float eps);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* maxpool2d_fprop(uint npuId, DLManagedTensorVersioned* features, nuint kernelHeight, nuint kernelWidth, nuint padding, nuint stride);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* avgpool2d_fprop(uint npuId, DLManagedTensorVersioned* features, nuint kernelHeight, nuint kernelWidth, nuint padding, nuint stride, [MarshalAs(UnmanagedType.U1)] bool countIncludePad);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* adaptive_maxpool2d_fprop(uint npuId, DLManagedTensorVersioned* features, nuint outputHeight, nuint outputWidth);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* adaptive_avgpool2d_fprop(uint npuId, DLManagedTensorVersioned* features, nuint outputHeight, nuint outputWidth);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DLManagedTensorVersioned* calc_kan_layer_fprop(uint npuId, DLManagedTensorVersioned* features, DLManagedTensorVersioned* phis, DLManagedTensorVersioned* ampls, DLManagedTensorVersioned* ks);
}
