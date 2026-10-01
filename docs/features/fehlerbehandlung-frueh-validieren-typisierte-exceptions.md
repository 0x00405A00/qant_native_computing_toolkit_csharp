# Feature: Fehlerbehandlung: früh validieren, typisierte Exceptions (Implementierungsplan)

> **Status: umgesetzt, 2026-10-01.** Grundlage: [ADR-0005](../adr/ADR-0005-fehlerbehandlung-frueh-validieren-typisierte-exceptions.md) (**Accepted**).
> Dieser Plan zerlegt die Umsetzung in Meilensteine und Aufgaben. Er enthaelt **keinen Code**.

**Rollenkuerzel:** **SE** software-engineer · **T** software-tester · **SEC** it-security · **COMP** it-compliance · **A** software-architekt.

## 0. Umsetzungsstand (laufend fortzuschreiben)

| Aufgabe | Stand | Anmerkung |
| --- | --- | --- |
| 1.1 | erledigt | |
| 1.2 | erledigt | |
| 1.3 | erledigt | |

## 1. Ziel und Abgrenzung

Argumentfehler werden vor dem nativen Aufruf als `ArgumentException` gemeldet; native Fehler als `QantException` (mit `ErrorCode` bzw. Hinweis auf Logging). Entscheidung und Alternativen: siehe ADR-0005. Die Umsetzung entstand vor dem Plan; der Plan dokumentiert nachträglich, was gebaut wurde.

## 2. Meilensteine und Aufgaben

| Nr. | Rolle | Aufgabe | Ergebnis |
| --- | --- | --- | --- |
| 1.1 | SE | Prüfungen für Rang, gleiche Shapes, Kanalzahl, negative Größen in `QantDevice` | `QantDevice.cs` |
| 1.2 | SE | `QantException` mit `ErrorCode`; Rückgabewerte ungleich null und Null-Tensoren werden zu Exceptions | `Models.cs`, `TensorMarshaller.cs` |
| 1.3 | SE | Toolkit-Grenzen (Dilation, Batchgröße, output_padding) in XML-Kommentaren festhalten | `IQantDevice.cs` |

## 3. Tests und Abnahme

| Nr. | Rolle | Pruefung | Erwartung |
| --- | --- | --- | --- |
| T.1 | T | Falscher Rang und falsche Shapes lösen `ArgumentException` aus | `InvalidShapeFailsCleanly`, `Conv2dRejectsWrongRank`, `KanLayerRejectsMismatchedPhiAndAmplShapes` grün |
| T.2 | T | Nicht unterstützte Parameter lösen `QantException` aus | Tests zu Dilation, output_padding und Batch > 1 grün |

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

- Native Fehlerursachen stehen nicht in der Exception.
- Auf dem CPU-Backend meldet `setup_logging` einen Fehler; dafür gibt es keinen Test.
