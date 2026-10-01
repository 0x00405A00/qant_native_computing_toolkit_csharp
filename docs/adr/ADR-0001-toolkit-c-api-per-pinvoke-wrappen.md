# ADR-0001 – Toolkit-C-API per P/Invoke wrappen

**Status:** Full Acceptance (Final)
**Datum:** 2026-10-01
**Entscheider:** 
**Implementierungsplan:** [toolkit-c-api-per-pinvoke-wrappen.md](../features/toolkit-c-api-per-pinvoke-wrappen.md)
**Commit:** 2eceeae

## Kontext
*Nachträglich dokumentiert (2026-10-01): Die Entscheidung wurde beim Bau des Wrappers ohne eigenes ADR getroffen. Die erwogenen Alternativen sind aus dem Ergebnis rekonstruiert, nicht aus damaligen Unterlagen belegt.*

Das Q.ANT native computing toolkit ist eine Rust-Bibliothek, die eine C-API (`cdylib`, Header in `src/c_bridge`) exportiert. Offizielle Bindings gibt es nur für Python und C/C++. Für .NET soll eine abstrahierte C#-API entstehen.

## Entscheidung
Die C-API wird direkt per `[DllImport]` mit unsicheren Zeigern aufgerufen (`Native/NativeMethods.cs`). Es gibt keinen nativen Shim und keine Codegenerierung. Alle nativen Deklarationen sind `internal`. Der Wrapper zielt auf C-API 2.3 (`QantToolkit.SupportedToolkitVersion`).

## Konsequenzen
- Kein zusätzlicher nativer Build-Schritt; einzige Laufzeitabhängigkeit ist `libqant_native_computing_toolkit`.
- Struct-Layouts (`QantSensorInfo`, `QantVersionInfo`, DLPack) sind von Hand dupliziert und bei Toolkit-Updates neu zu prüfen.
- Das Bibliotheksprojekt benötigt `AllowUnsafeBlocks`.
- Toolkit-Einschränkungen (z. B. Dilation, Batchgröße) schlagen als Fehler der nativen Funktion durch (siehe ADR-0005).

## Alternativen
- C++/CLI oder eigener nativer Shim: zusätzlicher Build und Plattformabhängigkeit, kein Mehrwert gegenüber P/Invoke.
- Neuimplementierung gegen die Rust-Crate: nicht aus .NET nutzbar.
- Generierte Bindings (z. B. ClangSharp): bei ca. 25 Funktionen und kleinen Structs nicht gerechtfertigt.

## Links
- Upstream: https://github.com/Q-ANT-GmbH/qant_native_computing_toolkit (ADR 2 und 4, `src/c_bridge/*.h`)
