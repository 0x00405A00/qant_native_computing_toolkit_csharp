# ADR-0002 – DLPack-Structs von Hand spiegeln und Tensor-Eigentum explizit regeln

**Status:** Implemented
**Datum:** 2026-10-01
**Entscheider:** 
**Implementierungsplan:** [dlpack-structs-von-hand-spiegeln-und-tensor-eigentum-explizi.md](../features/dlpack-structs-von-hand-spiegeln-und-tensor-eigentum-explizi.md)

## Kontext
*Nachträglich dokumentiert (2026-10-01): Die Entscheidung wurde beim Bau des Wrappers ohne eigenes ADR getroffen. Die erwogenen Alternativen sind aus dem Ergebnis rekonstruiert, nicht aus damaligen Unterlagen belegt.*

Tensoren überqueren die Grenze als `DLManagedTensorVersioned` (DLPack 1.1; das Toolkit lehnt andere Major-Versionen ab). Eingaben sind geliehen (`const*`, das Toolkit ruft deren Deleter nie auf). Ausgaben werden vom Toolkit allokiert und müssen über den eigenen `deleter` freigegeben werden.

## Entscheidung
- DLPack-Structs werden von Hand gespiegelt (`Native/DLPack.cs`), nicht generiert.
- Eingaben: `PinnedTensor` pinnt das verwaltete `BFloat16[]` per `GCHandle` (keine Kopie), Header und Shape liegen in einem unverwalteten Block, Deleter ist null; Freigabe in `Dispose`.
- Ausgaben: `TensorMarshaller.Consume` prüft Dtype (bfloat16, 1 Lane) und `strides == null`, kopiert in einen verwalteten `Tensor` und ruft den Deleter immer in einem `finally` auf.
- Es werden nur CPU-Geräte (`kDLCPU`, Id 0) und bfloat16 erzeugt und akzeptiert.

## Konsequenzen
- Native Speicher gelangen nie in die öffentliche API; Aufrufer können nichts leaken oder doppelt freigeben.
- Eingaben kosten keine Kopie, Ausgaben eine Kopie.
- Strided Ausgaben oder andere Dtypes führen zu `QantException`.
- Die als deprecated markierten `mul_npu_f32` und `mul_npu_i16` (float32/int16) sind nicht gewrappt.
- Die Struct-Layouts wurden gegen `dlpack.h` v1.1 geprüft; bei DLPack-Updates erneut prüfen.

## Alternativen
- Generierte Bindings (wie im Toolkit mit bindgen): bräuchte eine Toolchain für die Header und ist für fünf Structs überdimensioniert.
- Tensoren dauerhaft nativ halten (Zero-Copy auch bei Ausgaben): erfordert `IDisposable`-Tensoren in der öffentlichen API und erhöht das Leckrisiko.
- Eingaben kopieren: einfacher, aber unnötig teuer.

## Links
- DLPack: https://github.com/dmlc/dlpack (v1.1)
- Upstream ADR 3 und 6 (manuelle und generierte DLPack-Bindings)
