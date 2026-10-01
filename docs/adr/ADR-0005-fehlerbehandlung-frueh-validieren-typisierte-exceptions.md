# ADR-0005 – Fehlerbehandlung: früh validieren, typisierte Exceptions

**Status:** Full Acceptance (Final)
**Datum:** 2026-10-01
**Entscheider:** 
**Implementierungsplan:** [fehlerbehandlung-frueh-validieren-typisierte-exceptions.md](../features/fehlerbehandlung-frueh-validieren-typisierte-exceptions.md)
**Commit:** 2eceeae

## Kontext
*Nachträglich dokumentiert (2026-10-01): Die Entscheidung wurde beim Bau des Wrappers ohne eigenes ADR getroffen. Die erwogenen Alternativen sind aus dem Ergebnis rekonstruiert, nicht aus damaligen Unterlagen belegt.*

Die C-API meldet Fehler über einen Rückgabewert ungleich null (Info- und Generic-Funktionen) oder einen Null-Zeiger (Rechenfunktionen). Es gibt keine Funktion für die letzte Fehlermeldung; Details gehen nur ins Toolkit-Log. Das Toolkit 2.3 hat zudem Einschränkungen: Dilation muss 1 sein (Conv, ConvTranspose), `output_padding` bei ConvTranspose muss 0 sein, und ConvTranspose sowie BatchNorm2d akzeptieren nur Batchgröße 1.

## Entscheidung
- Im verwalteten Code erkennbare Argumentfehler (Rang, gleiche Shapes, Kanalzahlen, negative Größen) lösen vor dem nativen Aufruf eine `ArgumentException` aus.
- Rückgabewerte ungleich null lösen `QantException` mit `ErrorCode` aus; Null-Tensoren lösen `QantException` mit Hinweis auf `QantToolkit.SetupLogging` aus.
- Weitere Einschränkungen (Dilation, Batchgröße, Teilbarkeit bei Adaptive Pooling) prüft der Wrapper nicht, sie schlagen als `QantException` durch.

## Konsequenzen
- Die nativen Fehlerursachen stehen nicht in der Exception; Nutzer müssen Logging aktivieren. Auf dem CPU-Backend meldet `setup_logging` selbst einen Fehler („logging not available for cpu-backend").
- Die verwalteten Vorabprüfungen duplizieren einen Teil der Toolkit-Validierung und können davon abweichen.
- Die Einschränkungen von Toolkit 2.3 sind in den XML-Kommentaren von `IQantDevice` und in Tests dokumentiert.

## Alternativen
- Nur native Fehler durchreichen: weniger Code, aber schlechte Fehlermeldungen bei offensichtlichen Fehlbedienungen.
- Toolkit-Einschränkungen im Wrapper nachprüfen: würde bei Toolkit-Updates veralten und stillschweigend zu streng werden.
- Result-Typen statt Exceptions: unüblich für .NET-APIs dieser Art.

## Links
- Siehe ADR-0002, ADR-0007
