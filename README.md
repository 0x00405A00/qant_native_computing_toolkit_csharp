# Qant.NativeComputing (C#)

C#-Wrapper für das [Q.ANT native computing toolkit](https://github.com/Q-ANT-GmbH/qant_native_computing_toolkit) (C-API v2.3, DLPack v1.1).

## Voraussetzung

Die Shared Library `libqant_native_computing_toolkit.so` (Release-`.deb`, oder `cargo build --release [-F cpu-backend]`).
Ohne Q.ANT-Hardware: CPU-Backend (`-F cpu-backend`). Die Library wird über den Standard-Suchpfad gefunden,
oder explizit über `QANT_NATIVE_LIB_PATH=/pfad/libqant_native_computing_toolkit.so`.

## Beispiel

```csharp
using Qant.NativeComputing;

QantToolkit.SetupLogging("./logs", LogLevel.Info);
foreach (var npu in QantToolkit.GetAvailableDevices()) Console.WriteLine($"{npu.Id}: {npu.SerialNumber}");

using IQantDevice dev = QantToolkit.OpenDevice(0);

var x = Tensor.FromArray(new float[,] { { 1, 2, 3 } });       // (batch, in)
var w = Tensor.FromArray(new float[,] { { 1, 0, 0 }, { 0, 1, 1 } }); // (out, in)
Tensor y = dev.Linear(x, w);                                   // x @ w^T -> (1, 2)
Console.WriteLine(string.Join(", ", y.ToSingleArray()));
```

## Aufbau

- `IQantDevice` – abstrakte Schnittstelle (Mock-/Fake-bar), alle Operationen + Diagnose
- `QantDevice` – Implementierung über P/Invoke
- `Tensor`, `BFloat16` – verwaltete bfloat16-Tensoren (row-major), Marshalling via DLPack (gepinnt, ohne Kopie auf Eingabeseite)
- `Native/` – interne P/Invoke- und DLPack-Structs

Nicht umgesetzt: die als deprecated markierten `mul_npu_f32` / `mul_npu_i16`.

## Demo-Anwendungen

- `samples/Qant.Sample` – Konsole (Linear, ReLU, MaxPool)
- `samples/Qant.BlazorDemo` – Blazor Server (net10.0): Geräte-/Diagnose-Seite, 6 Samples mit Referenzvergleich (Linear, Aktivierungen, Conv2d, Pooling, BatchNorm2d, KAN) und ein Playground für eigene Vektoren.
  Start in VS Code über das Debug-Profil „Qant Blazor Demo (CPU-Backend)“ (http://localhost:5080) oder
  `QANT_NATIVE_LIB_PATH=native/libqant_native_computing_toolkit.so dotnet run --project samples/Qant.BlazorDemo`.
  Der Library-Pfad lässt sich auch über `Qant:LibraryPath` in `appsettings.json` setzen.
