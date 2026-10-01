namespace Qant.NativeComputing;

/// <summary>Logging level; values match the toolkit's integer levels.</summary>
public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
}

/// <summary>Thrown when the native toolkit reports a failure.</summary>
public class QantException : Exception
{
    /// <summary>Native error code, or null when the call signalled failure by returning a null tensor.</summary>
    public int? ErrorCode { get; }

    public QantException(string message, int? errorCode = null) : base(message) => ErrorCode = errorCode;
}

/// <summary>An NPU found on this machine.</summary>
public readonly record struct NpuDescriptor(uint Id, string SerialNumber);

public readonly record struct SensorInfo(
    double TempPd1, double TempPd2, double TempPd3,
    double TempDac1, double TempDac2,
    double Voltage1V8, double Voltage3V3,
    double Voltage6V0_1, double Voltage6V0_2,
    double Voltage6V2, double Voltage6V5,
    double Voltage8V0_1, double Voltage8V0_2,
    double Voltage12V, double Voltage12V5, double VoltageN12V5,
    double Current12V);

public readonly record struct VersionInfo(string FirmwareVersion, string ZephyrVersion, string BoardSerialNumber);

/// <summary>Performance counters, in seconds since the last reset.</summary>
public readonly record struct PerformanceCounters(double TimeSinceReset, double StallingTime);

/// <summary>Convolution parameters (conv2d and transposed conv2d).</summary>
public readonly record struct ConvOptions(int Padding = 0, int Stride = 1, int Dilation = 1);

/// <summary>Pooling window parameters.</summary>
public readonly record struct PoolOptions(int KernelHeight, int KernelWidth, int Padding = 0, int Stride = 1)
{
    public PoolOptions(int kernelSize, int padding = 0, int stride = 1) : this(kernelSize, kernelSize, padding, stride) { }
}
