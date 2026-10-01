using System.Runtime.InteropServices;

namespace Qant.NativeComputing.Native;

/// <summary>
/// Temporary DLPack view over a managed <see cref="Tensor"/>. The tensor data is pinned
/// (no copy); the header and shape live in unmanaged memory until disposed.
/// </summary>
internal sealed unsafe class PinnedTensor : IDisposable
{
    private GCHandle _handle;
    private DLManagedTensorVersioned* _header;

    public PinnedTensor(Tensor tensor)
    {
        _handle = GCHandle.Alloc(tensor.BackingArray, GCHandleType.Pinned);

        // header + shape in one block
        int ndim = tensor.Rank;
        nuint size = (nuint)(sizeof(DLManagedTensorVersioned) + ndim * sizeof(long));
        _header = (DLManagedTensorVersioned*)NativeMemory.AllocZeroed(size);
        long* shape = (long*)(_header + 1);
        for (int i = 0; i < ndim; i++) shape[i] = tensor.Shape[i];

        _header->VersionMajor = DLPackConstants.MajorVersion;
        _header->VersionMinor = DLPackConstants.MinorVersion;
        _header->Deleter = null; // the toolkit treats inputs as const and never frees them
        _header->Tensor.Data = (void*)_handle.AddrOfPinnedObject();
        _header->Tensor.Device = new DLDevice { DeviceType = DLPackConstants.DeviceCpu, DeviceId = 0 };
        _header->Tensor.NDim = ndim;
        _header->Tensor.DType = new DLDataType { Code = DLPackConstants.CodeBfloat, Bits = 16, Lanes = 1 };
        _header->Tensor.Shape = shape;
        _header->Tensor.Strides = null;
        _header->Tensor.ByteOffset = 0;
    }

    public DLManagedTensorVersioned* Pointer => _header;

    public void Dispose()
    {
        if (_header != null) { NativeMemory.Free(_header); _header = null; }
        if (_handle.IsAllocated) _handle.Free();
    }
}

internal static unsafe class TensorMarshaller
{
    /// <summary>
    /// Copies a toolkit-owned output tensor into managed memory and invokes its deleter.
    /// </summary>
    public static Tensor Consume(DLManagedTensorVersioned* p, string operation)
    {
        if (p == null)
            throw new QantException(
                $"'{operation}' failed (the toolkit returned a null tensor). Enable logging via QantToolkit.SetupLogging for details.");

        try
        {
            ref DLTensor t = ref p->Tensor;
            if (t.DType.Code != DLPackConstants.CodeBfloat || t.DType.Bits != 16 || t.DType.Lanes != 1)
                throw new QantException($"'{operation}' returned an unexpected dtype (code {t.DType.Code}, bits {t.DType.Bits}).");
            if (t.Strides != null)
                throw new QantException($"'{operation}' returned a strided tensor, which is not supported.");

            var shape = new int[t.NDim];
            long count = 1;
            for (int i = 0; i < shape.Length; i++)
            {
                shape[i] = checked((int)t.Shape[i]);
                count *= shape[i];
            }

            var data = new BFloat16[count];
            var src = new ReadOnlySpan<BFloat16>((byte*)t.Data + t.ByteOffset, (int)count);
            src.CopyTo(data);
            return new Tensor(data, shape);
        }
        finally
        {
            if (p->Deleter != null) p->Deleter(p);
        }
    }
}
