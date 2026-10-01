using Qant.NativeComputing;

// Set QANT_NATIVE_LIB_PATH to the toolkit's shared library (see .vscode/launch.json).
Console.WriteLine($"Library: {Environment.GetEnvironmentVariable("QANT_NATIVE_LIB_PATH") ?? "<default search path>"}");

foreach (var npu in QantToolkit.GetAvailableDevices())
    Console.WriteLine($"NPU {npu.Id}: {npu.SerialNumber}");

using IQantDevice dev = QantToolkit.OpenDevice(0);

// Linear layer: (1,3) @ (2,3)^T -> (1,2)
var x = Tensor.FromArray(new float[,] { { 0.5f, 0.25f, 0.125f } });
var w = Tensor.FromArray(new float[,] { { 1, 0, 0 }, { 0, 1, 1 } });
var y = dev.Linear(x, w);
Console.WriteLine($"linear  -> {y}: [{string.Join(", ", y.ToSingleArray())}]");

// ReLU
var r = dev.Relu(Tensor.FromArray(new[] { -0.5f, 0.5f, -0.1f, 0.25f }));
Console.WriteLine($"relu    -> {r}: [{string.Join(", ", r.ToSingleArray())}]");

// 2x2 max pooling on a 4x4 image
var img = Tensor.FromArray(Enumerable.Range(1, 16).Select(i => (float)i).ToArray(), 1, 1, 4, 4);
var p = dev.MaxPool2d(img, new PoolOptions(kernelSize: 2, padding: 0, stride: 2));
Console.WriteLine($"maxpool -> {p}: [{string.Join(", ", p.ToSingleArray())}]");
