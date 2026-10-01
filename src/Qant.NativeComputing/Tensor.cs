namespace Qant.NativeComputing;

/// <summary>
/// Dense, row-major (C-order) tensor of <see cref="BFloat16"/> values living in managed memory.
/// </summary>
public sealed class Tensor
{
    private readonly int[] _shape;
    private readonly BFloat16[] _data;

    /// <summary>Wraps <paramref name="data"/> (no copy) with the given shape.</summary>
    public Tensor(BFloat16[] data, params int[] shape)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.Length == 0)
            throw new ArgumentException("A tensor needs at least one dimension.", nameof(shape));

        long count = 1;
        foreach (int d in shape)
        {
            if (d <= 0)
                throw new ArgumentException($"All dimensions must be positive, got [{string.Join(", ", shape)}].", nameof(shape));
            count *= d;
        }
        if (count != data.Length)
            throw new ArgumentException($"Shape [{string.Join(", ", shape)}] has {count} elements, but data has {data.Length}.", nameof(data));

        _shape = (int[])shape.Clone();
        _data = data;
    }

    public int Rank => _shape.Length;
    public int Length => _data.Length;
    public ReadOnlySpan<int> Shape => _shape;
    public Span<BFloat16> Data => _data;

    internal BFloat16[] BackingArray => _data;

    /// <summary>Creates a tensor from float32 values (converted to bfloat16).</summary>
    public static Tensor FromArray(ReadOnlySpan<float> values, params int[] shape)
    {
        var data = new BFloat16[values.Length];
        for (int i = 0; i < data.Length; i++)
            data[i] = BFloat16.FromSingle(values[i]);
        return new Tensor(data, shape);
    }

    /// <summary>Creates a 1D tensor.</summary>
    public static Tensor FromArray(float[] values) => FromArray(values, values.Length);

    /// <summary>Creates a 2D tensor from a rectangular array.</summary>
    public static Tensor FromArray(float[,] values)
    {
        int rows = values.GetLength(0), cols = values.GetLength(1);
        var data = new BFloat16[rows * cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                data[r * cols + c] = BFloat16.FromSingle(values[r, c]);
        return new Tensor(data, rows, cols);
    }

    public static Tensor Zeros(params int[] shape)
    {
        long n = 1;
        foreach (int d in shape) n *= d;
        return new Tensor(new BFloat16[n], shape);
    }

    /// <summary>Row-major element access.</summary>
    public BFloat16 this[params int[] index]
    {
        get => _data[FlatIndex(index)];
        set => _data[FlatIndex(index)] = value;
    }

    public float[] ToSingleArray()
    {
        var result = new float[_data.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = _data[i].ToSingle();
        return result;
    }

    private int FlatIndex(int[] index)
    {
        if (index.Length != _shape.Length)
            throw new ArgumentException($"Expected {_shape.Length} indices, got {index.Length}.");
        int flat = 0;
        for (int i = 0; i < index.Length; i++)
        {
            if ((uint)index[i] >= (uint)_shape[i])
                throw new IndexOutOfRangeException($"Index {index[i]} out of range for dimension {i} (size {_shape[i]}).");
            flat = flat * _shape[i] + index[i];
        }
        return flat;
    }

    public override string ToString() => $"Tensor<bfloat16>[{string.Join(", ", _shape)}]";
}
