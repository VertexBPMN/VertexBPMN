# Git-Modellversionierung für VertexBPMN Studio – Implementierungsplan

Stand: 2026-10-05. Planungsbasis: `master`, Commit `cb88bee0f83807cd0d7906a4d092807a56b49969`.

Fortschritt 2026-10-06: [deterministische echte Commitobjekte](2026-10-06_Git-G04_Commitobjekte.md) als Vorarbeit für Write-Reconciliation ergänzt. 63 lokale Adaptertests bestanden. G04 ist damit begonnen, aber nicht abgeschlossen; dauerhafter Worker, Ref-CAS und Push bleiben offen. Ältere Angaben „G04 nicht begonnen“ unten sind historische Teilstände.

Aktueller Folgestand 2026-10-06: [endlicher Commit-Executor, Ref-CAS und lokale Reconciliation](2026-10-06_Git-G03_G04_Commit-Ausfuehrung-und-Reconciliation.md) mit 73 lokalen Adaptertests, 16 SQLite-Storefällen und drei WSLC-PostgreSQL-Abnahmen bestanden. Git-Write mit anschließendem DB-Abschlussfehler wird anhand des vorher gespeicherten Belegs abgeglichen. Gehosteter Dispatch mit frischer Rollenauflösung/Heartbeat, Remote-Push und Provenienz bleiben offen. Kein vollständiger G02/G03/G04-Abschluss.

Die aktuelle CI-safe Gesamtsuite ist erfolgreich abgeschlossen: 1.380 bestanden, 0 Fehler, acht bestehende Opt-in-Fälle übersprungen; Dauer 15 Minuten 27,138 Sekunden. Die zwischenzeitlich beobachtete ungewöhnliche Laufzeit im bestehenden ML-Dauerprognosetest und der verbleibende Diagnoseordner sind im Folgebericht dokumentiert. Offene Feature-Abnahmen bleiben davon unabhängig offen.

Aktuelle Nutzerentscheidung: SQL-Server-Abnahme am 2026-10-05 ausdrücklich aus dem laufenden Arbeitsauftrag ausgenommen. Keine Testinstanz bereitstellen und keine Verbindung mehr anfordern. Das ursprüngliche Kriterium bleibt nachvollziehbar, ist aber **nicht geprüft / nicht bestanden**. Die übrigen G02/G03-Aufgaben laufen weiter. Wartungs-/Hoststart-Nachweise: [G03-Maintenance](2026-10-05_Git-G03_Maintenance.md).

Aktueller Folgestand: echter TLS-/Helper-Git-Fetch mit bytegetreuem Readback, hostile Hooks/Filter/Gitlink, laufendem Timeout/Cancel und typisierte Commit-Annahme lokal bestanden; siehe [Transport-/Annahmebericht](2026-10-05_Git-G02_Transport-und-G03_Annahme.md). Die ältere Statuszusammenfassung darunter beschreibt den vorherigen Teilstand. G02/G03 bleiben offen bis zu den verbleibenden Write-/Reconciliation-/Provenienzgrenzen; SQL Server bleibt ausdrücklich ungetestet.

Status: **Phase 1 abgeschlossen. Phase 2/G02 und G03 in Arbeit: Sicherheitsbasis, persistenter Credentialanschluss, GitHub-App-Tokenbroker, HTTPS-Zielprüfung sowie persistente Bindungen/ACLs/Sessions/Operations und Migration implementiert. SQLite: 5 Persistenztests bestanden; PostgreSQL unter WSLC: echte Migration/Upgrade, Idempotenz und Claims/Fencing bestanden. Native Git-Prozessintegration, Quoten-/Recovery-/Cleanup-Abnahme und weitere Pflichtgrenzen noch offen. G02/G03 nicht abgeschlossen; G04–G11 nicht begonnen. Nachweise: G00/G01, G02-Sicherheitsbasisbericht und [G03-WSLC-Abnahme](2026-10-05_Git-G03_WSLC-Abnahme.md).**

Dieser Plan ist ein eigenständiger Arbeitsauftrag für Menschen und Coding-Agents. Vorheriger Chatverlauf ist nicht erforderlich. Pfade sind repository-relativ. G01-C#-Verträge sowie die in [G02](2026-10-05_Git-G02_Sicherheitsbasis.md) dokumentierte Sicherheitsbasis und das Optionsbinding existieren; Produktprovider und neue HTTP-Routen noch nicht. Weitere neue Typen, Dateien, Routen und Kategorien sind **Vorschläge, keine bereits vorhandenen APIs**. Vor ihrer Implementierung den aktuellen Checkout erneut prüfen.

## 1. Ziel und Lieferumfang

VertexBPMN erhält eine optionale Git-Anbindung für die Quelldateien seiner Modelle. Anwender verbinden ein Repository, öffnen einen Branch, bearbeiten Modelle im bestehenden Studio, vergleichen Änderungen, committen auf einem Arbeitsbranch, pushen und erstellen einen Pull Request. Ein freigegebener, unveränderlicher Commit kann anschließend ausdrücklich deployt werden.

**Git ist die Quelle für Modellhistorie und Review. Die Vertex-Datenbank bleibt die Quelle für deployte Definitionen, Instanzen und Runtime-Zustand. Commit, Push, Merge und Deployment sind getrennte Aktionen.**

### Lieferstufen

| Stufe | Umfang | Abschlussbedingung |
| --- | --- | --- |
| A – erster nutzbarer Lieferumfang | BPMN, generischer Git-Zugriff über HTTPS, GitHub-Pull-Requests, Konfliktschutz, nachvollziehbares Deployment | G00–G09 vollständig abgenommen |
| B – Modellfamilien | DMN, CMMN und Formulare mit Referenz-/Release-Manifest | G10 separat implementiert und abgenommen |
| C – dynamisches DLL-Plugin | Vertrauenswürdig ausgelieferter Git-Adapter über ein abgesichertes Plugin-Framework | G11 separat implementiert und abgenommen |

Stufe A wird als austauschbares, fest registriertes Source-Control-Modul umgesetzt. Das ist keine Behauptung einer funktionsfähigen dynamischen DLL-Installation. Der derzeitige Plugin-Sicherheitsblocker bleibt bis G11 bestehen. GitLab/Azure-DevOps-PR-Adapter, SSH, Git LFS und Echtzeit-Kollaboration sind weitere, ausdrücklich nicht zum ersten Lieferumfang gehörende Erweiterungen.

### Nutzerablauf in Stufe A

1. Berechtigte Person hinterlegt Repository-Verbindung, erlaubte Modellverzeichnisse, Branchregeln und Credential-Referenz.
2. Modellierer wählt Repository und Branch; Studio zeigt Ausgangscommit und Modellpfad.
3. BPMN öffnen, mit bisherigen Editorfunktionen bearbeiten, Änderungen vergleichen.
4. Änderungen explizit als Snapshot auf einem Arbeitsbranch committen; späteres Editieren bleibt uncommitted.
5. Push und Pull Request ausführen; Konflikte oder fehlende Rechte verständlich anzeigen.
6. Nach Review/Merge einen freigegebenen Commit auswählen, Ziel/Tenant prüfen und Deploy ausdrücklich bestätigen.
7. Deployte Definition zeigt Repository, Commit, Datei, Inhalts-Hash und Deploymentergebnis. Laufende Instanzen behalten ihre bisherige Definitionsbindung.

## 2. Verifizierte Ausgangslage

Die ursprüngliche Planung beruhte auf Quellcode-/Dokumentlektüre. In Phase 1 wurden Restore, ursprünglicher Produkt-Build, Contract-/Application-Prüfungen und nach autorisierter OpenAPI-Korrektur Solution-Build, Swagger-Regression, CI-safe Suite sowie lokale Chromium-Editor-Vertragsprüfungen ausgeführt; Ergebnisse in [G00](2026-10-05_Git-G00_Inventur-und-Baseline.md) und [G01](2026-10-05_Git-G01_Vertraege-und-Sicherheitsgrenzen.md). Editorprüfungen verwenden isolierte Assets bzw. eine Stub-API; keine echte Infrastruktur-, Git-Transport- oder Produktionsabnahme behaupten.

| Befund | Code-/Dokumentverweis | Konsequenz |
| --- | --- | --- |
| Plugin-Verträge liegen im API-Projekt; generischer Aufruf über Methodennamen/Objektparameter | `src/VertexBPMN.Api/Plugins/IPlugin.cs`, `PluginContext.cs`, `PluginManager.cs` | Neue Source-Control-Verträge typisieren; nicht über generisches Execute freigeben |
| Sicherheitsvalidierung lehnt DLL-Aktivierung mangels konfigurierter Prüfungen ausdrücklich ab | `src/VertexBPMN.Api/Plugins/PluginSecurityManager.cs` | Keine Abschaltung zur Beschleunigung; Stufe A ohne dynamischen Loader |
| Plugin-Servicecontainer ist ein eigenes Dictionary, kein Host-DI-Container | `src/VertexBPMN.Api/Plugins/PluginServiceContainer.cs` | Explizite Providerregistrierung/-auflösung und korrekte Lifetimes |
| Assembly-Auflösung ist ein Platzhalter und liefert `null` | `src/VertexBPMN.Api/Plugins/PluginAssemblyLoadContext.cs` | Eigener Framework-Ausbau erst in G11 |
| BPMN-Seite bietet Import/Export, Deployment und Versionsbereich | `src/VertexBPMN.Studio/Components/Pages/BpmnModelerPage.razor` | Git ergänzen; bestehende Aktionen, Command-Stack und Layoutkonventionen erhalten |
| Aktiver Repository-Service kommt aus Application | `src/VertexBPMN.Application/ApplicationModule.cs`, `RepositoryService.cs` | Nicht den gleichnamigen Infrastructure-Service versehentlich erweitern |
| Deployment validiert BPMN und erzeugt tenantbezogene Definitionsversionen | `src/VertexBPMN.Application/RepositoryService.cs` | Validierung und Engine-Verträge wiederverwenden; bestehende Versionsermittlung allein ist kein Idempotenznachweis |
| Webhook-Synchronisation folgt im Controller auf das Deployment | `src/VertexBPMN.Api/Controllers/RepositoryController.cs`, `src/VertexBPMN.Application/WorkflowTriggerService.cs` | Gemeinsame Orchestrierung für normalen und Git-Deploy inklusive Triggerpfad prüfen |
| Geschützter Credential-Dienst vorhanden | `src/VertexBPMN.Domain/Interfaces/ICredentialService.cs`, `src/VertexBPMN.Infrastructure/Persistence/Services/PersistentCredentialService.cs` | Referenzen verwenden; Secret-Auflösung ausschließlich intern und tenantgeprüft |
| Reale lokale Browsertests und Use-Case-Katalog vorhanden | `tests/VertexBPMN.Studio.UiTests/`, `docs/testing/studio-use-cases.tsv`, `docs/testing/studio-acceptance-coverage.md` | Neue Git-Szenarien nachvollziehbar ergänzen, nicht als bestehend bestanden ausgeben |
| Schnelle Tests laufen mit Microsoft.Testing.Platform; externe Kategorien werden ausgeschlossen | `global.json`, `.github/workflows/ci.yml`, Schritt `Test (CI-safe suite)` | Native-Git-, Remote- und Browserabnahme nicht ungefiltert in CI aufnehmen |

Weitere Orientierung: [Studio-Workspace-Plan](2026-09-09_Studio-Modellierungsworkspace_Implementierungsplan.md), [Produkt-Support-Matrix](../reference/product-support-matrix.md), [Deployment-Runbook](../runbooks/production-deployment.md). Historische Planstände sind kein Beweis aktueller Implementierung; G00 sucht vorhandene Entwürfe, Kontextschutz und Deployment-Idempotenz erneut.

## 3. Architektur und verbindliche Grenzen

### 3.1 Komponenten

| Vorgeschlagener Baustein | Verantwortung / Ort |
| --- | --- |
| `VertexBPMN.SourceControl.Abstractions` | Kleine hostunabhängige Vertragsbibliothek: revisionsgebundene DTOs, Providerfähigkeiten, Fehlercodes, Cancellation; keine API-/EF-Abhängigkeit |
| `IModelSourceControlProvider` | Git-Operationen: Revision lesen, Baum/History/Diff, Commit und Push; keine Hosting-spezifischen PR-Operationen |
| `IRepositoryHostingProvider` | Anbieter-API für PR-Erstellung/-Status und verifizierten Review-/Merge-Status |
| `Application/SourceControl/` | Berechtigungen, Anwendungsfälle, Snapshot-/Konfliktregeln, Jobs und Deploymentauftrag |
| `Infrastructure/SourceControl/` | Native-Git-Adapter, HTTPS-Authentifizierung, GitHub-Adapter, kontrollierte Arbeitsbereiche und Persistenzadapter |
| `Api/Controllers/SourceControlController.cs` | Typisierte HTTP-Verträge; authentifizierten Actor/Tenant serverseitig ermitteln |
| `Studio/Services/SourceControl/` und Komponenten | Repositoryauswahl, Dateiliste, Status, Diff, Commit-/PR-/Deploymentdialoge |

Bevorzugter Adapter für A: installierte, unterstützte Git-CLI hinter einer austauschbaren Schnittstelle. Die unterstützte Mindestversion erst in G00/G01 anhand benötigter Sicherheitsfunktionen festlegen und dokumentieren. Kein Git im Browser; kein Shell-Befehl aus Benutzertext. Fehlt Git, bleibt nur die aktivierte Integration nicht verfügbar, nicht die gesamte API/Engine.

### 3.2 Identität und Autorisierung

- Repositorybindung hat eine eigene ID und einen zwingenden Tenant. Serverzuordnung statt Vertrauen in Request-Tenant oder frei übergebene URLs/Pfade.
- G01 definiert Repository-ACLs und ordnet Lesen, Bearbeiten/Commit, Push/PR, Deploy und Verwaltung den bestehenden Rollen/Policies zu. Repo-Leserecht gewährt nicht automatisch Deploymentrecht.
- Jede Operation, Jobabfrage und Secret-Referenz wird tenant-/ressourcenbezogen autorisiert. Hintergrundjobs tragen serverseitig bestätigten Actor und Tenant; vor externen Schreibaktionen Berechtigungsentzug berücksichtigen.
- Keine gemeinsam genutzten Benutzer-Credentials oder Arbeitsverzeichnisse über Tenantgrenzen. Providercredentials können von der Hosting-Installation stammen, Vertex-Autorisierung bleibt davon unabhängig.
- Standardbetrieb über validierte HTTPS-Hosts. Lokale Dateiremotes ausschließlich für isolierte Testadapter oder ausdrücklich administrativ freigegebene Serverwurzeln; keine Browserwahl beliebiger Serverpfade.

### 3.3 Revisionen, Bytes und Konflikte

- Commit-ID als opaque vollständige Object-ID behandeln, keinen festen SHA-1-Längenvertrag einbauen. Branchname ist veränderbar und ersetzt keine Commit-ID.
- Editorstatus hält Repository/Branch/Pfad, Basiscommit, Dokumentgeneration, lokale Revision und Snapshot-Hash. Remote-, Git- und Deploymentversion getrennt anzeigen.
- Commit verarbeitet genau den bestätigten Snapshot. Verspätete Antworten für Revision N dürfen N+1 nicht als committed markieren.
- Commit-, Push- und Deploy-Requests tragen Idempotenzschlüssel. Gleicher Schlüssel mit anderem Inhalt/Ziel ergibt Konflikt.
- Vor Write den erwarteten Remote-Head prüfen; explizite Ref-Ziele und serverseitig prüfbare Aktualisierungsbedingungen verwenden. G04 muss auch das Rennen zwischen Fetch und Push testen; bloßer vorheriger Vergleich reicht nicht.
- Arbeitsbranches sind verbindlicher Standard; keine Default-Branch-/Tag-Schreibrechte, automatischen Merges, Resets oder Force-Pushes. Remote-Änderung führt zu `409 Conflict`, eigener Snapshot bleibt erhalten. Nutzer kann separat sichern, neu öffnen oder abbrechen.
- Unveränderte Dateien bytegetreu erhalten; keine stillen Zeilenende-, Encoding- oder XML-Formatänderungen. Bei Bearbeitung semantische Erhaltung von IDs, DI, Bedingungen und Extensions zusätzlich prüfen. Änderungen der Serialisierung im Diff transparent darstellen.
- Textdiff ist im ersten Lieferumfang verbindlich. Vorhandenen Modellvergleich nur mit nachgewiesener Semantikerhaltung integrieren; kein automatisches semantisches XML-Merge versprechen.
- Git erlaubt fachlich noch nicht ausführbare Entwürfe, aber nicht unzulässige Pfade, Größen, gefährliches XML oder verbotene Secret-Inhalte. Deployment bleibt fail-closed validiert.

### 3.4 Prozess-, Netzwerk- und Dateisicherheit

- Absoluten administrativ festgelegten Git-Pfad verwenden; `ProcessStartInfo.ArgumentList`, keine Shell und keine frei erlaubten Git-Argumente. Branch/Ref/Revision separat validieren, optionsähnliche Eingaben zurückweisen.
- Kontrollierte Prozessumgebung, keine übernommenen Git-Konfigurations-/Credential-Helper-/Trace-Variablen. Hooks, externe Filter/Diffprogramme, Editor/Pager, rekursive Submodule und LFS-Ausführung deaktivieren. Konfiguration aus Repository und Host darf keine unkontrollierten Programme starten.
- Secrets nicht in Remote-URL, Argumentliste, `.git/config`, Ergebnisartefakten oder Logs. Kurzlebige Authentifizierung über einen internen Credential-Kanal; Umsetzung und Restexposition separat security-reviewen. Keine interaktiven Credential-Prompts.
- TLS-Prüfung nicht abschalten. Host-/Port-Allowlist, sichere Redirect-Policy und Schutz gegen private/Loopback/Link-local-/Metadata-Ziele sowie DNS-Rebinding; lokale Testausnahmen explizit isolieren. Provider-API-Ziele ebenso prüfen.
- Dateipfade relativ zum erlaubten Modellroot normalisieren. Traversal, absolute/UNC-Pfade, Symlinks/Reparse Points, Gitlinks, Case-Kollisionen, reservierte Windowsnamen und `.git` ablehnen. Dateien aus Git-Objekten kontrolliert lesen, keine ZIP-Extraktion ohne denselben Schutz.
- Pro Sitzung/Job eigener Clone für A. Keine gemeinsam schreibbare Checkout-Sitzung; Worktrees mit gemeinsamem Git-Metadatenbereich sind kein Sicherheitsersatz.
- Quoten für Clonevolumen, Objekt-/Dateigröße, Dateianzahl, parallele Jobs, Diff/History und Laufzeit. Prozessbaum bei Timeout/Cancel beenden; keine beliebigen festen Sleeps.
- Cleanup nur für eindeutig zugeordnete Arbeitsbereiche innerhalb geprüfter Root; Aufbewahrung/Persistenz von ungesicherter Arbeit sichtbar regeln. Niemals Benutzerrepositories löschen.
- Git bei Aktivierung aktuell und gewartet halten; Repositorydaten sind untrusted. Ein kontrollierter Git-Prozess allein ist keine Sandbox für beliebigen Fremdcode.

### 3.5 Deployment und Wiederanlauf

- Deploy liest Bytes aus einer festen Commit-ID und prüft Hash, Repositoryrechte, freigegebenen Stand, Tenant und Ziel. Ein Branchwechsel während des Jobs darf die Bytes nicht verändern.
- Im MVP ist ein freigegebener Stand ein über den Hosting-Adapter nachweisbar gemergter PR-Commit auf einem konfigurierten geschützten Releasebranch. G01 konkretisiert Prüfung, Provenienz und Rechte; ein UI-Häkchen oder Commitautor ist kein Reviewnachweis.
- Offline-Git ohne Hosting-Review erhält in A Lese-/Commit-Funktionen, aber keine behauptete automatische Freigabe für Produktion. Eine zusätzliche manuelle Freigabepolicy wäre separat zu entscheiden.
- Provenienz: Tenant, Repository-ID, Commit-ID, Pfad, Content-Hash, Freigabenachweis, Ziel, Actor, Idempotenzschlüssel, Definitions-ID/-Version und Ergebnis.
- Git-Push und DB-Deployment bilden keine gemeinsame Transaktion. Persistenter Auftrag mit eindeutiger Operation-ID, Lease/Fencing, Status und Wiederanlauf ist erforderlich.
- Gemeinsame Deploymentorchestrierung muss Definitionsanlage, Provenienz und die benötigten Triggeränderungen im vorhandenen Persistenzmodell absichern. Nach Crash unmittelbar nach Definitionscommit darf Retry keine zweite Version erzeugen. Transaktions-/Reconciliation-Grenze mit realer DB nachweisen, nicht über read-then-insert vortäuschen.
- Webhook-Secrets nur im vorhandenen Einmal-Anzeigepfad; niemals in Git/Provenienz speichern. Verhalten bei Antwortverlust ausdrücklich definieren.
- Parallele normale und Git-Deployments derselben Prozesskennung dürfen keine Versionskollisionen erzeugen. Bestehende Instanzbindungen, Tenantregeln, externe Taskverträge und Validatoren erhalten.
- Git-/Hosting-Ausfall blockiert keine laufenden Prozesse und keinen normalen lokalen Modellierungs-/Deploymentpfad.

## 4. Arbeitspakete und Reihenfolge

Aufwand relativ einschließlich Tests/Review: S = klein, M = mittel, L = groß; keine garantierte Agent-Laufzeit. G00 darf anhand des echten Ist-Stands neu schätzen.

| ID | Phase / Paket | Priorität | Aufwand | Abhängigkeiten |
| --- | --- | --- | --- | --- |
| G00 | Phase 1: Inventur und Baseline | Muss A | S | keine |
| G01 | Phase 1: Verträge, Rechte, Freigabe und Limits | Muss A | M | G00 |
| G02 | Phase 2: Credentials und Sicherheitsgrenzen | Muss A | L | G01 |
| G03 | Phase 2: Persistenz, Jobs und Arbeitsbereiche | Muss A | L | G01 |
| G04 | Phase 3: Realer Git-Provider | Muss A | L | G02, G03 |
| G05 | Phase 3: Autorisierte Source-Control-API | Muss A | M | G04 |
| G06 | Phase 4: Studio-BPMN-Git-Workspace | Muss A | L | G05 |
| G07 | Phase 4: GitHub-Review/Pull-Requests | Muss A | M | G02, G05; UI nach G06 |
| G08 | Phase 5: Commitgebundenes Deployment | Muss A | L | G03, G05, G07; UI nach G06 |
| G09 | Phase 6: Lokale Gesamt- und Remote-Abnahme | Muss A | L | G00–G08 |
| G10 | Folgephase: DMN/CMMN/Formulare | Muss B | L | G09 |
| G11 | Folgephase: Abgesichertes DLL-Plugin | Muss C | L | G09, separater Framework-/Security-Review |

G02/G03 können nach abgestimmtem G01 unabhängig vorbereitet werden. G07 und G08 dürfen nicht gleichzeitig dieselben Studio-/Deploymentdateien verändern. Pro aktivem Paket klare Dateiverantwortung und kleine Revieweinheiten; keine unkoordinierte Paralleländerung des Modelers.

### G00 – Inventur und Baseline

- [x] Aktuellen Status/Commit, `AGENTS.md`, README, Supportmatrix und diesen Plan lesen. Branch `codex/git-source-control-phase-1` angelegt; bestehenden Plan erhalten.
- [x] Source-Control-/Draft-/ETag-/Job-/Idempotenz-/ACL-Implementierungen repo-weit gesucht; wiederverwendbare Pfade und bestätigte Lücken in G00 dokumentiert.
- [x] Aktive DI und Transaktionsgrenzen für Definitionen, Credentials, Trigger und Jobs geprüft; ungenutzten Infrastructure-Repositoryservice nicht als Hauptpfad behandelt.
- [x] Lokale Profile/Testhost-/Runnerparameter und Windows-Git erfasst. Linux-Git und WSLC-Backend unqualifiziert; als offene Voraussetzung dokumentiert, kein Gitzwang für deaktivierte Integration.
- [x] Release-Build und anwendbare CI-safe Baseline ausführen; bekannte Fehler vor Änderungen eindeutig erfassen. Editor-Kernabläufe lokal verifizieren, soweit Infrastruktur verfügbar. Lokale Chromium-Vertragsfälle bestanden; echte DB-/Broker-Gesamtabnahme nicht durchgeführt.

Abnahme: Inventurbericht mit Dateien, Befunden, tatsächlich ausgeführten Befehlen und offenen Voraussetzungen. Keine Produktänderung nötig, um G00 als Inventur abzuschließen; fehlende Laufzeitbaseline ausdrücklich offen lassen.

Stand: [G00-Bericht](2026-10-05_Git-G00_Inventur-und-Baseline.md). OpenAPI-Abhängigkeit, Bearer-Dokumentreferenz und Profiler-Lebenszyklus/Messkontext korrigiert; finaler Solution-Build erfolgreich (0 Fehler, 59 Warnungen). Swagger- und zwei lokale Editor-Vertragsfälle bestanden. Finale CI-safe Suite: 1.279 bestanden, kein Fehler, acht externe Fälle übersprungen. Keine Assertions abgeschwächt oder Ausschlüsse ergänzt. G00 mit dokumentierten externen Voraussetzungen abgeschlossen.

### G01 – Typisierte Verträge und Produktentscheidungen

- [x] Packagefreie Abstractions-Bibliothek und Application-Referenz implementiert; Git/Hosting getrennt, keine API-/Plugin-Abhängigkeit.
- [x] DTOs und Snapshotinvarianten für Bindung, Revision, Tree/History/Diff, Session, Commit/Push, PR, Job und Provenienz spezifiziert/implementiert; alle I/O-Verträge mit zwingender Cancellation.
- [x] Rollen-/ACL-Matrix und Default-/Releasebranch-Regeln inklusive Fremdtenant, fehlenden Claims, ReadOnly und Admin verbindlich definiert. Reale Handler-/ACL-Prüfung folgt in G02/G05, nicht als bereits bestanden behauptet.
- [x] Fehlervertrag mit stabilen Codes und redigierten HTTP-Regeln definiert; ausführbare Endpunkte folgen in G05.
- [x] `SourceControlOptions` mit disabled-default, Git-Pfad/Root/Hosts/Limits/Timeouts/Retention definiert; noch kein Hostbinding oder Startupzwang. Laufzeitvalidierung folgt in G02.
- [x] Review-/Merge-Nachweis, Secret-/Exportpolicy, technische Botidentität/Vertex-Actor, Audit und Aufbewahrung verbindlich festgehalten.
- [x] Quantitative Limits und Performanceziele vor Laufzeitmessungen festgelegt; echte Durchsetzung und Messung folgen in G02–G09.

Abnahme: überprüfbarer Vertrag mit Beispielen für Erfolg, Konflikt, fehlende Rechte, Ausfall und Deployment. Ungeklärte sicherheitsrelevante Entscheidungen blockieren die betreffende Umsetzung.

Stand: [G01-Vertrag](2026-10-05_Git-G01_Vertraege-und-Sicherheitsgrenzen.md), 44/44 isolierte Vertragstests bestanden; Application-Teilbuild ohne Warnung/Fehler. G01-Vertragsumfang und Phase-1-Baseline abgeschlossen, nicht gleichbedeutend mit Git-/Security-/Produktabnahme.

### G02 – Authentifizierung und Schutz

Teilstand: [G02-Sicherheitsbasis](2026-10-05_Git-G02_Sicherheitsbasis.md) und aktueller [Phase-2-Abschlussstand](2026-10-05_Git-Phase2_Abschlussstand.md). Credentialadapter, kurzlebiger GitHub-App-Broker, echter Helper-Prozess, HTTPS-Handler und Native-Git-Prozessgrenze implementiert; lokale Vertrags-/Negativfälle bestanden. Vollständige Transport-/Recovery-Abnahme bleibt offen. Die folgenden Paketcheckboxen bleiben bis zur vollständigen Umsetzung/Abnahme offen.

- [ ] Vorhandenen Credential-Dienst über tenantgeprüften internen Adapter verwenden. Für GitHub bevorzugt repositorybegrenzte GitHub-App-Installation mit kurzlebigen Installationstokens; kein verpflichtender globaler PAT.
- [ ] Git- und Hosting-Tokenkanal, Rotation/Ablauf/Widerruf sowie Rechteentzug implementieren. App-Schlüssel nur geschützt serverseitig; Studio zeigt Metadaten, nie Secretwerte.
- [ ] Prozess-/URL-/Ref-/Pfadvalidierung und Limits aus Abschnitt 3.4 umsetzen. Konfiguration fail-closed prüfen, gefährliche Transportprotokolle ausschließen.
- [ ] Inline-Secrets-/Redaktionsregeln an vorhandenen Export-/Deploymentschutz anschließen. Erkannte unsichere Inhalte blockieren; sichere Credential-Referenzen anbieten, Original nicht still maskieren und committen.
- [ ] Securitytests einschließlich Injection, SSRF, Hooks/Filter, Secretleckage und tenantfremder Credential-Referenz ergänzen.

Abnahme: negative Tests greifen über echte Adaptergrenzen; keine Testsecrets in Prozessargumenten, URL, Git-Konfiguration, Logs oder Artefakten. Einschränkung: beliebige Secrets in opaque Skripten sind nicht vollständig automatisch erkennbar; Review bleibt erforderlich.

### G03 – Persistenz, Operationen und Arbeitsbereiche

- [ ] Bindungen/ACLs, Edit-Sessions, Operationen und spätere Provenienz persistent modellieren; vorhandene EF-/Jobmechanismen prüfen, nicht parallel einen InMemory-Store bauen.
- [ ] Migrationen für unterstützte DB-Profile samt Upgrade aus bestehendem Stand testen. Optionale Integration darf deaktivierten Bestandsbetrieb nicht auf Git/Hosting-Secrets angewiesen machen.
- [ ] Idempotenz mit DB-Unique-Constraints und bedingten Zustandsübergängen, Claims/Leases/Fencing, Retry und Statusabfrage implementieren.
- [ ] Isolierte Clones/Sessiongenerationen, Quoten und sicheren Cleanup implementieren. Retention uncommitteter Snapshots entscheiden; keine nicht belegte Wiederherstellung nach Tabverlust versprechen.
- [ ] Aktive Jobs nach Neustart erkennen; Schreibresultate anhand erwarteter IDs/Hashes abgleichen. Nebenwirkungen nicht blind wiederholen.

Abnahme: parallele Replikate erzeugen pro Operation nur einen wirksamen Write; realer DB-Neustart-/Lease-Test; fremde und außerhalb Root liegende Pfade werden nicht gelesen/gelöscht.

### G04 – Git-Provider

- [ ] Fähigkeiten/Verfügbarkeit, Clone/Fetch, Branchliste, revisionsgebundene Dateiliste, Dateiinhalt, History und paginierten Diff implementieren.
- [ ] Explizite Änderungsliste und Commit auf isoliertem Arbeitsbranch; sonstige Repositorydateien unverändert erhalten. Keine pauschalen `add --all`-/`push --all`-Operationen.
- [ ] Push mit erwartetem Remote-Stand, eindeutiger Zielref und atomarem Konfliktschutz implementieren; Standard-/Releasebranch nicht direkt beschreiben. Race auch für Remote-Reset/neuen Commit testen.
- [ ] Statusmodelle `committed-local`, `pushed`, `conflict`, `result-unknown` sauber trennen. Nach verlorenem Push-Response remote Commit abgleichen, bevor Retry erfolgt.
- [ ] Dateilöschung/-umbenennung zunächst nur über explizite, bestätigte Änderungen anbieten, sofern G01 sie in A freigegeben hat; sonst capabilitybedingt nicht anbieten.

Abnahme: echte temporäre Repositories und unabhängiger zweiter Clone bestätigen Bytes, History und Remote-Refs. Stubs allein beweisen keinen Git-Transport. Zeitouts/Abbruch und fehlendes Git deterministisch testen.

### G05 – Source-Control-API

- [ ] Vorschlag `/api/source-control` mit typisierten Endpunkten für Bindungen, Revisionen/Dateien, Sessions, Operationen und Status nach G01 implementieren; niemals beliebige Git-Kommandos entgegennehmen.
- [ ] Tenant/Actor aus vertrauenswürdigem Requestkontext, Ressourcenrechte serverseitig; keine Umgehung über generisches `PluginController.Execute`.
- [ ] Lange Writes als `202 Accepted` mit autorisierter Jobabfrage; kleine Reads mit Timeout/Paging. Requests und Results an Sessiongeneration und Basiscommit binden.
- [ ] Rate-/Größenlimits, Cancellation, korrelierbare redigierte Logs/Audit und OpenAPI-Vertrag ergänzen. Reale Authorization-Tests für jeden Endpunkt.
- [ ] Studio nutzt geschützte API-Dienste; falls neue öffentliche SDK-/CLI-Verträge aufgenommen werden, separat freigeben und testen, nicht ungefragt als Nebenarbeit ausbauen.

Abnahme: alle Operationen tenantisoliert; kein Secret/Serverpfad im Response; deaktivierter Modus klar nicht verfügbar ohne Auswirkungen auf vorhandene API.

### G06 – BPMN-Studiointegration

- [ ] Repository-/Branch-/Dateiauswahl in vorhandenes Designsystem integrieren. Git-Historie klar von deployten Definitionsversionen unterscheiden.
- [ ] Öffnen/Branch-/Tenantwechsel mit Dirty-Warnung und Abbruchmöglichkeit. Veraltete Antworten dürfen kein neues Dokument überschreiben; Circuit-Reconnect überschreibt keine lokalen Änderungen.
- [ ] Aktuellen Snapshot, Basiscommit, uncommittete Änderungen und Diff zeigen. Commitdialog mit Nachricht, Arbeitsbranch und expliziter Dateiauswahl; Commit/Pushing/Fehler getrennt anzeigen.
- [ ] Konfliktdialog mit eigenem Snapshot, Remote-Stand, separatem Sicherungspfad und Abbrechen. Kein automatisches XML-Merge und keine versteckte destructive Reset-Aktion.
- [ ] Bestehende Import-/Export-/Undo-/Properties-/Simulation-/Validierungs-/Deploymentaktionen erhalten. Benutzerdefinierte Extensions/DI prüfen; bei möglichem Verlust Original erhalten und Überschreiben sperren.
- [ ] Tastatur, Fokus, Leer-/Lade-/Offline-/Rechtezustände sowie Desktop/schmalen Viewport testen. Git ausfallend/deaktiviert darf den Editor nicht unbrauchbar machen.

Abnahme: echte lokale Playwright-Abläufe GT01–GT06, GT09, GT16; unabhängige Git-/API-Prüfung zusätzlich zur Erfolgsmeldung. Keine neuen Mock-only-Tests als Backendabnahme deklarieren.

### G07 – GitHub-Pull-Requests und Review

- [ ] GitHub-App-Einrichtung und minimale Repositoryrechte dokumentieren; nur autorisierte Installation/Repositorybindung akzeptieren.
- [ ] Pull Request mit Basis-/Arbeitsbranch und Commitbezug erstellen, Status lesen und Studio-Link anzeigen. Keine automatische PR-Freigabe oder Merge-Aktion in A.
- [ ] Bei Timeout nach PR-Erstellung vorhandenen PR anhand gespeicherter Operation und Head/Base abgleichen; Retry erzeugt keinen zweiten PR.
- [ ] Gemergten Commit/Reviewstatus über Hosting-API verifizieren, PR-Head, Mergecommit und Deploymentcommit nicht verwechseln. Bei nicht prüfbarem Freigabenachweis kein freigegebenes Deployment behaupten.
- [ ] Git-Commitautor, Hosting-Bot und tatsächlichen Vertex-Anwender im Audit unterscheiden; Hosting-Rate-Limits/Tokenablauf behandeln.

Abnahme: isoliertes Testrepository, echte PR-Erstellung/Status, verlorene Antwort und Rechteentzug. Temporäre Testbranches/PRs nur in diesem autorisierten Repository erzeugen; keine Produktbranches verwenden.

### G08 – Unveränderliches Deployment und Provenienz

- [ ] Git-Deploymentauftrag mit Commit/Pfad/Hash, Releasefreigabe, Tenant, Ziel und Idempotenzschlüssel persistieren.
- [ ] Gemeinsame Application-Orchestrierung für vorhandenen API-Deploy und Git-Deploy schaffen bzw. vorhandene nutzen: Validatoren, externe Verträge, Definitionspersistenz und Webhook-Synchronisation erhalten.
- [ ] Versionsvergabe/Konkurrenz und Definitions-/Provenienzcommit mit Transaktion, Constraints und Operation-ID absichern. Bei mehreren DB-Kontexten reale Grenzen und Reconciliation implementieren; kein erfundenes atomisches Bundle.
- [ ] Crash vor/nach Definitionscommit, nach Trigger-Synchronisation sowie Antwortverlust gezielt injizieren. Retry löst Status auf ohne zusätzliche Version/Trigger-Nebenwirkung.
- [ ] Deploymentdialog zeigt genaue Quelle/Ziel und Ergebnis. Späteres Editieren verändert weder veröffentlichte Version noch Provenienz. Bereits laufende Instanzen bleiben unverändert.
- [ ] Nachträgliches Deployment älteren Commits als neue geprüfte Definition behandeln, nicht als Umschreiben historischer Daten. Bestehende Tenant-/Freigaberegeln gelten weiterhin.

Abnahme: GT10–GT13, GT17 mit realer Persistenz und bestehendem Runtimepfad. Bei unerfüllter Idempotenz-/Triggergrenze bleibt G08 offen; kein einfaches Commit-and-Deploy ohne Recovery ausliefern.

### G09 – Gesamtabnahme, Betrieb und Dokumentation

- [ ] Alle Muss-A-Fälle aus Abschnitt 5 auf konkrete Tests abbilden und ausführen. Pflicht-Skips, fehlende Infrastruktur und null entdeckte Tests sind keine bestandene Abnahme.
- [ ] Windows-/Linux-Git-Adapter qualifizieren; mindestens reale Studio/API-/PostgreSQL-Abnahme und SQLite-Defaultregression. Andere unterstützte DB-Profile mit vorhandenen lokalen Voraussetzungen nachweisen.
- [ ] Reale Remote-/GitHub-Abnahme nur explizit opt-in mit isoliertem Testrepository. Testdaten/Cleanup dokumentieren; keine Produktionscredentials oder Benutzerrepositorys verändern.
- [ ] Bestehende Editor-/Runtime-/Tenant-/Credentials-Regressionen und CI-safe Suite ausführen. Keine Standardsfixtures abschwächen und keine Browser-/Infrastrukturtests in den schnellen Workflow ziehen.
- [ ] Use-Case-Katalog um `GIT-*`-IDs erweitern; Testmethoden, Varianten, Prüfart und offene Lücken zuordnen. Statische Abdeckung ist kein bestandenes Testergebnis.
- [ ] Runbook erstellen: Aktivierung/Deaktivierung, Git-Version/Installation, Credentials, Branchschutz, Quoten/Cleanup, Backup der Bindungen/Provenienz, Job-Recovery und Safe-Rollback. Remote-Git ersetzt kein DB-Backup.
- [ ] README/Supportmatrix erst entsprechend belegtem Umfang aktualisieren. B/C nicht als supported kennzeichnen, solange deren Nachweise fehlen.
- [ ] Finalen Kandidaten aus sauberem Checkout nachqualifizieren; Bericht mit Commit, Testnamen, Infrastruktur, passed/failed/skipped, Dauer und redigierten Screenshots/Logs.

Abnahme: Stufe A nur bei vollständigen Muss-Nachweisen abgeschlossen. Ohne Remote-/Browserlauf heißt der Stand höchstens „implementiert, extern/lokal nicht vollständig abgenommen“.

### G10 – Weitere Modellfamilien und Release-Manifeste

- [ ] Provideragnostisches Manifest mit Schema-Version, Modellart, Pfad, Schlüssel, Hash und Abhängigkeiten festlegen; alle Dateien stammen aus einem festgehaltenen Commit.
- [ ] DMN/CMMN/Form-Modeler an gemeinsame Source-Control-Dienste anschließen, jeweils eigenes Roundtrip-/Dirty-/Konfliktverhalten testen.
- [ ] Abhängigkeiten wie Decision-, Form- und Call-Activity-Referenzen auflösen, Zyklen/fehlende Referenzen/Zielkonflikte vor Deployment melden. Mutable/latest-Bindings von gepinnten Versionen unterscheiden.
- [ ] Bestehende Decision-/Case-/Form-Dienste verwenden und Provenienz pro Artefakt speichern. Unterschiedliche DB-Kontexte und Versionsmodelle prüfen.
- [ ] Ein Multi-Artefakt-Release darf zunächst als orchestriert mit einzeln sichtbaren Ergebnissen angeboten werden. „Atomar“ nur bei implementierter, getesteter atomarer Aktivierungsgrenze; Teilfehler niemals als Gesamterfolg anzeigen.
- [ ] Wiederanlauf nach Teildeployment und Promotion/Rückkehr zum früheren Release nachweisen; zusätzlich Fixtures mit BPMN→DMN/Form/Call-Abhängigkeiten verwenden.

Abnahme: jeder Modelltyp und Verbundfall hat echte Editor-, Git-, Persistenz- und Deploymentnachweise. G10 ist kein stiller Umfangswechsel während G00–G09.

### G11 – Dynamisch ladbares Git-Plugin

- [ ] Bestehende Plugin-Verträge aus API-Kopplung lösen, Migrations-/Kompatibilitätsstrategie für vorhandene Plugins festlegen. Neue Source-Control-Verträge wiederverwenden.
- [ ] `AssemblyDependencyResolver`, gemeinsame Vertragsidentität, kontrollierte Dependency-/Native-Library-Auflösung, Lifetimes/Disposal und typed Providerauflösung implementieren.
- [ ] Versioniertes Pluginmanifest, explizite Zulassung und überprüfbare Herkunft/Signatur/Integrität umsetzen; Widerruf/Update/fehlgeschlagene Prüfung fail-closed behandeln. Hashprüfung allein macht Fremdcode nicht sicher.
- [ ] Loader darf nur vertrauenswürdig freigegebene Assemblies im Host ausführen. Untrusted Drittanbieterplugins brauchen einen getrennten Prozess mit echter OS-/Dienstisolation; `AssemblyLoadContext` ist keine Sandbox.
- [ ] Disable/Unload während aktiver Jobs: keine neuen Writes, laufende Jobs kontrolliert beenden/fortsetzen; keine Ressourcenlecks oder verlorenen Operationen. Keine Host-DI-Mutation ohne Lifecyclekonzept.
- [ ] Versionsinkompatibilität, unbekannte Abhängigkeit, manipulierte DLL und mehrfaches Laden/Entladen negativ testen.

Abnahme: separat reviewtes Pluginframework und real geladenes Git-Plugin; aktueller pauschaler Blocker wird nur durch nachgewiesene sichere Policy ersetzt, nicht durch `IsValid=true`.

## 5. Verbindliche Abnahmeszenarien

IDs beschreiben geplante Anforderungen, keine bereits vorhandenen Testmethoden. Die Umsetzung ergänzt konkrete Methoden und Nachweise. A-Fälle sind Muss für A; B/C bleiben eigene Gates.

| ID | Ablauf | Erforderlicher Nachweis | Stufe / Prüfart |
| --- | --- | --- | --- |
| GT01 | Repository/Branch wählen, BPMN öffnen, editieren, committen, pushen, neu laden | Zweiter Clone sieht richtigen Commit und Modell; Runtime-Versionen beim Commit unverändert | A / Browser + echter Git |
| GT02 | Vertex- und Fremd-BPMN mit Extensions/DI unverändert öffnen bzw. gezielt editieren | Unverändert bytegetreu; Edit semantisch korrekt; keine verlorenen Extensions | A / Browser + Roundtrip |
| GT03 | Commit N verzögern, währenddessen N+1 editieren | N+1 bleibt uncommitted; Remote erhält nur bestätigten Snapshot N | A / Browser + Git |
| GT04 | Branch/Dokument/Tenant wechseln, während Read/Commit läuft | Dirty-Abbruch möglich; alte Antwort überschreibt keinen neuen Kontext | A / Browser + API |
| GT05 | Zwei Nutzer öffnen denselben Head und ändern dieselbe Datei | Zweiter Write bekommt Konflikt; erster Commit bleibt; eigener Snapshot gesichert | A / echte Git-Clients + Browser |
| GT06 | Remote zwischen Fetch und Push ändern oder zurücksetzen | Erwarteter Ref-Stand wird atomar geprüft; keine fremden Änderungen überschrieben | A / echter Git mit Synchronisationspunkten |
| GT07 | Fremdtenant/ReadOnly/fehlender Claim auf jeden Endpoint/Job/Credential | Kein Lesen/Write/Secretzugriff; vereinbarter Fehler, keine Nebenwirkung | A / API + Persistenz |
| GT08 | Options-/Shell-Injection, SSRF/Redirect, Traversal, Symlink, Hooks/Filter | Kein fremder Prozess/Dateizugriff/unerlaubter Netzwerkzugriff | A / Unit + echter Adapter |
| GT09 | Git fehlt, HTTPS/Token fällt aus, Circuit reconnectet | Klarer Fehler; dirty bleibt; bestehender Editor und Engine weiterhin nutzbar | A / Browser + Fehlerfälle |
| GT10 | Freigegebenen Commit deployen, danach Branch und Editor ändern | Definitionsinhalt/Hash stimmt zum festen Commit; Provenienz bleibt unverändert | A / Browser + DB |
| GT11 | Deploy-Doppelklick, zwei API-Replikate, Antwortverlust | Eine Definitionsanlage pro Operation, konsistente Provenienz/Versionsnummer | A / echte DB + API |
| GT12 | Crash direkt nach Definitionscommit / Triggeränderung; Neustart/Retry | Keine zweite Definition/Triggerwirkung; dauerhafter Status wird aufgelöst | A / reale Recovery |
| GT13 | Neue Git-Version deployen, während alte Instanz an User Task wartet | Alte Instanz behält alte Definition, neue Instanz verwendet neue | A / Runtime + DB |
| GT14 | PR erzeugen, Antwort verlieren, erneut anfordern; Merge prüfen | Genau ein zugehöriger PR; Deployfreigabe bindet tatsächlichen Mergecommit | A / isoliertes GitHub-Testrepo |
| GT15 | Credentialrotation/Tokenablauf/Rechteentzug nach Queueing | Keine unerlaubte Writewirkung; kein Secret in Args/URLs/Config/Logs/Artefakten | A / Adapter + Remote |
| GT16 | Tastatur, schmaler Viewport, Fehler-/Konfliktdialog, vorhandene Modeleraktionen | Erreichbare Aktionen/Fokus; Undo/Export/Simulation/normaler Deploy erhalten | A / lokaler Browser |
| GT17 | Fachlich ungültiges BPMN, gefährliches XML, Inline-Secrets, fehlende Freigabe | Entwurf nach Policy; unsicherer Commit bzw. unzulässiger Deploy blockiert | A / API + Browser |
| GT18 | Timeout/Cancel, zu großes Repo/Diff, mehrere Jobs, Retention/Cleanup | Limits eingehalten, Prozessbaum beendet, keine fremden Dateien gelöscht | A / lokaler Git + DB |
| GT19 | Integration deaktiviert, Upgrade bestehender DB, Windows/Linux | Kein Git-/Hostingzwang; bestehende lokale/WSLC-Profile und Migration intakt | A / Build + lokale Regression |
| GT20 | BPMN/DMN/CMMN/Form-Release mit fehlender Referenz und Teilfehler | Valide Referenzprüfung, transparente Teilergebnisse, sicherer Retry | B / Browser + reale Deployments |
| GT21 | Signierte/zugelassene und manipulierte Plugin-DLL; Disable bei aktivem Job | Kontrollierte Aktivierung/Ablehnung, Lifecycle ohne Datenverlust | C / Loader + Jobs |

## 6. Testausführung und CI-Abgrenzung

### Schnelle Tests

Reine Vertrags-, Rechte-, DTO-, Pfad-/URL- und Zustandslogiktests dürfen Teil der CI-safe Suite sein, wenn sie weder native Gitinstallation noch externen Dienst/Browser voraussetzen. Fakes sind dort als isolierte Vertragstests zulässig, aber kein Transport-/Persistenznachweis.

Vorgeschlagene neue Kategorien: `GitLocalAcceptance` für echte lokale Git-/DB-/Recovery-Prüfungen und `GitExternalAcceptance` für Remote/GitHub. G01 prüft passende vorhandene Kategorien. Werden neue Kategorien im zentralen Testprojekt verwendet, in G05 **gezielt nur deren Ausschlüsse** im bestehenden CI-safe Schritt ergänzen. Keine neue CI-Jobmatrix und keine externen Dienste hinzufügen. Bestehende lokale UI-Kategorie `LocalStudioE2E` beibehalten.

Vorbereitung vom Repository-Root; erst bei tatsächlicher Implementierungsabnahme ausführen:

```powershell
dotnet restore VertexBPMN.sln
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
```

Asset-Skip nur bei unveränderten Editorassets oder separat nachgewiesenem Assetbuild. Nach geänderten JS-/Editorquellen bestehende Studio-npm-Pipeline/Projekt-Targets verwenden, generierte Bundles nicht manuell editieren. Testbefehl und vollständige Ausschlüsse aus `.github/workflows/ci.yml` übernehmen; keine VSTest-Filter erfinden. `--no-build` nur nach aktuellem erfolgreichem Build.

### Lokale und externe Abnahme

- Neuen lokalen Testeinstieg vorschlagen, z. B. `scripts/test-source-control.ps1`, mit expliziter Kategorieauswahl, Git-Version, isolierten temporären Repositories und Resultbericht; Bash-äquivalenten Linux-Einstieg dokumentieren. Noch nicht vorhanden.
- Lokale Dateiremotes beweisen Commit/Push/Ref-Konflikte, **nicht** HTTPS-Authentifizierung. Dafür zusätzlich kontrollierter lokaler HTTPS-Transporttest und isolierter Remote-Lauf.
- Browsertests in vorhandenen echten Host integrieren; `scripts/test-studio-e2e.ps1 -Infrastructure Existing` bzw. `-Infrastructure Wslc` erst nach Prüfung der verfügbaren Dienste/Parameter verwenden. Keine Benutzerinstanzen überschreiben.
- GitHub-Testrepo, Appinstallation und Credentials müssen explizit für Testwrites freigegeben sein. Nicht aus VM-Identität oder Repozugang ableiten, dass externe Mutationen erlaubt sind.
- Native-/Remote-/Browserfälle opt-in lokal; fehlende Infrastruktur als ungetestet ausweisen. Cleanup begrenzt auf dokumentierte Testressourcen; Screenshots/Logs vor Weitergabe auf Secrets prüfen.
- Null entdeckte Tests oder übersprungene Pflichtfälle führen zu nicht bestandener Paketabnahme, nicht zu „grün“.

## 7. Übergabe und Definition of Done

Jedes Paket erhält einen kurzen Bericht bzw. Statusabschnitt mit folgendem Inhalt:

```markdown
## Gxx – Titel
- Status: offen / in Arbeit / blockiert / implementiert, nicht abgenommen / abgenommen
- Basiscommit, Branch und Kandidatencommit:
- Geänderte Dateien und Vertrags-/Migrationsauswirkungen:
- Reproduktion / erwartetes Verhalten:
- Umsetzung und begründete Abweichungen:
- Tests: genaue Befehle, Methoden, Kategorien, Infrastruktur, passed/failed/skipped
- Artefaktpfade und Sicherheits-/Kompatibilitätsgrenzen:
- Offene Entscheidungen und ungeprüfte Abnahmekriterien:
- Nächster sicherer Schritt:
```

Ein Paket ist erst abgenommen, wenn seine Pflichtkriterien und zugeordneten Tests nachgewiesen sind. Implementiert, kompiliert, lokal getestet und remote akzeptiert sind unterschiedliche Zustände. Keine globale Produktionsfreigabe allein aus diesem Featureplan ableiten.

Commit/Push/PR, Plugininstallation und externe Ressourcenerstellung erfolgen nur im Rahmen gesonderter Nutzerautorisierung. Dieser Planauftrag startet keine Dienste, provisioniert nichts und schreibt keine GitHub-App-/Repositoryeinstellungen.

## 8. Fortschritt und Startauftrag

- [x] Plan erstellt und Einstiegspunkte gegen aktuellen Quellcode geprüft.
- [x] G00 – Inventur, Solution-Build, CI-safe Gesamtbaseline und verfügbare Editor-Vertragsbaseline fertig; externe Voraussetzungen dokumentiert.
- [x] G01 – Verträge/Rechte/Freigabe definiert, Bibliothek und 44 isolierte Tests implementiert/geprüft.
- [ ] G02 – in Arbeit: Credential-/HTTPS-/Prozess-/Helpergrenzen sowie echter lokaler TLS-Fetch, Hooks/Filter/Gitlink und laufender Timeout/Cancel bestanden; Linux-/Live-Remotequalifikation noch offen.
- [ ] G03 – Persistenz/Jobs/Arbeitsbereiche implementiert und unter SQLite/WSLC-PostgreSQL teilweise abgenommen; Maintenance-Host mit Crash-Erkennung/Fencing und sicherem Cleanup ergänzt. Effekt-Reconciliation und spätere Provenienz offen; SQL Server auf Nutzerwunsch nicht geprüft.
- [ ] G04 – Git-Provider.
- [ ] G05 – API.
- [ ] G06 – BPMN-Studio.
- [ ] G07 – GitHub-PR/Review.
- [ ] G08 – Deployment/Provenienz.
- [ ] G09 – Gesamt-/Remote-Abnahme.
- [ ] G10 – DMN/CMMN/Formulare, eigene Lieferstufe.
- [ ] G11 – dynamisches DLL-Plugin, eigene Lieferstufe.

**Nächster Schritt: verbleibende Transportgrenzen sowie typisierte Annahme und providergebundene Effekt-Reconciliation gemäß [Phase-2-Abschlussstand](2026-10-05_Git-Phase2_Abschlussstand.md) schließen. SQL-Server-Abnahme wird auf Nutzerwunsch nicht ausgeführt.** Phase 1 ist abgeschlossen; G02/G03 noch nicht vollständig abgenommen. Keine Produktionsreife aus isolierten Sicherheitsregeln, Optionsbinding oder bestandenem Teiltest ableiten.

Kopierbarer Startauftrag für einen Agent:

> Lies `AGENTS.md`, diesen Plan und die verlinkten G00-/G01-Berichte vollständig. Prüfe Branch, Status und aktuelle Implementierung. Phase 1 ist mit verfügbarer lokaler Baseline abgeschlossen; externe Voraussetzungen bleiben offen. Bearbeite nur das nächste freigegebene Paket G02/G03. Erhalte lokale/WSLC-Profile, Tenant-/Credentialschutz, Editoraktionen und Engine-Semantik. Halte Implementierung, Build und Abnahme getrennt; keine Standardsfixtures abschwächen, keinen Plugin-Sicherheitsblocker deaktivieren. Kein Commit/Push/PR oder externer Testwrite ohne entsprechenden Nutzerauftrag. Stelle wesentliche offene Produkt-/Securityentscheidungen konkret vor.

## 9. Technische Quellen

- [Git: Push und Ref-Updates](https://git-scm.com/docs/git-push): Grundlage für sichere Ref-Aktualisierung; der Adapter muss seinen konkreten Konkurrenzvertrag selbst nachweisen.
- [Microsoft: .NET-Pluginaufbau](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support): getrennte Vertragsassemblies, Dependency-Auflösung und Grenzen für untrusted In-Process-Code; relevant für G11.
- [GitHub: GitHub Apps](https://docs.github.com/en/apps/creating-github-apps/about-creating-github-apps/about-creating-github-apps): installationsgebundene Rechte und kurzlebige Tokens; Grundlage für G02/G07.

Quellen am 2026-10-05 geprüft. Vor Implementierung versions-/anbieterabhängige Details erneut verifizieren. Keine dieser Quellen ersetzt die projektspezifische Sicherheits- und Laufzeitabnahme.
