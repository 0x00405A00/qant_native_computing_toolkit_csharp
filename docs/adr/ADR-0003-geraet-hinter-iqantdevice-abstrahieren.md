# ADR-0003 – Gerät hinter IQantDevice abstrahieren

**Status:** Implemented
**Datum:** 2026-10-01
**Entscheider:** 
**Implementierungsplan:** [geraet-hinter-iqantdevice-abstrahieren.md](../features/geraet-hinter-iqantdevice-abstrahieren.md)

## Kontext
*Nachträglich dokumentiert (2026-10-01): Die Entscheidung wurde beim Bau des Wrappers ohne eigenes ADR getroffen. Die erwogenen Alternativen sind aus dem Ergebnis rekonstruiert, nicht aus damaligen Unterlagen belegt.*

Gewünscht war eine „abstrahierte" C#-API. Nutzer sollen nicht von P/Invoke-Typen abhängen und ihren Code ohne Hardware und ohne native Bibliothek testen können.

## Entscheidung
Alle Operationen und Diagnosefunktionen stehen auf der Schnittstelle `IQantDevice`; `QantDevice` ist die einzige P/Invoke-Implementierung. Die statische Klasse `QantToolkit` bietet Logging, Geräteerkennung und `OpenDevice`. C-Funktionen mit `npu_id` werden zu Instanzmethoden, die Id ist Eigenschaft des Geräts. Namen folgen .NET-Konventionen (`Linear`, `Conv2d`, `MaxPool2d`), nicht den C-Namen `*_fprop`.

Die statische Klasse heißt `QantToolkit` und nicht `Qant`, weil `Qant` mit dem Namespace `Qant.NativeComputing` kollidiert und jeden Nutzer außerhalb des Namespace zu einem Alias gezwungen hätte.

## Konsequenzen
- Fakes und Mocks von `IQantDevice` sind einfach; eine verwaltete Referenzimplementierung wird nicht mitgeliefert.
- Die Schnittstelle trägt die gesamte Toolkit-Oberfläche; Erweiterungen sind für Implementierer ein Breaking Change.
- `Dispose` ruft `release_npu` auf, `init_npu` läuft im Konstruktor.

## Alternativen
- Nur eine konkrete Klasse ohne Interface: weniger Code, aber nicht mockbar.
- Verwaltete CPU-Referenzimplementierung hinter dem Interface: wäre eine Zweitimplementierung des Toolkits; das CPU-Backend des Toolkits deckt den Zweck ab (siehe ADR-0007).
- Statische Funktionen mit `npu_id` wie in C: nah am Original, aber unhandlich und nicht abstrahiert.

## Links
- Siehe ADR-0001 (P/Invoke), ADR-0007 (Tests)
