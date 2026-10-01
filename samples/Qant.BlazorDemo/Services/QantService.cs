using Qant.NativeComputing;

namespace Qant.BlazorDemo.Services;

/// <summary>
/// Owns the single device used by the demo. Calls are serialised because one NPU is shared by all circuits.
/// </summary>
public sealed class QantService : IDisposable
{
    private readonly object _gate = new();
    private readonly uint _deviceId;
    private IQantDevice? _device;
    private Exception? _error;

    public QantService(IConfiguration config, ILogger<QantService> logger)
    {
        _deviceId = config.GetValue<uint>("Qant:DeviceId");
        var path = config["Qant:LibraryPath"];
        if (!string.IsNullOrWhiteSpace(path))
            Environment.SetEnvironmentVariable("QANT_NATIVE_LIB_PATH", Path.GetFullPath(path));
        LibraryPath = Environment.GetEnvironmentVariable("QANT_NATIVE_LIB_PATH");
        logger.LogInformation("Qant library path: {Path}", LibraryPath ?? "<default search path>");
    }

    public string? LibraryPath { get; }

    /// <summary>Last failure while loading the library or opening the device, if any.</summary>
    public Exception? Error { get { lock (_gate) return _error; } }

    /// <summary>True when the toolkit reports the simulated CPU backend.</summary>
    public bool IsCpuBackend { get; private set; }

    /// <summary>Runs <paramref name="action"/> on the device, opening it on first use.</summary>
    public T Run<T>(Func<IQantDevice, T> action)
    {
        lock (_gate)
        {
            if (_device is null)
            {
                try
                {
                    IsCpuBackend = QantToolkit.GetAvailableDevices().Any(d => d.Id == _deviceId && d.SerialNumber == "cpu_backend");
                    _device = QantToolkit.OpenDevice(_deviceId);
                    _error = null;
                }
                catch (Exception e)
                {
                    _error = e;
                    throw;
                }
            }
            return action(_device);
        }
    }

    public IReadOnlyList<NpuDescriptor> ListDevices() => Run(_ => QantToolkit.GetAvailableDevices());

    public void Dispose()
    {
        lock (_gate) { _device?.Dispose(); _device = null; }
    }
}
