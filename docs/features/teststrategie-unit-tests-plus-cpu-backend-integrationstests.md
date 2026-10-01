# Feature: Teststrategie: Unit-Tests plus CPU-Backend-Integrationstests (Implementierungsplan)

> **Status: umgesetzt, 2026-10-01.** Grundlage: [ADR-0007](../adr/ADR-0007-teststrategie-unit-tests-plus-cpu-backend-integrationstests.md) (**Accepted**).
> Dieser Plan zerlegt die Umsetzung in Meilensteine und Aufgaben. Er enthaelt **keinen Code**.

**Rollenkuerzel:** **SE** software-engineer · **T** software-tester · **SEC** it-security · **COMP** it-compliance · **A** software-architekt.

## 0. Umsetzungsstand (laufend fortzuschreiben)

| Aufgabe | Stand | Anmerkung |
| --- | --- | --- |
| 1.1 | erledigt | |
| 1.2 | erledigt | |
| 1.3 | erledigt | |
| 1.4 | erledigt | |
| 1.5 | erledigt | |

## 1. Ziel und Abgrenzung

Rein verwaltete Tests laufen immer; Integrationstests gegen die CPU-Backend-Bibliothek sind überspringbar; Rechenoperationen werden gegen verwaltete Referenzen verglichen. Entscheidung und Alternativen: siehe ADR-0007. Die Umsetzung entstand vor dem Plan; der Plan dokumentiert nachträglich, was gebaut wurde.

## 2. Meilensteine und Aufgaben

| Nr. | Rolle | Aufgabe | Ergebnis |
| --- | --- | --- | --- |
| 1.1 | SE | xUnit-Testprojekt mit `Xunit.SkippableFact` | `tests/Qant.NativeComputing.Tests` |
| 1.2 | SE | Integrationstests (Multiply, Linear, ReLU, AddBias, MaxPool, Diagnose, Fehlerfälle) | `IntegrationTests.cs` |
| 1.3 | SE | Referenztests für Conv2d, ConvTranspose2d, BatchNorm2d, KanLayer mit Toleranzen | `OperationReferenceTests.cs` |
| 1.4 | SE | Demo-Sample- und Hinweis-Tests (Beispieldefinitionen eingebunden) | `DemoSampleTests.cs` |
| 1.5 | SE | Skript `run-integration-tests.sh`; Bibliothek in `native/` (nicht im Repository) | `run-integration-tests.sh`, `.gitignore` |

## 3. Tests und Abnahme

| Nr. | Rolle | Pruefung | Erwartung |
| --- | --- | --- | --- |
| T.1 | T | Alle Tests mit Bibliothek | 42 Tests grün |
| T.2 | T | Ohne Bibliothek bleibt `dotnet test` grün | ohne Variable laufen nur die verwalteten Tests, der Rest wird übersprungen |

Nach erfolgreicher Umsetzung: ADR auf `Implemented`. Nach Tests und Abnahme: ADR auf `Implementation Tested and Acceptance`.

## 4. Prüfnachweise

Je Status bewertet eine Rolle (siehe Skill `adr-workflow`, Schritt 6). Ergebnis und Befunde hier eintragen.

| Status | Rolle | Datum | Ergebnis | Befunde |
| --- | --- | --- | --- | --- |
| Implemented | software-engineer | 2026-10-01 | Build ohne Warnungen und Fehler; 42 Tests grün (CPU-Backend) | siehe offene Punkte |
| Implemented | code-review | | nicht durchgeführt | |
| Implementation Tested and Acceptance | software-tester | | | |
| Security Review | it-security | | | |
| GDPR/Compliance Review | it-compliance | | | |
| Full Acceptance (Final) | software-architekt, Projektinhaber | | | |

## 5. Offene Punkte

- Nicht abgedeckt: Sensorinfo und Versionsinfo.
- Die Tests dokumentieren Toolkit-Grenzen und schlagen bei Toolkit-Updates an, wenn diese entfallen.
- Auf der Hardware wurde nichts getestet.
- Die lokale `native/*.so` muss bei Toolkit-Updates neu gebaut werden.
