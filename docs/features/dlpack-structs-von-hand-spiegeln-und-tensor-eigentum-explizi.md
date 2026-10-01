# Feature: DLPack-Structs von Hand spiegeln und Tensor-Eigentum explizit regeln (Implementierungsplan)

> **Status: umgesetzt, 2026-10-01.** Grundlage: [ADR-0002](../adr/ADR-0002-dlpack-structs-von-hand-spiegeln-und-tensor-eigentum-explizit-regeln.md) (**Accepted**).
> Dieser Plan zerlegt die Umsetzung in Meilensteine und Aufgaben. Er enthaelt **keinen Code**.

**Rollenkuerzel:** **SE** software-engineer · **T** software-tester · **SEC** it-security · **COMP** it-compliance · **A** software-architekt.

## 0. Umsetzungsstand (laufend fortzuschreiben)

| Aufgabe | Stand | Anmerkung |
| --- | --- | --- |
| 1.1 | erledigt | |
| 1.2 | erledigt | |
| 1.3 | erledigt | |

## 1. Ziel und Abgrenzung

Tensoren werden als `DLManagedTensorVersioned` (DLPack 1.1) übergeben; Eingaben gepinnt ohne Kopie, Ausgaben kopiert und über den Deleter freigegeben. Nicht-Ziele: GPU-Geräte, andere Dtypes als bfloat16. Entscheidung und Alternativen: siehe ADR-0002. Die Umsetzung entstand vor dem Plan; der Plan dokumentiert nachträglich, was gebaut wurde.

## 2. Meilensteine und Aufgaben

| Nr. | Rolle | Aufgabe | Ergebnis |
| --- | --- | --- | --- |
| 1.1 | SE | DLPack-Structs von Hand spiegeln (Layout gegen `dlpack.h` v1.1 geprüft) | `Native/DLPack.cs` |
| 1.2 | SE | `PinnedTensor`: `GCHandle` pinnen, Header und Shape in einem unverwalteten Block, Deleter null | `Native/TensorMarshaller.cs` |
| 1.3 | SE | `TensorMarshaller.Consume`: Dtype und Strides prüfen, kopieren, Deleter im `finally` | `Native/TensorMarshaller.cs` |

## 3. Tests und Abnahme

| Nr. | Rolle | Pruefung | Erwartung |
| --- | --- | --- | --- |
| T.1 | T | Round-Trip Eingabe → Gerät → Ausgabe liefert korrekte Werte (Multiply, Linear, Conv u. a.) | Integrationstests grün |
| T.2 | T | Mehrdimensionale Shapes (1D–4D) bleiben erhalten | Shape-Asserts in Conv-/Pooling-Tests grün |

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

- Keine Tests für abgelehnte Dtypes oder strided Ausgaben (nicht erzeugbar mit dem CPU-Backend).
- Kein Speicherleck-Test (der C-Test `memory_tests` des Toolkits hat kein Gegenstück).
- Bei DLPack-Updates Layout erneut prüfen.
