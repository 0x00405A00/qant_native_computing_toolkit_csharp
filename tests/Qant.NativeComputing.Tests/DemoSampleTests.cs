using Qant.BlazorDemo.Samples;
using Qant.NativeComputing;
using Xunit;

namespace Qant.NativeComputing.Tests;

public class DemoSampleTests
{
    private static readonly bool Available =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("QANT_NATIVE_LIB_PATH"));

    public static IEnumerable<object[]> Samples => DemoSamples.All.Select(s => new object[] { s.Id });

    [SkippableTheory]
    [MemberData(nameof(Samples))]
    public void SampleRunsAndMatchesItsReference(string id)
    {
        Skip.IfNot(Available);
        var sample = DemoSamples.All.Single(s => s.Id == id);
        bool cpu = QantToolkit.GetAvailableDevices().Any(d => d.SerialNumber == "cpu_backend");
        using var dev = QantToolkit.OpenDevice(0);

        var run = sample.Run(dev, cpu);

        Assert.NotEmpty(run.Outputs);
        foreach (var o in run.Outputs.Where(o => o.Expected is not null))
        {
            var actual = o.Value.ToSingleArray();
            Assert.Equal(o.Expected!.Length, actual.Length);
            for (int i = 0; i < actual.Length; i++)
                Assert.True(Math.Abs(actual[i] - o.Expected[i]) <= 0.05 + 0.01 * Math.Abs(o.Expected[i]),
                    $"{sample.Id}/{o.Name}[{i}] expected {o.Expected[i]}, got {actual[i]}");
        }
    }
}

public class OperationHintTests
{
    [Fact]
    public void EverySampleOperationHasAHint()
    {
        foreach (var op in DemoSamples.All.SelectMany(s => s.Operations))
            Assert.True(OperationHints.All.ContainsKey(op), $"missing hint for {op}");
    }

    [Fact]
    public void EveryDeviceOperationIsCoveredByASampleAndHasExamples()
    {
        string[] deviceOps = ["Multiply", "ScaledPeriodicNonlinearity", "Linear", "AddBias", "AddBiasConv2d", "Relu", "Sigmoid", "Softmax",
            "Conv2d", "ConvTranspose2d", "BatchNorm2d", "MaxPool2d", "AvgPool2d", "AdaptiveMaxPool2d", "AdaptiveAvgPool2d", "KanLayer"];
        var covered = DemoSamples.All.SelectMany(s => s.Operations).ToHashSet();
        foreach (var op in deviceOps)
        {
            Assert.Contains(op, covered);
            Assert.NotEmpty(OperationHints.All[op].Examples);
        }
    }
}
