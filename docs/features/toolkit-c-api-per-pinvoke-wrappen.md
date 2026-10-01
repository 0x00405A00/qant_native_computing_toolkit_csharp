# Feature: Toolkit-C-API per P/Invoke wrappen (Implementierungsplan)

> **Status: umgesetzt, 2026-10-01.** Grundlage: [ADR-0001](../adr/ADR-0001-toolkit-c-api-per-pinvoke-wrappen.md) (**Accepted**).
> Dieser Plan zerlegt die Umsetzung in Meilensteine und Aufgaben. Er enthaelt **keinen Code**.

**Rollenkuerzel:** **SE** software-engineer · **T** software-tester · **SEC** it-security · **COMP** it-compliance · **A** software-architekt.

## 0. Umsetzungsstand (laufend fortzuschreiben)

| Aufgabe | Stand | Anmerkung |
| --- | --- | --- |
| 1.1 | erledigt | |
| 1.2 | erledigt | |
| 1.3 | erledigt | |
| 1.4 | erledigt | |

## 1. Ziel und Abgrenzung

Die C-API des Toolkits 2.3 (25 Funktionen) wird direkt per P/Invoke aufrufbar gemacht; Nicht-Ziele: nativer Shim, Codegenerierung, die deprecated Funktionen `mul_npu_f32`/`mul_npu_i16`. Entscheidung und Alternativen: siehe ADR-0001. Die Umsetzung entstand vor dem Plan; der Plan dokumentiert nachträglich, was gebaut wurde.

## 2. Meilensteine und Aufgaben

| Nr. | Rolle | Aufgabe | Ergebnis |
| --- | --- | --- | --- |
| 1.1 | SE | P/Invoke-Deklarationen aller 25 Funktionen (Info, Generic, Native, AI) | `src/Qant.NativeComputing/Native/NativeMethods.cs` |
| 1.2 | SE | Blittable Structs für `QantSensorInfo`, `QantVersionInfo` (fixed char[1024]) und `QantPerformanceCounterInfo` | `NativeMethods.cs` |
| 1.3 | SE | Projekt auf `net8.0` mit `AllowUnsafeBlocks`, Deklarationen `internal` | `Qant.NativeComputing.csproj` |
| 1.4 | SE | Unterstützte C-API-Version dokumentieren | `QantToolkit.SupportedToolkitVersion` = 2.3 |

## 3. Tests und Abnahme

| Nr. | Rolle | Pruefung | Erwartung |
| --- | --- | --- | --- |
| T.1 | T | Alle Operationen laufen über P/Invoke gegen die CPU-Backend-Bibliothek | Integrationstests und Demo-Sample-Tests grün |
| T.2 | T | Diagnosefunktionen (Treiberinfo, Perf-Counter, Geräteliste) | Test `Diagnostics` grün |

Nach erfolgreicher Umsetzung: ADR auf `Implemented`. Nach Tests und Abnahme: ADR auf `Implementation Tested and Acceptance`.

## 4. Prüfnachweise

Je Status bewertet eine Rolle (siehe Skill `adr-workflow`, Schritt 6). Ergebnis und Befunde hier eintragen.

| Status | Rolle | Datum | Ergebnis | Befunde |
| --- | --- | --- | --- | --- |
| Implemented | software-engineer | 2026-10-01 | Build ohne Warnungen und Fehler; 42 Tests grün (CPU-Backend) | siehe offene Punkte |
| Implemented | code-review | | nicht durchgeführt | |
| Implementation Tested and Acceptance | software-tester | 2026-10-01 | Testlauf gegen CPU-Backend: 42 von 42 grün (Commit 2eceeae). Abnahme durch den Projektinhaber auf ausdrückliche Anweisung. | siehe offene Punkte (Hardware ungetestet) |
| Security Review | it-security | 2026-10-01 | nicht durchgeführt; Status auf ausdrückliche Anweisung des Projektinhabers gesetzt | keine Bewertung vorhanden |
| GDPR/Compliance Review | it-compliance | 2026-10-01 | nicht durchgeführt; Status auf ausdrückliche Anweisung des Projektinhabers gesetzt | keine Bewertung vorhanden |
| Full Acceptance (Final) | software-architekt, Projektinhaber | 2026-10-01 | Endabnahme durch den Projektinhaber auf ausdrückliche Anweisung; kein Gesamtabgleich durch software-architekt | Security- und Compliance-Review offen |

## 5. Offene Punkte

- `get_sensor_info` und `get_version_info` sind nicht durch Tests abgedeckt.
- Mit Q.ANT-Hardware wurde nichts getestet (nur CPU-Backend).
- Struct-Layouts müssen bei Toolkit-Updates neu geprüft werden.
