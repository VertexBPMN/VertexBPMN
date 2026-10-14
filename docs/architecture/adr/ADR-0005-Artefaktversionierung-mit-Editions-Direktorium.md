# ADR-0005 — Artefaktversionierung mit Editions-Direktorium

- **Status:** Vorgeschlagen (LC01; Umsetzung ab LC02/LC04)
- **Datum:** 2026-10-13
- **Entscheider:** Yovanny Rodríguez (Yova) / Product & Operations
- **Kontext:** [Low-Code-Anwendungsplattform-Plan](2026-10-13_Low-Code-Anwendungsplattform_Implementierungsplan.md), §5 LC01 „Artefaktversionierung mit Editions-Direktorium“. Definitionen und Formulare werden persistent und revisionsfähig verwaltet.

## Entscheidung

- **Editions-/Revisions-Direktorium** als gemeinsames, versioniertes Artefaktverzeichnis für veröffentlichte Definitionen und Formulare; **Direkt-`Update`/`Overwrite` auf dann lebende Release-Objekte sind explizit verboten.** Veröffentlichung läuft als Edits/Revisions-Batch über Edit-Box/Runtime-Office mit App-/Formular-Downgrade-Logik.
- **Einheitliche Editions-IDs** (anwendungs-/datenübergreifende Artefaktversionierung) mit stabilem Zuordnungs-Vertrag; Werte wie „Leer“/0 reserviert und nicht als Output zulässig.
- **Veröffentlichte Artefakte sind unveränderlich** (immutable publish); aktueller Stand = Zeiger, kein Mappen­über­schreiben. Runtime-Office-Vertrag (Abruf, Veröffentlichung, Inkraftsetzung) wird für `Formulare` auf **nur synchrone** Abläufe (unabhängig vom Stand des Runtime-Office) fixiert.

## Konsequenzen

- Weitere Artefakttypen (Anwendungen, Releases, App-Releases, Installations-/Konfigurations-Subsysteme) erben diese Editionierung; jedes Artefakt besitzt Editions-ID + stabilen Zuordnungsvertrag.
- Der vorhandene `FormDefinitionRecord.Version`-Zähler bleibt, wird aber durch das Editions-Direktorium als unveränderliche Historie ergänzt; bestehende `Version`-Semantik wird nicht mit den Editions-IDs gleichgesetzt.
- Migrationen sind abwärtskompatibel; Bestand wird beim ersten Release-Vorgang dem Editionierungsvertrag zugeführt.
