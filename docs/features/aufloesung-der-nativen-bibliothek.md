# Feature: Auflösung der nativen Bibliothek (Implementierungsplan)

> **Status: umgesetzt, 2026-10-01.** Grundlage: [ADR-0006](../adr/ADR-0006-aufloesung-der-nativen-bibliothek.md) (**Accepted**).
> Dieser Plan zerlegt die Umsetzung in Meilensteine und Aufgaben. Er enthaelt **keinen Code**.

**Rollenkuerzel:** **SE** software-engineer · **T** software-tester · **SEC** it-security · **COMP** it-compliance · **A** software-architekt.

## 0. Umsetzungsstand (laufend fortzuschreiben)

| Aufgabe | Stand | Anmerkung |
| --- | --- | --- |
| 1.1 | erledigt | |
| 1.2 | erledigt | |

## 1. Ziel und Abgrenzung

Die Shared Library wird über `QANT_NATIVE_LIB_PATH` oder die Standardsuche gefunden. Entscheidung und Alternativen: siehe ADR-0006. Die Umsetzung entstand vor dem Plan; der Plan dokumentiert nachträglich, was gebaut wurde.

## 2. Meilensteine und Aufgaben

| Nr. | Rolle | Aufgabe | Ergebnis |
| --- | --- | --- | --- |
| 1.1 | SE | `NativeLibrary.SetDllImportResolver` im statischen Konstruktor von `NativeMethods`, ausgelöst über `EnsureLoaded()` | `Native/NativeMethods.cs` |
| 1.2 | SE | Pfad der CPU-Bibliothek in Skripten und Debug-Profilen setzen; Blazor-Demo zusätzlich über `Qant:LibraryPath` | `run-integration-tests.sh`, `.vscode/launch.json`, `QantService` |

## 3. Tests und Abnahme

| Nr. | Rolle | Pruefung | Erwartung |
| --- | --- | --- | --- |
| T.1 | T | Alle Integrationstests laden die Bibliothek über `QANT_NATIVE_LIB_PATH` | Tests grün |
| T.2 | T | Ohne Variable werden Integrationstests übersprungen | `dotnet test` ohne Variable: 11 grün, 31 übersprungen |

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

- Der Fall „Bibliothek fehlt“ (`DllNotFoundException`) ist nicht getestet; die Blazor-Demo zeigt ihn im Statusbereich an.
- Standardsuche ohne Variable ist nicht getestet.
