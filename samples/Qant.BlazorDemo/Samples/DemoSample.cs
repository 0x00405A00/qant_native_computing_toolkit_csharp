using Qant.NativeComputing;

namespace Qant.BlazorDemo.Samples;

public sealed record NamedTensor(string Name, Tensor Value);

/// <param name="Expected">Optional managed reference values for the output of the same name.</param>
public sealed record SampleOutput(string Name, Tensor Value, float[]? Expected = null);

public sealed record SampleRun(IReadOnlyList<NamedTensor> Inputs, IReadOnlyList<SampleOutput> Outputs, string? Note = null);

/// <param name="Operations">Keys into <see cref="OperationHints.All"/> for every operation the sample uses.</param>
public sealed record DemoSample(string Id, string Title, string Description, string[] Operations, Func<IQantDevice, bool, SampleRun> Run);

/// <summary>Result of executing a sample, including timing and the largest deviation from the reference.</summary>
public sealed record SampleResult(DemoSample Sample, SampleRun Run, TimeSpan Elapsed, IReadOnlyDictionary<string, double> MaxAbsError);
