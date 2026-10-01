# Feature: Eigene Typen BFloat16 und Tensor (Implementierungsplan)

> **Status: umgesetzt, 2026-10-01.** Grundlage: [ADR-0004](../adr/ADR-0004-eigene-typen-bfloat16-und-tensor.md) (**Accepted**).
> Dieser Plan zerlegt die Umsetzung in Meilensteine und Aufgaben. Er enthaelt **keinen Code**.

**Rollenkuerzel:** **SE** software-engineer · **T** software-tester · **SEC** it-security · **COMP** it-compliance · **A** software-architekt.

## 0. Umsetzungsstand (laufend fortzuschreiben)

| Aufgabe | Stand | Anmerkung |
| --- | --- | --- |
| 1.1 | erledigt | |
| 1.2 | erledigt | |

## 1. Ziel und Abgrenzung

Eigene Typen `BFloat16` (Umwandlung von/zu float32) und `Tensor` (verwaltetes `BFloat16[]` plus Shape, row-major). Entscheidung und Alternativen: siehe ADR-0004. Die Umsetzung entstand vor dem Plan; der Plan dokumentiert nachträglich, was gebaut wurde.

## 2. Meilensteine und Aufgaben

| Nr. | Rolle | Aufgabe | Ergebnis |
| --- | --- | --- | --- |
| 1.1 | SE | `BFloat16` als `ushort`-Wrapper, Runden zur nächsten geraden Zahl, NaN/Infinity erhalten | `BFloat16.cs` |
| 1.2 | SE | `Tensor` mit Validierung von Shape und Datenlänge, Fabriken `FromArray` (1D, 2D, flach mit Shape), `Zeros`, Indexer, `ToSingleArray` | `Tensor.cs` |

## 3. Tests und Abnahme

| Nr. | Rolle | Pruefung | Erwartung |
| --- | --- | --- | --- |
| T.1 | T | BFloat16: exakte Werte, Runden zur geraden Zahl, NaN/Infinity | `BFloat16Tests` grün |
| T.2 | T | Tensor: row-major, Shape-Fehler, Indexfehler | `TensorTests` grün |

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

- Keine Arithmetik auf der verwalteten Seite (bewusst).
- Ersatz durch einen eingebauten `BFloat16`, sobald das Zielframework ihn bietet.
