# Feature: Gerät hinter IQantDevice abstrahieren (Implementierungsplan)

> **Status: umgesetzt, 2026-10-01.** Grundlage: [ADR-0003](../adr/ADR-0003-geraet-hinter-iqantdevice-abstrahieren.md) (**Accepted**).
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

Alle Operationen und Diagnosefunktionen stehen auf `IQantDevice`; `QantDevice` implementiert sie per P/Invoke; `QantToolkit` bietet Logging, Geräteerkennung und `OpenDevice`. Entscheidung und Alternativen: siehe ADR-0003. Die Umsetzung entstand vor dem Plan; der Plan dokumentiert nachträglich, was gebaut wurde.

## 2. Meilensteine und Aufgaben

| Nr. | Rolle | Aufgabe | Ergebnis |
| --- | --- | --- | --- |
| 1.1 | SE | Schnittstelle `IQantDevice` mit 16 Operationen und 5 Diagnosefunktionen, XML-Kommentare inkl. Toolkit-Grenzen | `IQantDevice.cs` |
| 1.2 | SE | `QantDevice`: Init im Konstruktor, `release_npu` in `Dispose`, Dimensionsprüfung vor dem nativen Aufruf | `QantDevice.cs` |
| 1.3 | SE | Statische Klasse `QantToolkit` (statt `Qant`, wegen Namespace-Kollision) | `QantToolkit.cs` |
| 1.4 | SE | Modelle: `ConvOptions`, `PoolOptions`, `SensorInfo`, `VersionInfo`, `PerformanceCounters`, `NpuDescriptor` | `Models.cs` |

## 3. Tests und Abnahme

| Nr. | Rolle | Pruefung | Erwartung |
| --- | --- | --- | --- |
| T.1 | T | Alle 16 Operationen sind über `IQantDevice` aufrufbar und durch ein Sample abgedeckt | `EveryDeviceOperationIsCoveredByASampleAndHasExamples` grün |
| T.2 | T | Konsumenten außerhalb des Namespace kompilieren ohne Alias | Konsolen-Sample und Blazor-Demo bauen |

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

- Es gibt keine Fake-/Mock-Implementierung von `IQantDevice`.
- Eine Schnittstellenerweiterung ist für Implementierer ein Breaking Change.
