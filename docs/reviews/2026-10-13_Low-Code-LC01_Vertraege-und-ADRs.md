# LC01 — Verträge und Architekturentscheidungen (Fortschritt)

Stand: 2026-10-13. Paket **LC01 (M)** gemäß Low-Code-Plan §5. Zweck: Vertrags- und
Architekturfundament für LC02+. Dieses Dokument bündelt Entscheidungen, Rollen-/
Zustandsmatrix, Options-/Einstellungsvertrag und `.http`-Konventionen. ADRs liegen
unter `docs/architecture/adr/`.

Status: **offen → lokal geprüft (Doku + Verträge erstellt; extern ausstehende
Prüfungen unten).**

## ADR-Überblick (sechs ADRs)

| ADR | Datei | Kernentscheidung |
| --- | --- | --- |
| ADR-0002 | Fachportal-und-Portal-BFF | separates optionales Portal + BFF; keine Tokens im Browser; CSRF |
| ADR-0003 | Optionale-Node-RED-Laufzeit | Node-RED optional-technisch, Sandbox-gebunden, kein Auto-Start in Tests |
| ADR-0004 | Vertrauensbereiche-und-Node-RED-Sandbox | Vertrauensbereiche getrennt entrollen; Sandbox-Regelmatrix |
| ADR-0005 | Artefaktversionierung-mit-Editions-Direktorium | immutable publish; Direct-Overwrite verboten; Editions-IDs |
| ADR-0006 | Datenmapping-und-Konfigurationsvertrag | Literal vs. Variablenref vs. Konfig ref, Fail-fast; FEEL begrenzt |
| ADR-0007 | Aktivierungs-und-Veröffentlichungsmodell | Aktivierungsdeltas; inkrementelles App-Downgrade; Editions-Release |

## Rollen-/Zustandsmatrix (Statusmodell, RBAC-Aspekt)

Veröffentlichung und Aktivierung laufen über Editions-/Release-Zustände. Rechte
sind instanz-/editionsbezogen (data-scoped); **UI-Verstecken ersetzt keine Autorisierung.**

| Zustand / Aktion | Rolle (Minimal) | Erlaubt |
| --- | --- | --- |
| Definition/Formular als Edition anlegen | Autor (tenant) | editieren, neue Edition öffnen, veröffentlichen (Staging) |
| Staging-Edition veröffentlichen | Veröffentlicher | Release binden; direktes Overwrite auf lebendem Objekt **verboten** |
| Release aktivieren | Veröffentlicher/Admin | Aktivierungsdelta anwenden (inkrementell) |
| App-Downgrade | Admin | nur semantisch erlaubtes, inkrementelles Downgrade; sonst **verboten** |
| Abruf einer Edition/Release | Nutzer mit Leserecht (edition-scoped) | Abruf unveränderlicher Edition |
| Sandbox-/Technik-Flow (Node-RED) | eigener Vertrauensbereich/Worker | nur regulierte Web-/Datei-/Netzzugriffe |

## Options-/Einstellungs-Vertrag (Konfiguration)

- **Eindeutige Herkunft:** jede Option ist genau eine der drei Kategorien:
  - **Literal** — Wert unverändert gesetzt (z. B. feste Basis-URL).
  - **Variablenreferenz** — `{{name}}` gegen Engine-Variablen aufgelöst (obsolet falls Ziel keine Variable).
  - **Konfigurationsreferenz** — vertragsgebundener Verweis (vorzugsweise im Mapping hinterlegt).
- **Fail-fast:** uneindeutige/unauflösbare Quelle → Verarbeitung bricht mit Editions-/
  aufruforientiertem Fehler ab (kein stiller Fallback, kein Demote).
- **Binding-Version:** Mappings/Bindings bekommen eine **Versionsnummer**; ältere
  Verträge bleiben für Bestand lesbar, neue Nur-Daten-Verträge sind versioniert.
- **Schema-/FEEL-Umfang (Mindest):** arithmetische Grundoperationen, STRING/NUMBER/
  BOOLEAN-Typen, Template-Zusammenstellung; darüber hinausgehendes FEEL 1.1 nur wo
  explizit deklariert.
- Keys sind **kanonisch klein**: `vertex:connector.*`, `ioMapping`, `appa`-Namensraum
  bleiben gegenüber LC02 unveränderte Referenzen; neue Verträge nur mit Version.

## `.http`-Konventionen (Plan §4.3)

- **Hauptdatei:** `src/VertexBPMN.Api/VertexBPMN.Api.http` bleibt kurzer Smoker und
  verweist auf `Http/`.
- **Katalog:** thematische Dateien in `src/VertexBPMN.Api/Http/<bereich>.http`;
  jede endet mit `# HTTP-Request-Catalog · <bereich> · LCxx`.
- **Umgebung:** `http-client.env.json` (versioniert, nicht-sensitiv) für Host/Ports/
  GUI-Leitwerte. Tokens & Secrets kommen **nur** aus Editor-Variablen/Umgebung,
  **kein Literal-Secret** in einer `.http`-Datei.
- **Anforderung je Request:** Soll-Antwort als Kommentar (`# Soll: 200 …`); Pfad/
  Methode/DTO erforderlich; wiederholbare Negativfälle zur Stabilisierung der Baseline.
- **IDs:** echte IDs aus Antworten übernehmen, nicht erfinden — für deterministische
  Reihenfolge echte Rückgaben mitschneiden.

## OIDC-Bausteine-Inventur

Siehe eigenes Dokument `2026-10-13_Low-Code-LC01_OIDC-Bausteine-Inventur.md`.

## Verbindliche Verträge

Zentrale Vertragssammlung unter
`docs/reference/low-code-application-contracts.md` — deckt ab: Konfigurationsherkunft
(Literal/Variable/Konfiguration/Transformation), Binding-Version, JSON-Schema-Draft
2020-12 + Validatorwahl (JsonSchema.Net), FEEL-Umfang (feelin 7.0.1),
OIDC-Bausteine/-Verträge (inkl. zwei Portal-Replikate, Refresh/Rotation/Logout),
Persistenz/Migrationen/Rollback, Optionsklassen-Tabelle, Fehlercodes + Cancellation +
Rollen-/Objektmatrix sowie die `.http`-Konventionen aus §7.3.

## Geliefert

1. ADRs ADR-0002 … ADR-0007 (6 Dateien)
2. `2026-10-13_Low-Code-LC01_OIDC-Bausteine-Inventur.md`
3. `docs/reference/low-code-application-contracts.md` (verbindliche Verträge)
4. `Http/authorization-and-errors.http` (Negativfälle: fehlender/ungültiger Token,
   ReadOnly-, Fremdtenant-, Worker-Rolle, falscher Worker-Topic) — Routen/DTOs aus
   Code verifiziert
5. dieses LC01-Fortschrittsdokument

## Offen / extern ausstehend (LC01)

- Ausführen der `.http`-Requests gegen isolierte laufende API mit echten getrennten
  Testidentitäten (Smoke/`authorization-and-errors.http`).
- JSON-Schema-Validator-Referenz kommt erst dort ins Projekt, wo Validierung real
  benötigt wird (LC04 Formulare), nicht als Leerlauf-Referenz.
- Portal-/Node-RED-Laufzeit: LC06/LC08.
