# ADR-0006 — Datenmapping und erweiterter Konfigurations-/Variablenvertrag (LC02)

- **Status:** Vorgeschlagen (LC01; Umsetzung LC02)
- **Datum:** 2026-10-13
- **Entscheider:** Yovanny Rodríguez (Yova) / Product & Operations
- **Kontext:** [Low-Code-Anwendungsplattform-Plan](2026-10-13_Low-Code-Anwendungsplattform_Implementierungsplan.md), §5 LC01/LC02. Konfiguration und Datenfluss zwischen Konfiguration, Literalen und Variablenreferenzen sowie Schema- und FEEL-Umfang sind eindeutig festzulegen.

## Entscheidung

- **Eindeutige Konfiguration-Gegenüber-Literal-Gegenüber-Variablenreferenz:** Jede Binding-/Konfigurationsquelle ist entweder ein **Literal** (unverändert gesetzt), eine **Variablenreferenz** (`{{...}}`-Auflösung gegen Engine-Variablen) oder eine **Konfigurationsreferenz** (vertragsgebunden, vorzugsweise im Mapping hinterlegt); mehrdeutige/fehlerhafte Quellen werden beim Start **hart** abgelehnt (Fail-fast, kein stiller Fallback).
- **Vertrag Mensch–Maschine bleibt:** Engine- und Konfigurationspfade unterscheiden sich nicht durch Merkwürdigkeit der Eingabe; die Zuordnung erfolgt deterministisch über erkennbare Syntax, nicht über Prosa.
- **Schema-FEEL-Umfang:** kundige Schemakompatibilität; FEEL ist nur explizit erlaubt, wo der Vertrag es deklariert (kein verschleierter FEEL in schemaleichen Pfaden). Mindestumfang (arithmetische Grundoperationen, STRING/NUMBER/BOOLEAN, Vorlagen zusammenstellen) festgelegt; **bewusste Einschränkung** gegenüber breitem FEEL 1.1.
- **Editions-/Aufruforientierte Fehlerbehandlung:** Fehler sind editions-/aufruforientiert, Externer Fehler wird abgesetzt, Definition nicht still vermerkt; Demote-/Fallback-Pfade ohne Automationen.

## Konsequenzen

- Der bestehende `ioMapping`-Textvertrag (`name=value`) und `HttpConnectorExecutor` (liefert nur `httpStatus`, kein Body) werden in LC02 zu einem **versionierten Datenmapping** erweitert, das Literale/Variablen/Konfiguration explizit unterscheiden kann.
- Neuen Vertragsabnahme(.http) Katalog Requests aus LC02 folgen; BEUM-Erweiterung ist Teil der LC02-Verträge.
