# Low-Code-Anwendungsplattform — Verbindliche Verträge

Stand: 2026-10-13. Verbindliche Grundlage für alle Folgepakete (LC02–LC10) gemäß
[Implementierungsplan](2026-10-13_Low-Code-Anwendungsplattform_Implementierungsplan.md) §5 LC01.
Dieses Dokument ist die referenzierte Vertragssammlung; ADRs liegen unter
`docs/architecture/adr/` (ADR-0002 … ADR-0007).

Gültigkeitsregel: Jeder öffentliche Vertrag besitzt eine **Version**, feste
**Fehlercodes**, definiertes **Cancellation**-Verhalten und **Berechtigungsregeln**.
Ältere Verträge bleiben für Bestand lesbar und werden nur additiv erweitert.

## 1. Konfigurationsherkunft (Literal / Variable / Konfiguration / Transformation)

Eine Binding-Quelle ist genau eine der folgenden vier Kategorien — keine
Mehrdeutigkeit durch Nachdenken beim Menschen:

| Kategorie | Syntax | Bedeutung |
| --- | --- | --- |
| Literal | nackter Wert ohne `{{...}}` und ohne Vertrags-Präfix | Wert wird unverändert an die Ausführung übergeben |
| Variablenreferenz | `{{<name>}}` | gegen Engine-Variablen des aktuellen Kontexts aufgelöst; **hart** abgelehnt, falls Variable fehlt |
| Konfigurationsreferenz | vertragsgebundenes Präfix (Mapping-/Binding-Eintrag) | verweist auf Mapping/Konfiguration, nicht auf Prozessdaten |
| Transformation | expliziter Ausdrucksvertrag (FEEL nur wo deklariert) | Ergebnis wird aus Eingaben berechnet, nie still geraten |

**Regeln:**
- **Fail-fast:** uneindeutige, unauflösbare oder ambivalente Quelle → Abarbeitung
  bricht mit Editions-/aufruforientiertem Fehler ab. Kein stiller Fallback, kein
  Demote-Pfad, keine automatische Umdeutung.
- **Binding-Version:** jedes Mappings-/Binding-Set trägt eine `bindingVersion`
  (integer, ab 2 im neuen Vertrag). Legacy-Templates ohne `bindingVersion` bleiben
  lesbar und werden beim Laden mit Versionshinweis gemeldet (Diagnose), aber durch
  Konvertierung gezielt aktualisiert — Bedeutung wird nie erraten.
- Legacy-`ioMapping` (`name=value`, textuell) wird beibehalten und parallel zum
  versionierten Vertrag unterstützt; Konflikte zwischen Legacy-Konfiguration und
  neuem Binding werden beim Start explizit abgelehnt.

## 2. Schema- und Ausdrucksumfang

- **JSON-Schema-Draft:** **Draft 2020-12**, Validierung mit **JsonSchema.Net**
  (Bibliothek `Json.Schema`, .NET 8+-kompatibel, MIT). Begründung: System.Text.Json-
  nativer Typ-Resolver, aktueller Draft-Stand, aktive Wartung. Validator kommt erst
  dort hin, wo Validierung real benötigt wird (LC04 Formulare), nicht Leerlauf-Referenz.
- **Erlaubte Schemas:** nur **lokale Referenzen** (`$ref` auf Dokument-interne
  Definitionen); **keine entfernten `$ref`**, keine Remote-URLs.
- **Begrenzungen:** maximale Schema-Tiefe (z. B. 32 Ebenen) und maximale
  Schema-/Instance-Größe (z. B. 1 MiB) sind Teil des Validator-Vertrags.
- **FEEL:** vorhandene, eingebettete FEEL-Laufzeit **feelin 7.0.1** über
  `FeelEvaluator.cs` (Jint-Realm, thread-lokal) wird für freigegebene Transformationen
  verwendet. **Kein** zweiter selbst geschriebener Ausdrucksinterpreter.
- **Mindest-FEEL-Umfang:** arithmetische Grundoperationen, STRING/NUMBER/BOOLEAN,
  Template-Zusammenstellung. Darüber hinausgehendes FEEL 1.1 nur wo explizit
  deklariert; bestehende Laufzeit wird auf Mappinganforderungen getestet, nicht volle
  Transformationsfähigkeit unterstellt.

## 3. OIDC-Bausteine und Verträge (Portal-BFF, LC06)

Basierend auf der [OIDC-Bausteine-Inventur](2026-10-13_Low-Code-LC01_OIDC-Bausteine-Inventur.md).
Gemeinsame, minimal nötige Authentifizierungsbibliothek wird **erst nach** LC01-
Entscheidung herausgelöst; keine voreilige Duplikation, kein Zwangsrefactoring.

| Vertrag | Festlegung |
| --- | --- |
| Anmeldung/BFF | serverseitig im Portal-BFF; Authorization-Code + PKCE über vorhandene Bausteine; **keine Tokens im Browser/LocalStorage/SessionStorage/UI-Payload**; CSRF-Schutz für Cookie-Mutationen |
| Token-Refresh | serverseitig, automatisch vor Ablauf; Refresh über Rotations-Endpoint; Schlüsselrotation wird durch Refresh mitgezogen |
| Rotation | Sliding/Refresh-Rotation; alte Refresh-Tokens werden nach Rotation widerrufen |
| Logout | serverseitiger Logout ruft Provider-Endpoint und invalidiert lokale Session; Portal- und Studio-Session getrennt |
| Zwei Portal-Replikate | getrennte (z. B. Redis/DB-basierte) Session-/Key-Stores mit Shared-Store-Registrierung und Schlüsselaustausch; In-Process-Session ist **keine** Produktionspersistenz; Data Protection allein ersetzt den Replikat-Nachweis nicht |
| Workeridentität | **nie als Benutzer ausgeben**; External-Task-Worker nutzt eigene Identität mit exakten Tenant-/Topic-/Profil-Claims |

## 4. Persistenz, Migrationen und Anwendungsrollback

- **Provider:** additive Migrationen je unterstütztem Provider (SQLite, PostgreSQL,
  weitere nach Bedarf); SQL-Skript/Migrations pro Schema-Version.
- **Migration:** `--migrate-only` wird vom Migration-Job vor API/Studio-Traffic
  ausgeführt (implementiert in `Program.cs:221`); reguläre Pods prüfen nur den
  Schema-Stand. **Kein automatischer Schema-Downgrade.**
- **Backup:** siehe `docs/runbooks/*`; RPO/RTO je Art (ADR-0001: RPO ≤ 15 min, RTO ≤ 4 h).
- **Anwendungsrollback:** inkrementelles App-Downgrade über Editions-Direktorium
  (ADR-0007); semantisch unerlaubte Downgrades sind verboten. Rollback ändert **kein**
  DB-Schema zurück (Schema-Downgrade verboten).
- **Lokales In-Memory-Session-Profil** ist Prototyp, keine Produktionspersistenz.
- Editions-/Release-Instanzen tragen optionale `ApplicationReleaseId` für
  durchgängige Versionsbindung; bestehende Instanzen ohne Release bleiben gültig und
  erhalten **kein** erfundenes Release zugeordnet.

## 5. Betriebsoptionen (Optionsklassen)

Neue Optionsklassen dokumentieren mögliche Namen; vorhandene Schlüssel werden zuerst
geprüft, bevor ein neuer Name eingeführt wird. Disabled-Verhalten und Startvalidierung
sind Teil jedes Vertrags.

| Option | Name (Vorschlag) | Disabled-Verhalten |
| --- | --- | --- |
| Low-Code-Paket an/aus | `Modules:LowCode` (Erweiterung von `ModuleOptions`) | betroffene Endpunkte/Seiten 404/disabled, Rest bleibt aktiv |
| Durchschnitt FEEL-Transformationen | `Mapping:Feel` (`FeelOptions`, Default limitiert) | FEEL-Mindestumfang bleibt, keine erweiterten Ausdrücke |
| Import-Referenzen (OpenAPI) | `Mapping:OpenApiImport` | Import erlaubt, Remote-`$ref` abgelehnt |
| External-Task-Vertrag (Objekt-/Array) | `ExternalTasks:VersionedResolver` | Nur skalare Legacy-Verträge, erweiterte Inputs abgelehnt |
| Editions-/Release-Zeiger | `Editions:Store` | immutable publish deaktiviert → Update/Overwrite verboten, Folgepaket bricht ab |

Alle Optionen werden beim Start per `Modules:`/eigener Section gebunden und bei
Fehlkonfiguration **hart** abgelehnt (analog `ModuleOptions`/`AdvancedFeatureOptions`
in `Program.cs`).

## 6. Fehlercodes, Cancellation und Berechtigungsregeln

- **Fehlercodes:** neue Vertragskategorien verwenden stabile **ProblemDetails**-Codes
  (z. B. `LC2_MAPPING_UNAMBIGUOUS`, `LC2_BINDING_VERSION_CONFLICT`,
  `LC5_REVISION_CONFLICT`, `LC4_INVALID_COMPLETION`, `LC8_LEASE_LOST`), je als
  `type`-URI und `code`-Feld; menschenlesbare Meldung per `title`/`detail`.
- **Cancellation:** alle neuen Ablaufflüsse (Import, Deploy, Start, Complete, Release-
  Aktivierung, HTTP-Executor) respektieren `CancellationToken`; Timeouts sind Teil des
  Vertrags (HTTP-Timeout, Schema-/Größen-Limit).
- **Berechtigungsregeln (Rollen-/Objektmatrix):**
  - **Entwerfen:** Autor (tenant) darf Editionen anlegen/veröffentlichen (Staging).
  - **Veröffentlichen:** Veröffentlicher/Admin aktiviert Releases (inkrementell);
    Direkt-Overwrite auf lebendem Objekt **verboten**.
  - **Verwenden:** Nutzer mit Leserecht auf Edition/Release (edition-scoped).
  - **Aufgabenbearbeitung:** serverseitiger Abschlussvertrag prüft Schema + Berechtigung
    + erlaubte Ergebnisvariablen gegen Task-Snapshot.
  - **Worker:** eigene Identität, nie als Benutzer ausgegeben; Tenant-/Topic-/Profil aus
    Claims; Heartbeat nur bei gültiger Lease.
- UI-Verstecken ersetzt **keine** Autorisierung; serverseitige Auflösung bleibt Pflicht.

## 7. Gemeinsame `.http`-Konventionen (Abschnitt 7.3)

- **Umgebungen:** `local-http` (`http://localhost:5263`, Profil `http`) und
  `local-https` (`https://localhost:51869`, Profil `VertexBPMN.Api`) in der
  versionierten, **nicht**-sensitiv `http-client.env.json`. Portal analog am eigenen
  Projekt. **Kein vorkonfiguriertes Produktionsziel.**
- **Variablen:** `baseUrl`, `portalBaseUrl`, `tenantId`, `otherTenantId`, `appKey`,
  Objekt-/Revisions-IDs, Idempotenzschlüssel; lokale IDs aus Voraussetzungen/echten
  Antworten, nicht erfunden.
- **Identitäten (getrennt):** Admin, Fachnutzer, ReadOnly, External-Task-Worker aus
  getrennten Testidentitäten mit passenden Claims; Dateien referenzieren Werte,
  enthalten aber **keine** gültigen Tokens/Passwörter; Authentifizierung wird nie zur
  Vereinfachung abgeschaltet.
- **Secrets:** vorzugsweise ASP.NET Core User Secrets; persönliche Werte alternativ in
  nicht versioniertem `http-client.env.json.user` (`.gitignore` `*.user` geprüft,
  gilt). Keine Bearertoken-Rückkopplung in Logs/Dokumente/gespeicherte Antworten.
- **Aufbau:** Kopf mit Ziel/Voraussetzungen/Identität/Reihenfolge; `# @name`-Requests
  durch `###` getrennt; kommentierte Sollwerte (Status, Responsefelder, Fachwirkung);
  reguläre und negative Fälle getrennt; Mutationen/Cleanup einzeln auswählbar mit
  isolierten IDs. `.http`-Response-Verweise gelten nur dateiintern.
- **Tochterdateien vs. Hauptdatei:** `VertexBPMN.Api.http` bleibt kurzer Smoker und
  verweist auf `Http/<bereich>.http`. Katalog umfasst laut Plan u. a.:
  `runtime-baseline.http` (LC00/02), `connectors-and-mapping.http` (LC02/03),
  `forms-and-tasks.http` (LC04), `applications.http` (LC05/07), `app-runtime.http`
  (LC06/07/09), `external-tasks.http` (LC08), `events-and-endpoints.http` (LC09),
  `application-releases.http` (LC10), `authorization-and-errors.http` (LC01+),
  `Portal/Http/portal-bff.http` (LC06).
- **Definition of Done je Endpunkt:** Requestdatei vorhanden und im gewählten Editor
  ausführbar; Methode/Route/DTO/Auth stimmen mit OpenAPI/Code überein; regulärer +
  relevante Negativfälle dokumentiert und ausgeführt; Requestkatalog/Testanleitung
  aktualisiert; automatisierte Regression bleibt. Ohne diesen API-Lieferumfang gilt ein
  Arbeitspaket nicht als vollständig.
