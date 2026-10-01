using System.Text;
using Qant.NativeComputing.Native;

namespace Qant.NativeComputing;

/// <summary>Library-wide entry points: logging, device discovery, device creation.</summary>
public static unsafe class QantToolkit
{
    /// <summary>Major.minor version of the C API this wrapper was written against.</summary>
    public static readonly Version SupportedToolkitVersion = new(2, 3);

    /// <summary>Enables logging to stdout and to timestamped files in <paramref name="folder"/>.</summary>
    public static void SetupLogging(string folder, LogLevel level = LogLevel.Info)
    {
        NativeMethods.EnsureLoaded();
        var bytes = Encoding.UTF8.GetBytes(folder + '\0');
        fixed (byte* p = bytes)
            QantDevice.Check(NativeMethods.setup_logging(p, (int)level), "setup_logging");
    }

    /// <summary>Lists the NPUs available on this machine.</summary>
    public static IReadOnlyList<NpuDescriptor> GetAvailableDevices(int capacity = 16)
    {
        NativeMethods.EnsureLoaded();
        var ids = new uint[capacity];
        var serials = new byte[capacity * NativeMethods.CharArrLength];
        nuint count;
        fixed (uint* pi = ids)
        fixed (byte* ps = serials)
            QantDevice.Check(NativeMethods.get_available_npus(pi, ps, (nuint)capacity, &count), "get_available_npus");

        if ((int)count > capacity)
            return GetAvailableDevices((int)count);

        var result = new List<NpuDescriptor>((int)count);
        for (int i = 0; i < (int)count; i++)
        {
            var slice = serials.AsSpan(i * NativeMethods.CharArrLength, NativeMethods.CharArrLength);
            result.Add(new NpuDescriptor(ids[i], QantDevice.ReadCString(slice)));
        }
        return result;
    }

    /// <summary>Opens a device. Dispose it when done.</summary>
    public static IQantDevice OpenDevice(uint id = 0) => new QantDevice(id);
}
