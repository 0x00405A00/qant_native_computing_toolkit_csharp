using System.Runtime.InteropServices;

namespace Qant.NativeComputing.Native;

// Mirrors dlpack.h v1.1 (DLManagedTensorVersioned).

internal static class DLPackConstants
{
    public const uint MajorVersion = 1;
    public const uint MinorVersion = 1;

    public const int DeviceCpu = 1;

    public const byte CodeInt = 0;
    public const byte CodeBfloat = 4;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DLDevice
{
    public int DeviceType;
    public int DeviceId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DLDataType
{
    public byte Code;
    public byte Bits;
    public ushort Lanes;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DLTensor
{
    public void* Data;
    public DLDevice Device;
    public int NDim;
    public DLDataType DType;
    public long* Shape;
    public long* Strides;
    public ulong ByteOffset;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DLManagedTensorVersioned
{
    public uint VersionMajor;
    public uint VersionMinor;
    public void* ManagerCtx;
    public delegate* unmanaged[Cdecl]<DLManagedTensorVersioned*, void> Deleter;
    public ulong Flags;
    public DLTensor Tensor;
}
