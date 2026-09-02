# Konzept: Docs in `docs/` vereinfachen und neu ordnen

## Ausgangslage

In `docs/` liegen derzeit drei Arten von Inhalten nebeneinander:

- Nutzer- und Entwicklerdokumentation
- Architektur- und Umsetzungsplaene
- historische Statusberichte, alte Entwurfsdokumente und schon ueberholte Zwischenstaende

Dadurch entstehen Doppelungen, alte Einstiegspunkte und unklare Quellen der Wahrheit. Besonders sichtbar ist das bei Quickstarts, Roadmaps, Studio-Planaenderungen und langen Statusdokumenten, die inzwischen nur noch den alten Stand dokumentieren.

## Ziel

Die Dokumentation soll fuer neue und bestehende Nutzer schneller auffindbar, kuerzer und verlaesslicher werden.

Konkret heisst das:

- ein klarer Einstiegspunkt
- genau eine kanonische Seite pro Thema
- Live-Dokumentation getrennt von Arbeits- und Archivmaterial
- keine mehrfach gepflegten Inhalte mit leicht unterschiedlichen Aussagen
- alte Dokumente nicht stillschweigend loeschen, sondern sauber archivieren

## Vorschlag fuer die neue Struktur

```text
docs/
  README.md
  getting-started/
  guide/
  reference/
  architecture/
  runbooks/
  archive/
  assets/
  working/
```

### 1. `docs/README.md`

Der neue Einstiegspunkt fuer Menschen und Tools.

Enthaelt:

- kurze Projektbeschreibung
- die 5 bis 8 wichtigsten Links
- klare Trennung zwischen:
  - Start hier
  - API und SDK
  - Studio
  - Betrieb
  - Architektur
  - Archiv

### 2. `docs/getting-started/`

Nur fuer den ersten Einstieg und schnelle lokale Erfolge.

Hierhin gehoeren:

- Setup
- lokales Starten
- erster Deploy und erster Prozessstart
- kurze FAQ fuer typische Setup-Probleme

Ziel: Eine Seite, ein roter Faden, keine historischen Exkurse.

### 3. `docs/reference/`

Technische Referenzdokumentation, die sich an feste Vertrage bindet.

Hierhin gehoeren:

- OpenAPI
- SDK-Doku
- CLI-Referenz
- API-Mappings
- Contract- und Schema-Beschreibungen

### 4. `docs/guide/`

Praxisnahe Anleitungen fuer typische Nutzerziele.

Hierhin gehoeren:

- BPMN ausfuehren
- Tasks und Variablen
- Webhooks
- Monitoring
- Debugging aus Anwendersicht
- Studio-Arbeitsablaeufe

### 5. `docs/architecture/`

Hier liegen die Entscheidungs- und Konzeptdokumente, die erklaeren, warum das System so gebaut ist.

Hierhin gehoeren:

- Parser- und Modellentscheidungen
- Architekturkonzepte
- Paritaets- und Integrationsplaene
- technische Zielbilder
- groessere Designentscheidungen

### 6. `docs/runbooks/`

Operationales Wissen fuer Betrieb und Support.

Hierhin gehoeren:

- Deployment
- Monitoring
- Troubleshooting
- Production Notes
- Security- und Hardening-Anleitungen

### 7. `docs/working/`

Temporaere Planungs- und Umsetzungsdokumente, die noch aktiv bearbeitet werden.

Hierhin gehoeren:

- Roadmaps
- Implementierungsplaene
- Migrationsplaene
- Feature-Matrizen in Arbeit
- Zwischenstaende mit offenem Scope

Regel: Sobald ein Thema umgesetzt ist, wandert das Dokument entweder in `archive/` oder wird in eine kanonische Referenzseite ueberfuehrt.

### 8. `docs/archive/`

Alles, was historisch wichtig, aber fachlich ueberholt ist.

Typische Kandidaten:

- alte Implementierungsplaene
- erledigte Statusberichte
- veraltete Zwischenstaende
- ueberholte Quickstarts
- Dokumente mit historischem Wert, aber ohne aktiven Pflegebedarf

### 9. `docs/assets/`

Nur fuer Medien und statische Begleitdateien.

Hierhin gehoeren:

- Bilder
- PDFs
- Diagramme

Die Dateien bleiben unveraendert, aber werden in der Doku sauber referenziert.

## Konkrete Aufraeumregeln

1. Ein Thema hat genau eine kanonische Seite.
2. Wenn ein Thema umgesetzt ist, wird der alte Plan nicht weiter als Hauptdokument gepflegt.
3. Statusdokumente bekommen ein klares Label:
   - `Status: active`
   - `Status: draft`
   - `Status: archived`
4. Jede Dokumentation bekommt ein kurzes `Zuletzt aktualisiert`.
5. Alte Quickstarts werden durch eine einzige neue Einstiegslinie ersetzt.
6. Vergleichs- und Feature-Matrizen werden nur dort gepflegt, wo sie wirklich noch Entscheidungswert haben.

## Was aus dem aktuellen Bestand besonders zusammengefuehrt werden sollte

### Einstieg und Quickstarts

- `docs/README.md`
- `docs/getting-started/README.md`
- `docs/getting-started/api-quickstart.md`
- `docs/getting-started/csharp-quickstart.md`
- `docs/reference/sdk-dotnet.md`

Empfehlung:

- einen kanonischen Einstieg unter `docs/getting-started/`
- den SDK-Start unter `docs/reference/` oder `docs/guide/`
- veraltete Quickstarts archivieren oder auf die neue Seite umleiten

### API- und Referenzdokumentation

- `docs/reference/openapi.md`
- `docs/reference/api-mapping-camunda.md`
- `docs/reference/api-parity-camunda.md`
- `docs/reference/mcp-server.md`
- `docs/reference/predictive-analytics-api.md`
- `docs/reference/debugger-trace-api.md`

Empfehlung:

- Referenzdokumente nach Themen clustern
- Mapping- und Paritaetsseiten nur behalten, wenn sie aktiv gepflegt werden

### Studio, Paritaet und Feature-Planung

- `docs/working/studio-api-parity-plan.md`
- `docs/working/roadmap.md`
- `docs/archive/phase4-innovation-status.md`
- `docs/working/Unified-Gap-Matrix.md`
- `docs/working/Unified-Parser-Migration-Guide.md`
- `docs/working/ROUNDTRIP_STRICT_PLAN.md`

Empfehlung:

- aktiven Plan in `docs/working/`
- umgesetzte Inhalte in `docs/archive/`
- echte Architekturentscheidungen in `docs/architecture/`

### Status-, Test- und Implementierungsberichte

- `docs/AI-Handler-Tests-Summary.md`
- `docs/AI-Handler-Tests-Fixed-Summary.md`
- `docs/HttpClient-Mocking-Implementation-Complete.md`
- `docs/archive/test-coverage.md`
- `docs/runbooks/production-notes.md`

Empfehlung:

- nur die fuer den Betrieb oder die Nachvollziehbarkeit wirklich relevanten Berichte behalten
- rein historische Fortschrittsberichte archivieren

### Bilder, PDFs und Prompts

- `docs/BPMN_by_example.pdf`
- `docs/Business Process Model and Notation BPMN.pdf`
- `docs/archive/prompts/*`
- `docs/HowToBuildAgenticAISystem.jpg`

Empfehlung:

- Medien nach `docs/assets/`
- Prompt-Sammlungen nur dann im Haupt-Docs-Bereich lassen, wenn sie aktive Arbeitsmaterialien sind

## Migrationsplan in drei Schritten

### Phase 1: Inventur

- alle Dateien in `docs/` klassifizieren
- pro Datei den Status festhalten:
  - active
  - draft
  - archived
- Duplikate und veraltete Eintraege markieren

### Phase 2: Konsolidierung

- einen neuen Einstiegspunkt anlegen
- Quickstarts zusammenziehen
- Paritaets- und Planungsdokumente auf einen aktiven Bestand reduzieren
- reine Zwischenberichte archivieren

### Phase 3: Aufraeumen

- alte Seiten mit Verweis auf die neue kanonische Seite versehen
- ueberholte Dokumente verschieben
- Link- und Navigationsstruktur glatttziehen

## Definition von fertig

Die Dokumentation ist aus Sicht der Organisation ausreichend aufgeraeumt, wenn:

- neue Nutzer sofort den richtigen Einstieg finden
- pro Kernthema nur eine aktive Seite existiert
- alte Inhalte entweder archiviert oder auf eine neue Seite umgeleitet sind
- `docs/README.md` die Navigation sichtbar fuehrt
- die verbleibenden offenen Dokumente eindeutig als Arbeit oder Archiv erkennbar sind

## Empfehlung fuer den naechsten praktischen Schritt

1. Erst eine Inventarliste aller Dateien in `docs/` erstellen.
2. Dann die 10 bis 15 groessten Doppelungen zusammenfuehren.
3. Anschliessend `docs/README.md` als sauberen Einstieg bauen.
4. Zum Schluss die Archivstruktur anlegen und alte Dokumente dorthin verschieben.
