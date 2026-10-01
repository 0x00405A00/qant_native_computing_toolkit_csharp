# ADR-0007 – Teststrategie: Unit-Tests plus CPU-Backend-Integrationstests

**Status:** Full Acceptance (Final)
**Datum:** 2026-10-01
**Entscheider:** 
**Implementierungsplan:** [teststrategie-unit-tests-plus-cpu-backend-integrationstests.md](../features/teststrategie-unit-tests-plus-cpu-backend-integrationstests.md)
**Commit:** 2eceeae

## Kontext
*Nachträglich dokumentiert (2026-10-01): Die Entscheidung wurde beim Bau des Wrappers ohne eigenes ADR getroffen. Die erwogenen Alternativen sind aus dem Ergebnis rekonstruiert, nicht aus damaligen Unterlagen belegt.*

Q.ANT-Hardware steht nicht allgemein zur Verfügung, das Toolkit lässt sich aber mit `cargo build --release --no-default-features -F cpu-backend` bauen (Rust 1.93 und das Submodul `external/dlpack` nötig). Das CPU-Backend ist eine einfache Simulation, `tcos` ist dort ein echter Kosinus und `get_available_npus` liefert die Seriennummer `cpu_backend`.

## Entscheidung
- Rein verwaltete Tests (`BFloat16`, `Tensor`) laufen immer.
- Integrationstests rufen die echte Bibliothek auf und nutzen `Xunit.SkippableFact`. Sie werden übersprungen, wenn `QANT_NATIVE_LIB_PATH` nicht gesetzt ist, damit `dotnet test` auch ohne Bibliothek grün bleibt.
- Rechenoperationen werden gegen einfache verwaltete Referenzimplementierungen verglichen (Conv, ConvTranspose, BatchNorm, KAN) oder gegen von Hand berechnete Werte.
- Der KAN-Zahlenvergleich läuft nur auf dem CPU-Backend (Seriennummer `cpu_backend`), weil `tcos` auf der Hardware nur kosinusähnlich ist.
- Zahlenvergleiche nutzen Toleranzen wegen der bfloat16-Genauigkeit.
- `run-integration-tests.sh` führt alles gegen `native/libqant_native_computing_toolkit.so` aus.

## Konsequenzen
- Abgedeckt sind alle 16 Operationen (teils über die Demo-Sample-Tests), Diagnose und Argumentprüfung. Nicht abgedeckt: Sensorinfo und Versionsinfo.
- Die Tests dokumentieren Toolkit-Grenzen (Dilation, Batchgröße) und schlagen bei einem Toolkit-Update an, wenn diese entfallen.
- `native/*.so` ist ein lokaler, reiner Linux-x64-CPU-Build und nicht im Repository (`.gitignore`); er muss bei Toolkit-Updates neu gebaut werden.
- Auf der Hardware wurde nichts getestet.

## Alternativen
- Nur Mocks gegen `IQantDevice`: würde P/Invoke und Marshalling nicht prüfen.
- Integrationstests ohne Skip-Mechanismus: rot ohne Bibliothek.
- Hardware-Tests in der CI: Hardware nicht verfügbar.

## Links
- Siehe ADR-0003, ADR-0005, ADR-0006
