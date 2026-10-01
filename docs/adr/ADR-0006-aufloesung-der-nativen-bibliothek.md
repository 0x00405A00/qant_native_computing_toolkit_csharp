# ADR-0006 – Auflösung der nativen Bibliothek

**Status:** Implemented
**Datum:** 2026-10-01
**Entscheider:** 
**Implementierungsplan:** [aufloesung-der-nativen-bibliothek.md](../features/aufloesung-der-nativen-bibliothek.md)

## Kontext
*Nachträglich dokumentiert (2026-10-01): Die Entscheidung wurde beim Bau des Wrappers ohne eigenes ADR getroffen. Die erwogenen Alternativen sind aus dem Ergebnis rekonstruiert, nicht aus damaligen Unterlagen belegt.*

Die Shared Library wird getrennt ausgeliefert (Release-`.deb` für die Hardware-Variante, `cpu-backend`-Build für den Betrieb ohne Hardware) und liegt nicht in einem NuGet-Paket. Der Ablageort unterscheidet sich je Rechner.

## Entscheidung
Es wird der logische Name `qant_native_computing_toolkit` verwendet und ein `NativeLibrary.SetDllImportResolver` registriert. Ist `QANT_NATIVE_LIB_PATH` gesetzt, wird genau diese Datei geladen, sonst gilt die Standardsuche von .NET. Der Resolver wird im statischen Konstruktor von `NativeMethods` installiert und über `EnsureLoaded()` in den öffentlichen Einstiegspunkten ausgelöst.

## Konsequenzen
- Hardware- oder CPU-Variante wird allein durch die bereitgestellte Datei gewählt; der C#-Code ist identisch.
- Die Bibliothek ist nicht Teil des Pakets; fehlt sie, kommt beim ersten Aufruf eine `DllNotFoundException`.
- Ein Resolver kann je Assembly nur einmal gesetzt werden.

## Alternativen
- Native Bibliothek im NuGet-Paket bündeln: bräuchte Binaries je Plattform und das Lizenz-/Treiberthema (der Treiber ist proprietär, siehe Upstream ADR 2).
- Fester Pfad oder `LD_LIBRARY_PATH` allein: weniger flexibel für Tests und Debugging.

## Links
- Siehe ADR-0001, ADR-0007
- Upstream ADR 2 (Treiber als dynamische C-Bibliothek)
