namespace Qant.BlazorDemo.Samples;

/// <summary>Practical guidance for one operation.</summary>
/// <param name="SuitableFor">What the operation is good for.</param>
/// <param name="Examples">Typical real-world uses.</param>
/// <param name="Limits">Constraints of the wrapper / toolkit 2.3, if any.</param>
public sealed record OperationHint(string Operation, string SuitableFor, string[] Examples, string? Limits = null);

/// <summary>
/// Hints describe typical roles of each operation in machine-learning and signal-processing code.
/// They are not performance claims about a particular device.
/// </summary>
public static class OperationHints
{
    public static IReadOnlyDictionary<string, OperationHint> All { get; } = new OperationHint[]
    {
        new("Multiply",
            "Elementweises Produkt zweier gleich langer Vektoren: Skalieren, Gewichten, Maskieren und Gating.",
            ["Sensorwerte mit Kalibrierfaktoren multiplizieren", "Audio-Samples mit einer Lautstärke-Hüllkurve versehen", "Ein Gate (Werte 0..1) auf Feature-Werte anwenden"],
            "Nur 1D, beide Tensoren gleich lang."),
        new("ScaledPeriodicNonlinearity",
            "Berechnet w = tcos(u)·v je Wertepaar: eine periodische Nichtlinearität mit Amplitude. Baustein für periodische Funktionen und für die KAN-Schicht.",
            ["Periodische Merkmale (Tageszeit, Saison, Winkel) mit gelernten Amplituden gewichten", "Phasenmodulation eines Signals", "Bausteine für selbst gebaute Fourier-artige Netze"],
            "Nur 1D. tcos ist auf der Hardware nur kosinusähnlich, im CPU-Backend ein echter Kosinus."),
        new("Linear",
            "Vollständig verbundene (Dense-)Schicht: y = x·Wᵀ. Mischt alle Eingangsmerkmale zu neuen Ausgangsmerkmalen.",
            ["Klassifikator-Kopf: 512 Bildmerkmale → 10 Ziffernklassen", "Projektion eines Embeddings von 768 auf 128 Dimensionen", "Eine Schicht eines einfachen MLP für tabellarische Daten (z. B. Preisvorhersage)"],
            "Gewichte haben die Form (Ausgang, Eingang) und werden implizit transponiert. Der Bias wird separat mit AddBias addiert."),
        new("AddBias",
            "Addiert pro Kanal einen konstanten Wert auf 2D-Features (Batch × Kanäle). Üblicher Folgeschritt nach Linear.",
            ["Nach einer Linear-Schicht den gelernten Bias addieren", "Einen festen Offset je Merkmal ausgleichen (z. B. Mittelwertverschiebung)"],
            "Features 2D, Bias 1D mit Länge = Kanalzahl."),
        new("AddBiasConv2d",
            "Addiert pro Kanal einen Wert auf Conv-Feature-Maps (Batch × Kanäle × Höhe × Breite). Üblicher Folgeschritt nach Conv2d.",
            ["Nach der Faltung eines RGB-Bildes den Bias je Ausgangskanal addieren", "Helligkeits-Offset je Farbkanal korrigieren"],
            "Features 4D, Bias 1D mit Länge = Kanalzahl."),
        new("Relu",
            "Standard-Aktivierung: negative Werte werden zu 0, positive bleiben. Einfach und weit verbreitet in tiefen Netzen.",
            ["Nach jeder Conv- oder Linear-Schicht in einem Bildklassifikator", "Negative Messwerte abschneiden (z. B. Leistung kann nicht negativ sein)"],
            "Der Wrapper erwartet 1D-Eingaben."),
        new("Sigmoid",
            "Quetscht Werte auf den Bereich 0..1. Geeignet für Wahrscheinlichkeiten unabhängiger Ja/Nein-Entscheidungen und für Gates.",
            ["Spam ja/nein: Wahrscheinlichkeit für eine einzelne Klasse", "Multi-Label-Erkennung (ein Bild enthält Hund und Ball gleichzeitig)", "Gate-Werte, die andere Werte per Multiply abschwächen"],
            "Der Wrapper erwartet 1D-Eingaben."),
        new("Softmax",
            "Wandelt Rohwerte (Logits) in eine Wahrscheinlichkeitsverteilung um, die sich zu 1 summiert. Für Entscheidungen zwischen sich ausschließenden Klassen.",
            ["Ziffernerkennung: Wahrscheinlichkeit für 0..9 aus den 10 Ausgaben des Klassifikators", "Textklassifikation: genau eine Kategorie je Dokument", "Auswahl einer Aktion aus mehreren Optionen"],
            "Form (1, N), keine Batches."),
        new("Conv2d",
            "Faltung: ein kleiner Kernel gleitet über das Bild und erkennt lokale Muster wie Kanten, Ecken oder Texturen, unabhängig von deren Position.",
            ["Kantenerkennung (Sobel-Filter) in Fotos", "Merkmalsextraktion in einem Bildklassifikator", "Defekterkennung auf Kamerabildern in der Fertigung (Kratzer, Risse)", "Mustersuche in 2D-Sensordaten wie Spektrogrammen"],
            "Dilation muss 1 sein (Toolkit 2.3)."),
        new("ConvTranspose2d",
            "Transponierte Faltung: vergrößert Feature-Maps (Upsampling) und lernt dabei die Interpolation. Gegenstück zur Faltung mit Stride.",
            ["Decoder eines Autoencoders (komprimiertes Bild wieder aufbauen)", "Segmentierung: grobe Feature-Map zurück auf Bildgröße bringen", "Bildgenerierung aus einem kleinen Zufallsvektor"],
            "Nur Batchgröße 1, Dilation 1, output_padding 0 (Toolkit 2.3)."),
        new("BatchNorm2d",
            "Normalisiert jeden Kanal mit gegebenem Mittelwert und Varianz und skaliert mit γ und β. Stabilisiert tiefe Netze, hier als Inferenz-Schritt mit fertig gelernten Statistiken.",
            ["Direkt nach einer Conv-Schicht in ResNet-artigen Netzen", "Importierte, bereits trainierte Modelle ausführen (Statistiken stammen aus dem Training)"],
            "Nur Inferenz; Batchgröße 1 (Toolkit 2.3)."),
        new("MaxPool2d",
            "Verkleinert Feature-Maps, indem je Fenster der größte Wert bleibt. Behält die stärkste Aktivierung und macht robust gegen kleine Verschiebungen.",
            ["Feature-Maps zwischen Conv-Blöcken halbieren (2×2, Stride 2)", "Prüfen, ob ein Muster irgendwo im Bildbereich vorkommt"]),
        new("AvgPool2d",
            "Verkleinert Feature-Maps durch den Mittelwert je Fenster. Glättet und unterdrückt Rauschen.",
            ["Rauschen in einer Wärmebildkamera-Karte mitteln und verkleinern", "Weicheres Downsampling als MaxPool in Bildmodellen"],
            "Der Parameter countIncludePad steuert, ob Padding-Nullen im Mittelwert zählen."),
        new("AdaptiveMaxPool2d",
            "Pooling mit fest vorgegebener Ausgabegröße, egal wie groß die Eingabe ist. Der Maximalwert je Bereich bleibt.",
            ["Beliebig große Bilder auf 2×2 reduzieren, damit die folgende Linear-Schicht immer dieselbe Größe sieht"],
            "Eingabegröße muss ein ganzzahliges Vielfaches der Ausgabegröße sein, symmetrisch (Toolkit 2.3)."),
        new("AdaptiveAvgPool2d",
            "Mittelwert-Pooling mit fester Ausgabegröße. Mit 1×1 entsteht Global Average Pooling.",
            ["Global Average Pooling vor dem Klassifikator (z. B. 7×7×512 → 512)", "Bilder unterschiedlicher Auflösung auf ein einheitliches Format bringen"],
            "Eingabegröße muss ein ganzzahliges Vielfaches der Ausgabegröße sein, symmetrisch (Toolkit 2.3)."),
        new("KanLayer",
            "Kolmogorov-Arnold-Schicht: statt fester Aktivierung lernt jede Verbindung eine eigene periodische Funktion (Summe aus tcos-Termen mit Phase und Amplitude).",
            ["Funktionsapproximation und Regression kleiner physikalischer Zusammenhänge (z. B. Pendelbewegung)", "Zeitreihen mit Periodizität: Energieverbrauch mit Tages- und Wochenrhythmus", "Interpretierbare Modelle: Phasen und Amplituden je Verbindung lassen sich einzeln ansehen"],
            "Der Vergleich mit einem echten Kosinus gilt nur im CPU-Backend; auf der Hardware ist tcos kosinusähnlich."),
    }.ToDictionary(h => h.Operation);
}
