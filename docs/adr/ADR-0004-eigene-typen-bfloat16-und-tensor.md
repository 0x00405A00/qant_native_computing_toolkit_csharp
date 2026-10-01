# ADR-0004 – Eigene Typen BFloat16 und Tensor

**Status:** Implemented
**Datum:** 2026-10-01
**Entscheider:** 
**Implementierungsplan:** [eigene-typen-bfloat16-und-tensor.md](../features/eigene-typen-bfloat16-und-tensor.md)

## Kontext
*Nachträglich dokumentiert (2026-10-01): Die Entscheidung wurde beim Bau des Wrappers ohne eigenes ADR getroffen. Die erwogenen Alternativen sind aus dem Ergebnis rekonstruiert, nicht aus damaligen Unterlagen belegt.*

Alle Toolkit-Operationen arbeiten mit bfloat16. .NET 8 hat keinen eingebauten `BFloat16`-Typ. Die Tensoren des Toolkits sind dicht, zeilenweise (row-major) und ein- bis vierdimensional.

## Entscheidung
`BFloat16` ist ein `ushort`-Wrapper mit Umwandlung von/zu float32 (Runden zur nächsten gerade Zahl, NaN bleibt NaN). `Tensor` hält ein verwaltetes `BFloat16[]` plus Shape, row-major und bei der Konstruktion validiert. Komfortfabriken nehmen float32-Daten. Auf der verwalteten Seite gibt es keine Arithmetik; gerechnet wird auf dem Gerät.

## Konsequenzen
- Float32 nach bfloat16 ist verlustbehaftet; Tests vergleichen mit Toleranzen.
- Keine Abhängigkeit von Drittbibliotheken für Tensoren; Austausch mit ihnen läuft über `ToSingleArray()` (Kopie).
- Steht auf dem Zielframework ein eingebauter `BFloat16` zur Verfügung, kann der Typ ersetzt werden.

## Alternativen
- `System.Half` (float16): anderes Format, nicht kompatibel zu bfloat16.
- `float[]` in der API mit Umwandlung im Wrapper: versteckt den Genauigkeitsverlust.
- Drittanbieter-Tensorbibliothek: zusätzliche Abhängigkeit für wenige Funktionen.

## Links
- Siehe ADR-0002 (Marshalling)
