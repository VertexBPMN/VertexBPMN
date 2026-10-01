# Studio-UI-Abnahme mit WSLC — 2026-09-30

## 1. Entscheidung

**Keine abschließende Freigabe.** Die aktuelle Oberfläche ist in vielen realen Abläufen funktionsfähig, aber die gewünschte vollständige lokale Abnahme ist nicht erreicht. Zwei unveränderte Gesamtläufe scheitern reproduzierbar, der vollständige Containerstart über WSLC ist blockiert und der echte OIDC-Browserpfad verliert ungespeicherte Entwürfe.

Dieser Bericht ersetzt keine Produktions-, Sicherheits-, Last- oder Standardkonformitätsfreigabe. Insbesondere bedeuten aufrufbare Seiten und grüne Heading-Smokes nicht, dass alle ihre Aktionen korrekt funktionieren.

Geprüfter Stand: `master`, Commit `d3fd145c823ac462447b2a05f66963e8d74b87bf`. Zu Beginn war der Checkout sauber. Produktcode, Fixtures, Testassertionen und GitHub-Workflows wurden für diese Prüfung nicht verändert. Kein Commit, Push oder Cloud-Deployment wurde ausgeführt.

Grundlagen:

- [Lokaler GUI-E2E-Testplan](2026-09-01_Lokaler_GUI_E2E_Testplan.md).
- Manueller BPMN-Editor-Testplan unter `C:/Users/yrodriguez/Downloads/VertexBPMN-Studio-BPMN-Editor-Testplan.md`.
- [Compose-Dokumentation](../../deploy/compose/README.md) und [Compose-Konfiguration](../../deploy/compose/docker-compose.yml).
- Tatsächliche Studio-Komponenten, HTTP-Adapter und lokale Tests, nicht nur die bisherigen Fertig-Markierungen.

## 2. Tatsächlich verwendete Umgebung

Windows, .NET SDK 10.0.303 / Runtime 10.0.12, WSLC 2.9.13.0, wslc-compose 0.5.0. Docker und Podman wurden nicht zum Starten eingesetzt.

Separates Compose-Projekt: `vertexbpmn-acceptance`.

| Bestandteil | Ausführung / Adresse |
| --- | --- |
| Engine-PostgreSQL | WSLC-Container, `127.0.0.1:55432`; fünf Engine-Datenbanken |
| RabbitMQ | WSLC-Container, AMQP `127.0.0.1:55672`, Management `127.0.0.1:15672` |
| Keycloak | WSLC-Container, `http://localhost:58080`; eigener PostgreSQL-Container |
| API für sichtbare OIDC-Prüfung | Nativer .NET-Prozess, `http://localhost:51870`, Profil `OidcTest` |
| Studio für sichtbare Prüfung | Nativer .NET-Prozess, `http://localhost:5263`, echter Keycloak-Login |
| Real-E2E-Suite | Separate native API-/Studio-Prozesse mit dynamischen Ports und je Lauf fünf eigenen PostgreSQL-Datenbanken auf der Compose-Infrastruktur |

Die erste sichtbare Prüfung nutzte einen eigens angelegten lokalen Testbenutzer mit Rolle `ProcessManager` und Tenant `tenant-a`. Die anschließende Prüfung mit einem getrennten echten OIDC-Admin-Konto ist in Abschnitt 8 dokumentiert. Der Real-E2E-Testhost nutzt dagegen explizite Development-Anmeldung mit API-Key und kombinierten Rollen `Admin`, `ProcessManager`, `ReadOnly`.

Wichtige Unterschiede des Testhosts: Simulation und Live-Migration aktiviert, C#-Scripts explizit erlaubt, stark erhöhte Rate-Limits, KI/MCP/Plugins deaktiviert. Siehe [LocalStudioE2ETestHost.cs](../../tests/VertexBPMN.Studio.UiTests/LocalStudioE2ETestHost.cs), insbesondere die Umgebungskonfiguration um Zeilen 218–280. Ergebnisse dieses Profils sind nicht ohne Weiteres auf das Compose-/Produktionsprofil übertragbar.

### Warum kein vollständig laufender WSLC-App-Containerstack?

1. **Host-Networking:** Die Compose-Datei verlangt `network_mode: host` für API, Studio und Worker (Zeilen 126, 228, 255). Der direkte WSLC-Probelauf mit `--network host` wurde mit „Das Hostmodusnetzwerk wird nicht unterstützt“ abgewiesen. wslc-compose setzte die App tatsächlich ins Bridge-Netz; ihre `localhost`-Datenbankadresse zeigte deshalb in den App-Container. Der API-Start brach in der Migration mit Npgsql-Verbindungsfehler ab. **Ein erfolgreicher nativer App-Start ist kein Nachweis, dass dieses Containerprofil funktioniert.**
2. **Tool-Kompatibilität:** WSLC liefert bei `images/list --format json` mehrere JSON-Objekte zeilenweise, während wslc-compose 0.5.0 die gesamte Ausgabe mit `json.loads` als ein Dokument erwartet. Zusätzlich weichen Feldnamen `ID`/`Names` von den Erwartungen ab. Ein isolierter Testadapter normalisierte die Ausgabe; die installierten Tools wurden nicht verändert.
3. **Shell-Zeilenenden:** Die ausgecheckten `keycloak-bootstrap.sh` und `healthcheck.sh` enthielten CRLF. Der Bootstrap meldete `set: pipefail\r: invalid option name`. Für diese Prüfung wurden ausschließlich LF-Kopien im ignorierten Ergebnisverzeichnis verwendet.
4. **Portbelegung:** Ein bereits vorhandener nativer PostgreSQL-Dienst belegte Port 5432. Deshalb wurden für die isolierte Testumgebung freie Ports verwendet. Der vorhandene Dienst wurde nicht verändert oder gestoppt.
5. `restart: unless-stopped` wird von diesem Compose-Tool nicht vollständig unterstützt. Der lokale Teststart ignorierte diese nicht unterstützte Einstellung ausdrücklich; Wiederanlaufsemantik wurde damit nicht abgenommen.

API und Studio wurden als ergänzende Prüfung nativ mit den aufgelösten Compose-Einstellungen gestartet. Keine Produktkonfiguration oder Sicherheitsprüfung wurde zur Erlangung eines grünen Containerergebnisses abgeschwächt. Die dokumentierten Publish-Skripte rufen zudem derzeit ausdrücklich `docker compose` auf; sie sind kein WSLC-Einstiegspunkt.

## 3. Ergebnisse der zwei unveränderten Gesamtläufe

| Lauf-ID | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen | Dauer |
| --- | ---: | ---: | ---: | ---: | ---: |
| `e2144118497e4cd4a3f3f2b6153dae45` | 99 | 97 | 2 | 0 | 476,126 s |
| `60a67de4ad204ad688db3c86451f6b8c` | 99 | 97 | 2 | 0 | 455,183 s |

Beide Prozesse lieferten Exitcode 1. Beide Läufe scheiterten an denselben zwei Fällen. Die Resultate stammen aus den XML-/HTML-Berichten, nicht aus einer geschätzten Zahl.

**Die 99 Fälle sind nicht 99 reine UI-Funktionstests:** 73 Fälle verwenden eine Browserseite, davon 30 parametrisierten Route-/Reload-Smokes und vier kleine Viewport-Smokes. 26 Fälle testen BPMN-Execution oder Repository-Validierung über HTTP ohne Browser. Diese helfen bei der Runtime-Prüfung, beweisen aber nicht die grafische Erstellung der entsprechenden BPMN-Elemente.

Die fünf laufbezogenen Datenbanken wurden nach jedem der beiden vollständigen Läufe gelöscht und ihre Abwesenheit verifiziert; `database-cleanup.log` enthält jeweils fünf bestätigte Entfernungen. Die dedizierten Compose-Volumes und die sichtbare Testumgebung wurden behalten. Bestehende fremde Container und der native PostgreSQL-Dienst blieben unberührt.

Zwei frühere Versuche scheiterten bereits beim Fixture-Aufbau: zuerst durch identische Test-Datenbanknamen, danach durch einen fehlenden nativen Apphost nach einem `UseAppHost=false`-Publish. Diese wurden vor den beiden eigentlichen Läufen in der Testvorbereitung korrigiert. Ihre Fixture-Fehler sind **keine 67 Produktregressionen** und nicht in obiger Bilanz enthalten.

## 4. Feature- und Aktionsabdeckung

Testnamen in dieser Tabelle befinden sich, sofern nicht anders angegeben, in [LocalStudioInfrastructureTests.cs](../../tests/VertexBPMN.Studio.UiTests/LocalStudioInfrastructureTests.cs). „Bestanden“ gilt für die konkreten vorhandenen Assertions im angegebenen Testprofil, nicht für jede denkbare Kombination.

| Bereich / Routen | Konkret geprüft | Ergebnis / Einschränkung |
| --- | --- | --- |
| Dashboard `/` | Reales Backend; Refresh, Workload-Anzeige; direkte Navigation und Reload | Basisabläufe bestanden. Der umfangreiche Verwaltungsfall bricht später am Versions-Testselektor ab. |
| BPMN `/bpmn-modeler` | Import; HTTP-Knoten in Sequence Flow; Vertex-Eigenschaften; Validate; Deploy; XML; Reload; Versionen; Export; Token-Simulation; Engine-Testlauf | **Nicht vollständig bestanden:** `BpmnModeler_ImportsEditsDeploysReloadsAndExports_ARealPersistedDefinition` verliert die Credential-Eingabe. Ein separater Simulations-/Engine-Testlauf und weitere Editor-Korrekturfälle bestehen. |
| BPMN mit echtem OIDC | Gültiges Start-/End-Modell bearbeiten, validieren, deployen; Definition nach erneutem Seitenaufruf anzeigen; Prozessstart, Completion und persistierte History; Business Key mit realen Tastendrücken | Erfolgreich für diesen einfachen Ablauf. Kein vollständiger Formular-/Worker-Prozess unter allen OIDC-Rollen. |
| DMN `/dmn-modeler` | Import, grafisch zusätzliche Rule, Deployment, API-Reload, High-/Low-/No-Match-Auswertung, Export und Re-Import | `DmnModeler_ImportsDeploysReloadsEvaluatesAndExports_ARealDecision` bestanden. Zusätzlich OIDC-Seitenaufruf, nicht derselbe komplette OIDC-Aktionspfad. |
| CMMN `/cmmn-modeler` | Import, zusätzliche Human Task, Registrierung, Case-Ausführung, User Event, Case File, Ad-hoc-Aktivierung, History, Export/Re-Import | `CmmnModeler_ImportsRegistersExecutesUpdatesAndExports_ARealCase` bestanden; OIDC-Seite aufrufbar. |
| Forms `/form-builder` | Import, zusätzliche Textkomponente, Save, Registry-Reload, Update mit stabiler ID, Runtime Viewer, JSON-Roundtrip | Development-Real-E2E bestanden. Zusätzlich Formular `AcceptanceForm20260930` mit echtem OIDC gespeichert und nach neuem Seitenaufruf aus Registry geladen. **Ungespeicherter OIDC-Entwurf geht verloren**, siehe F02. |
| n8n-Import | Echter JSON-Import, Mapping-Bericht, `NeedsReview`, Validierung, Deploy, Reload und Export | `N8nImporter_ReportsNeedsReviewValidatesDeploysReloadsAndExports_ARealWorkflow` bestanden. |
| Definitions `/process-definitions` | Suche, BPMN-Anzeige, Versionsdialog, Delete mit Reload; Pagination/Versionierung im umfangreichen Fall | Ein eigener Definitionsfall besteht. Der umfangreiche Verwaltungsfall scheitert reproduzierbar am Testselektor, nicht an einer nachgewiesenen falschen Version. Nicht alle Spaltenfilter-/Menükombinationen abgenommen. |
| Deployments `/deployments` | Gültige/ungültige Datei, Mehrfachupload, Größenlimits, Auffindbarkeit in Definitions | Beide konkreten Deployment-Fälle bestanden; persistiertes OIDC-Deployment sichtbar. |
| Instances `/process-instances` | Liste/Suche, Details, Suspend/Resume/Delete mit API-Abgleich, Reload, leere Suche | Konkrete Real-E2E-Fälle bestanden. OIDC-Prozess Completion, Business Key und History zusätzlich sichtbar geprüft. |
| Tasks `/tasks` | Prozessstart mit Variablen, Claim, reales Taskformular, Eingabe, Complete, Variablen-/History-Nachweis; Abschluss ohne Variablen | `BpmnRuntime_StartsClaimsCompletesAndShowsPersistedHistory_WithARealTaskForm` und Verwaltungsfall bestanden, jeweils Development-Anmeldung. |
| Execution `/execution-details` | Jobs, Incidents, Variablen; ungültige GUID, 404 und Wiederholung/Mehrfachklick | Konkrete Fälle bestanden. Keine künstliche Stub-API. |
| History / Event Log | Persistierte Prozess-/Taskereignisse und Live-Anzeige | Real-E2E bestanden; tatsächlicher OIDC-Start-/End-Verlauf sichtbar. |
| Messages / Signals | Korrelation, nicht passende Korrelation, Broadcast und Runtime-Effekt | `MessagesSignals_CorrelatesMessageAndBroadcastsSignal_ThroughTheRealEngine` bestanden. |
| Triggers `/triggers` | Register, One-Time-Secret, Test-Aufruf, Invocation, Disable/Enable, Delete mit API-Abgleich | `WorkflowTriggers_RegisterInvokeToggleAndDelete_ThroughTheRealEngine` bestanden. Geheimnisse nicht im Bericht. |
| Simulation `/simulation` | Engine-backed Run, Summary/Trace, Szenario-CRUD und Vergleich | Im aktivierten Testhost bestanden. Compose-/OIDC-Profil zeigt deaktivierte Ausführung: dort **nicht positiv abgenommen**. |
| Debugging `/debugging` | Trace, Session, Breakpoint, Step/Continue, Visualisierung, Variablen und Replay | Konkreter Real-E2E-Fall bestanden. |
| Migration `/migration` | Preview, Execute, Status, Snapshot/Restore, Rollback, unzulässige Migration | Im aktivierten Testhost bestanden. Im Compose-/OIDC-Profil deaktiviert, deshalb dort **nicht positiv abgenommen**. |
| Tenants `/tenants` | Create/Update/Switch, Isolation, Delete | Vorhandener Development-Admin-Fall grün, aber tatsächliche Bearbeitung damit nicht bewiesen: echter OIDC-Admin reproduziert Beschreibungslöschung durch Update (F07). OIDC-ProcessManager erhält erwartetes API-403. Keine vollständige echte OIDC-Rollenmatrix. |
| Credentials `/credentials` | Create, nur Metadaten statt Secret-Echo, Rotate/Delete | Konkreter Development-Admin-Fall bestanden; OIDC-ProcessManager sieht keine Änderungsaktionen. |
| Connectors `/connectors` | Create/Test/Toggle/Delete gegen echte API | Konkreter Real-E2E-Fall bestanden; echte OIDC-Erstellung, Toggle-Persistenz und Anzeige-Trennung zusätzlich geprüft. Erfolgsanzeige hat Error-Severity (F08). Test ist nur Konfigurationsprüfung, kein HTTP-Verbindungstest und kein UC5-Nachweis. |
| Feature Flags `/feature-flags` | Switch, API-Persistenz und Wiederherstellung | Development-Admin-Fall bestanden; OIDC-ProcessManager sieht `Admin only`. |
| Engine Management / Configuration | Read-only Seite bzw. Capabilities | Seiten laden. Engine-Management-Mutationen sind im API-Adapter ausdrücklich `NotSupportedException`; der Header suggeriert trotzdem eine Auswahl/Refresh-Funktion, siehe F03. |
| Health `/health` | Comprehensive Health, Metrics und Circuit Breaker mit echter API | Antwort vollständig lesbar, kein beobachteter ResponseEnded-Abbruch. **Inhalt fehlerhaft**, siehe F04. `Degraded` wegen ca. 90 % belegter lokaler Platte ist dagegen ein Umweltbefund. |
| Performance / Analytics | Seiten und Backend-Daten; Trainingsdatenexport mit Erfolg oder verständlichem Fehler | Smoke-/Fehlerbehandlung bestanden. Erfolgreiches Modelltraining, korrekter Exportinhalt und Performance-Messwerte nicht umfassend validiert. |
| Extensions / SSO / Compliance | Existierende Anzeige, echte OIDC-Anmeldung; read-only Information | Keine Abnahme von Plugin-Installation, Azure AD/Okta, vollständigem Logout-/Sessionablauf oder Zertifizierung. SSO-Seite ist eine Integrationsliste, kein vollständiger Konfigurationsdialog. |
| External Agent | Preset deployen, Prozess starten, wartenden prozessbezogenen Job in UI/API anzeigen | [ExternalAgent-Test](../../tests/VertexBPMN.Studio.UiTests/LocalStudioInfrastructureTests.ExternalAgent.cs) bestanden. Kein echter LLM-/Ollama-Worker-Abschluss; AgentWorker nicht gestartet. |
| Navigation / Responsive / Errors | 30 direkte Routen einschließlich `/counter`, Reload, vier Routen bei 390×844; unbekannte Route; 404; Doppelklick; leere Suche | Vorhandene Fälle zweimal bestanden. Im sichtbaren Browser zusätzlich alle 29 Menüziele aufgerufen. Ein aufgerufenes oder gerendertes Menüziel allein gilt nicht als vollständige Feature-Abnahme. |

### Abgleich mit dem manuellen BPMN-Plan

| Use Case | Stand dieser Prüfung |
| --- | --- |
| UC1 Start–Task–Ende | Realer UI-Runtime-/Taskformular-Test bestanden; einfacher Start–Ende-Prozess zusätzlich mit OIDC. Vollständige grafische Neuerstellung jedes Knotens nicht separat abgenommen. |
| UC2 Exclusive Gateway | API-Execution für beide Condition-Pfade plus grafischer Katalog-IF-Fall bestanden. Nicht jede Properties-Kombination. |
| UC3 Parallel Gateway | Runtime-API-Fork/Join bestanden; kein eigenständiger vollständiger manueller grafischer Fork/Join-Neubau. |
| UC4 User Task / Formular | Vollständiger vorhandener Browserfall mit Taskabschluss bestanden. |
| UC5 Echter HTTP-Connector | **Offen/blockiert.** Credential-Eingabe fehlerhaft. Der erfolgreiche CalculateScore-Servicehandler ist kein echter HTTP-Response-Nachweis. |
| UC6 Timer Boundary | Interrupting und non-interrupting Runtime-API-Fälle bestanden. Grafische Boundary-Erstellung separat offen. |
| UC7 Error Boundary aus HTTP-Fehler | **Nicht abgenommen.** Die Timer/Message/Signal-Boundary-Fälle beweisen nicht Error-Propagation aus einem real fehlgeschlagenen HTTP-Connector. |
| UC8 Embedded / Call Activity | Embedded-Execution API-seitig bestanden. Vollständiger Drill-down/-up und echter parent/child Call-Activity-Browserpfad offen. |
| UC9 Collection-basierte Multi-Instance | Parallel/sequential Execution API-seitig bestanden; vollständige grafische Konfiguration offen. |
| UC10 XML-Roundtrip / Version / ungültiges Modell | Einzelne Editor-/Repository-Fälle bestanden; vollständiger großer BPMN-Roundtrip und Verwaltungsfall wegen F01/F05 nicht grün. |

**Die gesamte Elementmatrix 2.1–2.11 ist nicht vollständig abgenommen.** Unter anderem sämtliche Start-/End-/Throw-Varianten, Transaktionen/Compensation, Event-Subprocess-Kombinationen, jedes Task-/Gateway-Properties-Feld, alle Import-Fehler, Undo/Redo-/Keyboard-/Zoom-Kombinationen und alle Grenzwerte benötigen eigene Browserfälle. API-Execution-Tests dürfen dafür nicht als grafische UI-Nachweise umetikettiert werden.

## 5. Konkrete Befunde und nächste Schritte

### F01 — P1: Vertex-Properties verlieren beim Tippen den Fokus

**Reproduziert in beiden Real-E2E-Läufen und zusätzlich im sichtbaren OIDC-Browser.**

- Im fehlgeschlagenen Test wurde `credential-<RunId>` eingegeben. Das exportierte XML enthielt stattdessen `<vertex:connector ... credentialRef="c" />`.
- Sichtbar: HTTP-Pattern einsetzen, Properties öffnen, HTTP-Service-Task auswählen, Vertex aufklappen, im Feld `Credential ref` einen echten Tastendruck `c` ausführen. Der DOM-Feldwert ist `c`, das aktive Element anschließend `BODY` statt dieses Eingabefelds.
- Kein Browser-Console-Fehler im betroffenen Test erforderlich: Datenverlust kann ohne JS-Exception auftreten.
- Quelle: [vertex-properties-provider.js](../../src/VertexBPMN.Studio/tools/bpmn-io/src/vertex-properties-provider.js), `textEntry`, Zeilen 99–125. Die React/Preact-Komponentenfunktion wird im Entry-Factory-Aufruf inline neu erzeugt; jeder sofortige Command-Stack-Write kann zur Neuerzeugung/Unmount des Editors führen. Das ist die aus Code und Fokusbefund abgeleitete Ursache, nicht bereits eine implementierte Reparatur.

**Lösung:** Stabile Komponentenidentität herstellen, Konfigurationsparameter über Props/Closure mit stabiler Factory zuordnen, Fokus und Cursor nach jedem Model-Write erhalten. Normales mehrbuchstabiges Tippen, Paste, schnelle Feldwechsel, Undo/Redo und Deploy mit noch fokussiertem Feld durch Browser-/XML-/Repository-Assertions nachweisen. Generated Assets über die bestehende `npm ci` / `npm run build:bpmnio`-Pipeline neu bauen; nicht nur das Bundle editieren. Den bestehenden Credential-Test nicht auf Parser-Prüfung oder vereinzeltes Nachfokussieren pro Buchstabe abschwächen.

### F02 — P1: OIDC-Refresh setzt ungespeicherte Bearbeitungen zurück

**Sichtbar beobachtet:** In einem eigenen Form-Builder-Tab wurde der Schlüssel per einzelnen Tastendrücken auf `acceptance-draft` geändert und per DOM/Screenshot bestätigt. Ohne Save, eigene Navigation oder Reload war bei der nächsten Kontrolle nach 34 Sekunden wieder `invoice-approval` vorhanden; eine zweite Kontrolle nach 74 Sekunden bestätigte den Reset. Zuvor verschwand auch ein ungespeichertes HTTP-Pattern aus dem BPMN-Editor ohne eigene Navigation.

Passende technische Ursache: [oidc-session-refresh.js](../../src/VertexBPMN.Studio/wwwroot/js/oidc-session-refresh.js), Zeilen 2 und 36–37: periodischer Session-Refresh führt bei `renewed === true` zu `window.location.reload()`. [OidcCookieRefreshEvents.cs](../../src/VertexBPMN.Studio/Services/OidcCookieRefreshEvents.cs), Zeilen 20–26, setzt die Renewal-Markierung. Der lokale Keycloak-Realm hat 300 Sekunden Access-Token-Laufzeit. Der konkrete Refresh-Response wurde bei dieser Sichtprüfung nicht mit einem Network-Trace korreliert; die Refresh-Zuordnung ist daher eine stark durch Code gestützte Diagnose. Der Verlust des Entwurfs selbst ist direkt beobachtet.

**Lösung:** Token-/Cookie-Synchronisierung darf keine unbedingte Seitenneuladung mit Draftverlust erfordern. Circuit-Kontext und sichere Rollenneubewertung erhalten; falls ein Reload aus Sicherheitsgründen nötig ist, Entwürfe vorher tenant-/benutzergebunden sichern und wiederherstellen. Test mit echten kurzen Keycloak-Tokens, mehr als einer Refreshperiode, offenen BPMN-/DMN-/CMMN-/Form-Entwürfen und unveränderten Artefakten danach. Role-/Tenant-Revoke muss weiterhin sicher greifen. Entwicklungstests ohne OIDC erkennen diese Regression nicht.

### F03 — P2: Engine-Kontext im Header ist irreführend

API-Readiness, Deployment und Prozessausführung funktionieren, während der Header `engine1` / `Disconnected` zeigt. `Refresh engines` besitzt keinen funktionierenden API-Vertrag: [HttpBpmnEngineService.cs](../../src/VertexBPMN.Studio/Services/HttpBpmnEngineService.cs), Zeilen 235–243, wirft für Engine-Connection-Management `NotSupportedException`.

[ActiveEngineService.cs](../../src/VertexBPMN.Studio/Services/ActiveEngineService.cs), Zeilen 8–10, 23 und 40–49: initial `false`; ein „Connection Check“ würde lediglich einen nicht leeren Engine-Namen auswerten, keine tatsächliche API-Antwort. [MainLayout.razor](../../src/VertexBPMN.Studio/Components/Layout/MainLayout.razor), Zeilen 47–80, stellt trotzdem Auswahl, Status und Refresh dar.

**Lösung:** Für die aktuelle Single-Endpoint-Architektur den konfigurierten Endpoint anzeigen und Readiness/Erreichbarkeit tatsächlich prüfen, inklusive Timeout und Wiederholung. Nicht implementierte Auswahl-/Registry-Aktionen entfernen oder eindeutig sperren. Tests: API erreichbar → korrekter Zustand, API gestoppt → disconnected, Wiederanlauf → recovered, OIDC-403 nicht als Netzwerkverlust verschleiern.

### F04 — P2: Health zeigt unzutreffende Datenbankgesundheit und negative Uptime

Sichtbare echte API-Antwort: `healthy_databases: []`, Beschreibung `All databases healthy: ` und Status Healthy; außerdem z. B. `uptimeSeconds: -5415.4215005` trotz seit ca. 30 Minuten laufendem Prozess.

Quelle: [ProductionHealthMonitoringService.cs](../../src/VertexBPMN.Api/Services/ProductionHealthMonitoringService.cs).

- Zeile 47: `GetServices<DbContext>()` kann leer sein, wenn nur konkrete Context-Typen registriert sind. Eine leere Sammlung wird trotzdem als Healthy ausgegeben.
- Zeile 56: Der boolesche Rückgabewert von `CanConnectAsync()` wird nicht ausgewertet; `false` ohne Exception zählt aktuell ebenfalls als gesund.
- Zeilen 326 und 377: `DateTime.UtcNow - process.StartTime` mischt UTC mit lokaler Startzeit und erzeugt im Windows-Zeitzonenprofil negative Werte.

**Lösung:** Alle benötigten konkreten Datenbanken explizit prüfen, fehlende Registrierung und `CanConnectAsync() == false` als Fehler behandeln, UTC-konsistente oder monotone Uptime verwenden. UI-/API-Tests mit intaktem und nicht erreichbarem lokalen PostgreSQL sowie Europe/Berlin-Zeitzone. Die Check-Description muss die tatsächlich geprüften fünf Datenbanken enthalten. Der separat gemeldete knappe Plattenplatz ist keine dieser Code-Regressionsursachen.

### F05 — P2: Mehrdeutiger Versions-Testselektor verhindert vollständige Verwaltungsabnahme

`ProcessManagement_DashboardAndDefinitions_RefreshPaginateVersionViewAndDeletePersistently` scheitert in beiden Läufen an [LocalStudioInfrastructureTests.cs](../../tests/VertexBPMN.Studio.UiTests/LocalStudioInfrastructureTests.cs), Zeile 591: `GetByText("v1", Exact = false)` trifft sowohl den Chip `v1` als auch die Namenszelle `Management v1`.

**Das ist ein nachgewiesener Testfehler, kein Nachweis einer falschen Produkt-Versionierung.** Nachfolgende Schritte dieses Testfalls wurden dadurch nicht ausgeführt.

**Lösung:** Den Versionschip beziehungsweise die versionsbezogene Tabellenzeile eindeutig adressieren und beide Versionen, XML-Zuordnung und nachfolgende Lösch-/Reload-Persistenz unverändert prüfen. Anschließend den vollständigen Fall und beide Gesamtläufe wiederholen. Keine Assertion streichen.

### F06 — P1 für die gewünschte Abnahme: WSLC-Gesamtprofil und Restabdeckung fehlen

**Lösung zuerst:** Dokumentiertes lokales WSLC-Profil ohne vorausgesetztes Host-Networking vorbereiten. OIDC-Issuer/Audience, erlaubte lokale Authority und Container-Erreichbarkeit sauber trennen; nicht pauschal Sicherheitsprüfungen ausschalten. Konkrete WSLC-/Compose-Versionen pinnen oder kompatibel machen, Shell-Dateien über `.gitattributes` als LF sichern, freie Ports parametrisieren und Linux-Publish explizit wählen. Bestehende Docker-/Aspire-/native Profile erhalten.

**Danach:** Dieselben Browser-Use-Cases gegen vollständig laufende App-Container und den echten OIDC-Login wiederholen. Echte getrennte Admin/ProcessManager/ReadOnly- und zweite-Tenant-Konten, Login/Logout, Refresh/Entwurfsschutz, 401/403 und Isolation abdecken. UC5/7/8 und die noch nicht abgedeckten grafischen Element-/Aktionenfälle ergänzen. Plugin-/LLM-/Training-Erfolg nur mit tatsächlich vorhandenen lokalen Voraussetzungen prüfen; nicht als bestanden markieren, wenn nur das Fehlermeldungsverhalten getestet wurde.

## 6. Reihenfolge zur Freigabe

1. WSLC-Startprofil reparieren und unveränderte Docker-/native Profile erhalten (L).
2. Credential-Fokus-/Speicherfehler, OIDC-Draft-/Tenantverlust und Tenant-Update-Datenverlust beheben; konkrete Browserregressionen hinzufügen (M–L).
3. Versions-Testselektor korrigieren, Status-/Health-Daten berichtigen und Ausfall-/Recovery-Fälle ergänzen (M).
4. Konfigurationsabhängige Funktionen/Rollen für das gewählte Abnahmeprofil explizit festlegen; übrige manuelle GUI-Use-Cases vollständig ausführen (L).
5. Zwei vollständige Läufe auf jeweils sauberer Datenbasis: **0 Fehler, 0 unerwartete Skips**, inklusive echtem OIDC-Aktionspfad. Vollständige Aktionsmatrix mit Nachweisen erneut bewerten.

Die historischen Grün-Markierungen im Plan vom 2026-09-01 sind keine aktuelle Freigabe dieses Commits. Insbesondere die frühere 8/8- bzw. 20/20-Bilanz ersetzt nicht die jetzigen 97/99-Ergebnisse; Kriterium „zweimal vollständig grün“ ist für diesen Stand **nicht erfüllt**.

## 7. Lokale Belege und Wiederholung

Alle ausführlichen Artefakte liegen **lokal und gitignored** unter:

`C:/repo/VertexBPMN/tests/VertexBPMN.Studio.UiTests/TestResults/acceptance-2026-09-30`

- `real-suite-e2144118497e4cd4a3f3f2b6153dae45/results.html` und `results.xml`.
- `real-suite-60a67de4ad204ad688db3c86451f6b8c/results.html` und `results.xml`.
- Je Browser-Test Screenshot, Playwright-Trace, Console-/Request-Diagnose sowie API-/Studio-Logs; einzelne parametrisierte Varianten verwenden denselben Szenario-Unterordner.
- Je Lauf `runner.log` und `database-cleanup.log`.
- `oidc-bpmn-deployed.png`, `oidc-process-completed.png`, `oidc-business-key-persisted.png`, `oidc-health.png`, `oidc-form-reloaded.png`.
- `oidc-draft-before-refresh.png` / `oidc-draft-after-refresh.png` und `oidc-route-*.txt` für die sichtbare Prüfung.
- Isolierte lokale Start-/Override-/Tooladapter unter demselben Ergebnisverzeichnis. `compose.env` enthält ausschließlich Testgeheimnisse und darf weder veröffentlicht noch committen werden.

Die lokale Wiederholung in dieser vorbereiteten Umgebung erfolgte zweimal mit:

```powershell
& ./tests/VertexBPMN.Studio.UiTests/TestResults/acceptance-2026-09-30/run-real-suite.ps1
```

Der Wrapper setzt explizit `VERTEXBPMN_STUDIO_E2E_ENABLED=true`, pro Lauf eine neue Run-ID, die dedizierte WSLC-PostgreSQL-Containeradresse und fünf verschiedene Datenbankbasen. Er ruft den vorhandenen Release-Runner mit `-trait Category=LocalStudioE2E -parallelMode none -longRunning 30 -result-html ... -result-xml ...` auf. Der Wrapper ist keine neue dauerhafte Produkt-Startanleitung.

Zum Standard-Runner siehe [test-studio-e2e.ps1](../../scripts/test-studio-e2e.ps1); dessen Voraussetzungen und tatsächliche Host-/Portwahl vor einem frischen Lauf prüfen. UI-/WSLC-/OIDC-/LLM-Prüfungen wurden nicht in CI verschoben.

**Zurückgelassene Testdaten:** OIDC-Prozessdefinition `Acceptance_20260930`, vier abgeschlossene Testinstanzen (davon eine mit Business Key `AB`) und Formular `AcceptanceForm20260930`, ausschließlich in den dedizierten Acceptance-Datenbanken. Sie dienen der sichtbaren Nachprüfung. Test-API und Test-Studio laufen bei Erstellung dieses Berichts weiterhin; kein Aspire-AppHost ist gestartet. Außer diesem Bericht sind keine getrackten Produktänderungen Bestandteil der Prüfung.

## 8. Ergänzende sichtbare Prüfung als echter OIDC-Admin

Separates Keycloak-Testkonto `acceptance-admin-3fcd5bb5`, ausschließlich Client-Rolle `Admin` für `vertexbpmn-api`, kein Keycloak-/Systemadministrator. Der vorhandene ProcessManager wurde nicht hochgestuft. Erfolgreiche Anmeldung im sichtbaren Studio und Zugriff auf die Tenant-Verwaltung ohne den bisherigen ProcessManager-403. Dieselben nativen Apps und dieselbe isolierte WSLC-Infrastruktur; keine neue vollständige Suite ausgeführt. Die beiden 97/99-Bilanzen bleiben unverändert.

### Durchgeführte Aktionen

| Fall | Tatsächlicher Nachweis | Ergebnis |
| --- | --- | --- |
| Tenant erstellen | `Acceptance Admin 20260930`, Name/Beschreibung per Tastatur, Create, Tabelle, vollständiger Reload | Name dauerhaft gespeichert. Beschreibung zunächst gespeichert, aber in Liste nicht geliefert; Update ist fehlerhaft, siehe F07. |
| Neuer Tenant im Header | Auswahl direkt nach Create öffnen, anschließend vollständiger Reload | Direkt nach Create fehlt der Tenant; erst nach Reload auswählbar. `MainLayout` lädt seine Liste nur bei Initialisierung (Zeilen 115–136), ohne Invalidierung durch Tenant-CRUD. |
| Tenant bearbeiten | Update im eigenen Tabellenrow, keine Edit-Felder/Dialog; zweiter eigener Tenant mit DB-Vorher/Nachher | **Nicht bestanden: Beschreibung wird gelöscht**, F07. Kein vorhandener fremder Tenant wurde geändert. |
| Credentials | Eigener Tenant, leere Liste, Pflichtfelder und gesperrter Create-Button | Basisanzeige/Leereingabe geprüft. Create/Rotate/Delete und OAuth2-Connect wurden in diesem sichtbaren OIDC-Admin-Lauf **nicht abgeschlossen**. Die grüne Development-Suite ist kein Ersatz für diese OIDC-Aktionsprüfung. |
| Connector-Leereingabe | Create ohne Name/Type | API weist mit 400 zurück, kein Datensatz. UI verliert die fachliche ProblemDetails-Erklärung und zeigt nur `Response status code ... 400 (Bad Request)`. |
| Connector erstellen | `Acceptance HTTP 20260930`, Typ `http`, eigener Tenant, keine Credentials/Templates, bewusst unerreichbarer Loopback-Port 59999 | Datensatz in echter Registry sichtbar. Keine echten Geheimnisse und keine externe URL verwendet. |
| Connector-Test | Enabled → Test; anschließend Disabled → Test | Enabled meldet Konfiguration gültig, nicht Verbindung erfolgreich. Disabled meldet `Enable the connector before testing.`. Erfolgsanzeige trotzdem rot, F08. |
| Connector-Persistenz | Disable → vollständiger Reload → eigenen Tenant neu wählen → weiterhin Disabled → Enable | Status persistiert; abschließend wieder Enabled. |
| Tenant-Anzeige-Trennung | Auf Acme wechseln → eigener Connector fehlt; zurück auf eigenen Tenant → erscheint wieder | Anzeige-Trennung funktioniert. **Kein** Ersatz für negative API-Autorisierungstests zweier Nicht-Admin-Konten. |
| Feature Flag | `liveinspector` ursprünglich true → false → vollständiger Reload → false → true → Refresh | Persistenz funktioniert, Originalzustand wiederhergestellt. Keine Freigabe anderer Flags oder Nachweis ihres gesamten Runtime-Effekts. |
| Extensions | Anzeige, Refresh, vier Extension Points, Load ohne DLL-Pfad gesperrt | Basisbedienung bestanden. Keine Plugin-DLL installiert, kein Enable/Disable/Unload eines echten Plugins getestet. |

Bei den Reload-Prüfungen und später auch ohne eigene Navigation fiel die Admin-Tenant-Auswahl auf `Default` zurück; die Credential-Seite wechselte zu „Select a tenant“. Der Context speichert den Tenant nur im Circuit ([StudioTenantContext.cs](../../src/VertexBPMN.Studio/Services/StudioTenantContext.cs):5–17). Dieser Reset erweitert F02: Ein vom Refresh erzeugter neuer Circuit verliert nicht nur Entwürfe, sondern auch den ausgewählten Tenant. Ein eigener Reload ist gesondert von dem beobachteten unangeforderten Reset zu unterscheiden. Die OIDC-Refresh-Zuordnung bleibt eine codegestützte Diagnose, kein neu korrelierter Network-Trace.

Ein Versuch, isolierte Credential-Metadatenfixtures über einen separaten `admin-cli`-Token vorzubereiten, erhielt API-401; **keine Credentials wurden angelegt**. Das ist kein nachgewiesener Fehler des funktionierenden Studio-Logins und kein Anlass, Audience-/Client-Prüfungen zu deaktivieren. Die Credential-Aktionsabnahme bleibt offen.

### F07 — P1: Tenant-Update löscht gespeicherte Beschreibungen

**Direkter Vorher-/Nachher-Nachweis an eigenen Testdaten:**

1. Tenant `Acceptance Description 20260930` in der UI mit Beschreibung `Beschreibung muss Update ueberleben` erstellt.
2. Read-only PostgreSQL-Abfrage gegen `vertexbpmn_tenants`, Tabelle `Tenants`: Beschreibung vor Update vorhanden.
3. Studio zeigt dennoch eine leere Description-Zelle.
4. Ein Klick auf Update ohne Eingabeänderung; dieselbe DB-Abfrage liefert danach `NULL`/leere Anzeige. Der Name bleibt erhalten.

Ursachenkette:

- [HttpIdentityService.cs](../../src/VertexBPMN.Studio/Services/HttpIdentityService.cs):7–13 liest `/api/identity/list-tenants` in ein Studio-Modell mit Description.
- Der Vertrag [IIdentityService.cs](../../src/VertexBPMN.Domain/Interfaces/IIdentityService.cs):28 enthält nur `TenantInfo(Id, Name)`; [PersistentIdentityService.cs](../../src/VertexBPMN.Infrastructure/Persistence/Services/PersistentIdentityService.cs):35–40 projiziert ebenfalls nur diese beiden Werte.
- [Tenants.razor](../../src/VertexBPMN.Studio/Components/Pages/Tenants.razor):86–88 sendet direkt den unvollständigen Listeneintrag, ohne Bearbeitungsdialog oder Detailabruf.
- [TenantController.cs](../../src/VertexBPMN.Api/Controllers/TenantController.cs), `Update`, überschreibt die gespeicherte Description mit diesem `null`.

**Konkrete Reparatur:** Vollständige Tenant-CRUD-Daten aus `/api/tenant` oder einem explizit vollständigen DTO laden; Update in einem echten Edit-Dialog mit initialisierten aktuellen Werten ausführen. Nicht bekannte Felder dürfen nicht als Löschauftrag interpretiert werden. Create/Update/Delete müssen die Header-Auswahl invalidieren. Regression: Beschreibung anlegen → Liste/Reload zeigt sie → Edit-Abbruch unverändert → nur Name ändern → Beschreibung bleibt → Beschreibung ausdrücklich ändern/leeren → DB-/API-/UI-Abgleich. Der bisherige No-op-Update-Test beweist keine funktionierende Bearbeitung.

### F08 — P2: Connector-Erfolg wird als Fehler dargestellt

Der sichtbare Test meldet `Test passed for 127.0.0.1. Connector configuration is ready ...` in einem roten Banner mit Fehlericon. Read-only DOM-Abgleich bestätigt CSS-Klasse `mud-alert-outlined-error`.

[Connectors.razor](../../src/VertexBPMN.Studio/Components/Pages/Connectors.razor):164 schreibt auch erfolgreiche Resultate nach `_error`; Zeile 41 stellt jeden solchen Text mit `Severity.Error` dar.

[PersistentConnectorService.cs](../../src/VertexBPMN.Infrastructure/Persistence/Services/PersistentConnectorService.cs):102–112 prüft lediglich Enabled/Endpoint-Host. Der Port 59999 hatte keinen Listener; es fand kein HTTP-Probe-Aufruf statt. Das ist absichtlich eine sichere Konfigurationsprüfung, **kein Beweis einer funktionierenden Verbindung**.

**Konkrete Reparatur:** Erfolg/Fehler getrennt modellieren, Severity aus dem Resultat setzen, Aktion/Ergebnis explizit „Konfiguration prüfen“ nennen. Ein echter Verbindungstest wäre ein eigener Vertrag mit Timeout, SSRF-/Credential-Schutz und kontrolliertem Ziel, nicht eine stille Umdeutung dieses sicheren Tests. Regression: gültige Konfiguration → Success-Severity, Disabled/fehlender Endpoint → klare Validierung; leere Pflichtfelder zeigen Feldhinweise und ProblemDetails statt nur HTTP-Code.

### Weitere lokale Belege / zurückgelassener Zustand

Zusätzliche Screenshots im Ergebnisverzeichnis aus Abschnitt 7: `oidc-admin-tenants.png`, `oidc-admin-tenant-update.png`, `oidc-admin-description-before-update.png`, `oidc-admin-tenant-reset.png`, `oidc-admin-credentials-empty.png`, `oidc-admin-connector-test.png`, `oidc-admin-connector-restored.png`, `oidc-admin-feature-flag-disabled.png`, `oidc-admin-extensions.png`.

Zusätzlich behalten: zwei ausschließlich für diese Prüfung erzeugte Tenants und ein wieder aktivierter Acceptance-Connector. Die beiden Beschreibungslöschungen betrafen nur diese eigenen Testtenants. Keine vorhandenen fremden Tenants/Credentials/Plugins verändert; keine neuen Credential-Fixtures angelegt. `liveinspector` ist wieder true. API/Studio und dedizierte Compose-Infrastruktur bleiben für die Nachprüfung gestartet. Produktcode und CI wurden nicht verändert.

## 9. Nachfolgende Korrekturen (2026-10-01)

Die Befunde F01–F08 wurden anschließend auf `codex/studio-acceptance-fixes`
bearbeitet. Änderungen, tatsächliche Testergebnisse und verbleibende Grenzen:
[Korrektur- und Nachprüfungsbericht](2026-10-01_Studio-Acceptance-Korrekturen.md).
Die Ergebnisse dieses ursprünglichen Berichts bleiben als Vorher-Nachweis
unverändert. Die neuen API-/Studio-Apps laufen tatsächlich in WSLC-Containern;
die früheren nativen Ersatzprozesse wurden beendet, Daten-Volumes erhalten.
