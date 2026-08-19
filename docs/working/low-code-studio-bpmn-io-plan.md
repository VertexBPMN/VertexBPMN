# VertexBPMN Low-Code Studio mit bpmn.io Toolkits

Status: In Umsetzung
Letzte Aktualisierung: 2026-08-20
Ausgangspunkt: `docs/archive/n8n-bpmn-camunda-parity.md`, `docs/archive/n8n-like-gui-for-vertex.md`

## Ziel

VertexBPMN Studio soll von einem einfachen BPMN-Modeler zu einer n8n-aehnlichen Low-Code-Oberflaeche ausgebaut werden. BPMN, DMN, CMMN und Formulare bleiben dabei echte, versionierbare Standards beziehungsweise offene Artefakte. Die UI nutzt die freigegebenen bpmn.io Toolkits fuer Viewer und Editoren; VertexBPMN ergaenzt Runtime, Connectoren, Credentials, Trigger, Tests, SDK, CLI und API.

## bpmn.io Toolkits

| Toolkit | Zweck in VertexBPMN Studio | Einsatz |
|---|---|---|
| `bpmn-js` | BPMN 2.0 Viewer und Editor | BPMN-Canvas, Modeler, Viewer, Runtime-Overlay, Custom Palette |
| `bpmn-js-properties-panel` | Technisches Properties Panel fuer BPMN | Vertex-spezifische Properties, Connector- und Retry-Felder, Multi-Instance-Konfiguration |
| `bpmn-js-token-simulation` | Lokale BPMN-Token-Simulation | Schnelle Modelltests ohne Engine-Deployment |
| `dmn-js` | DMN Viewer und Editor fuer Decision Tables | Decision-Table-Editor, DRD-Viewer, DMN Deploy/Evaluate Flow |
| `form-js` | Formular-Viewer und Formular-Builder | User-Task-Formulare, Start-Formulare, Connector-Konfigurationsformulare |
| `cmmn-js` | CMMN Viewer und Editor | Case-Modelle, Stages, Milestones, Case-Plan-Visualisierung |

Quellen fuer die Toolkit-Auswahl:

- https://bpmn.io/toolkit/bpmn-js/
- https://github.com/bpmn-io/bpmn-js-properties-panel
- https://github.com/bpmn-io/bpmn-js-token-simulation
- https://bpmn.io/toolkit/dmn-js/
- https://bpmn.io/toolkit/form-js/
- https://bpmn.io/toolkit/cmmn-js/

## Aktueller Stand im Repository

Bereits vorhanden:

- BPMN-Deployment ueber API `POST /api/repository`.
- Persistente Workflow-Trigger ueber `api/triggers`.
- SDK-Unterstuetzung fuer Deployment, Start und Workflow-Trigger.
- Studio-Seiten fuer BPMN-Upload, BPMN-Modeler, Workflow-Trigger, Connectoren und Credentials.
- Connector-Metadaten mit tenantbezogener API unter `api/connectors`.
- Persistenter Credential-Service mit verschluesselten Secret-Werten.
- DMN-, CMMN- und form-js-Bibliotheken sind im Studio bereits als statische Assets angelegt.

Noch unvollstaendig oder fehlend:

- API-Controller fuer `api/credentials` fehlt, obwohl Studio bereits einen HTTP-Client dafuer nutzt.
- CLI-Befehl `deploy-bpmn` muss gegen Help-Text und Tests geprueft werden.
- BPMN-Modeler ist noch minimal und nutzt kein vollstaendiges Properties Panel.
- Keine Vertex-spezifische BPMN-Moddle-Extension fuer Connectoren, Credentials, Retry, IO-Mapping und Webhooks.
- Keine manifestbasierte Connector-Template-Registry.
- Keine echte generische Connector-Runtime fuer `vertex:connector`.
- Keine n8n-aehnliche Node-Palette mit Kategorien, Icons, Quick-Insert und Template-Forms.
- Kein einheitlicher Test-Run mit Mockdaten, Connector-Preview und Runtime-Overlay.
- Kein n8n-Importer.
- SDK und CLI decken Connectoren, Credentials, Templates und Test-Runs noch nicht voll ab.

## Architekturziel

```text
VertexBPMN Studio
├─ Modeler Shell
│  ├─ BPMN Editor/Viewer via bpmn-js
│  ├─ DMN Editor/Viewer via dmn-js
│  ├─ Form Builder/Viewer via form-js
│  └─ CMMN Editor/Viewer via cmmn-js
├─ Low-Code Layer
│  ├─ Connector Palette
│  ├─ Properties Panel
│  ├─ Element Templates
│  ├─ Credential Picker
│  └─ Validation Panel
├─ Runtime Layer
│  ├─ Deploy
│  ├─ Start/Test Run
│  ├─ Webhook/Trigger Gateway
│  ├─ Connector Runtime
│  └─ Runtime Overlay
└─ Platform APIs
   ├─ Repository
   ├─ Triggers
   ├─ Credentials
   ├─ Connector Templates
   ├─ Connector Instances
   ├─ Forms
   ├─ DMN Decisions
   └─ CMMN Cases
```

## Phase 0: Basis stabilisieren

Prioritaet: P0  
Ziel: Vorhandene vorbereitete Funktionen ehrlich nutzbar machen.

Aufgaben:

1. `CredentialController` fuer `api/credentials` implementieren.
   - `GET /api/credentials?tenantId=...`
   - `POST /api/credentials`
   - `PUT /api/credentials/{id}`
   - `PUT /api/credentials/{id}/secret`
   - `DELETE /api/credentials/{id}`
   - Rollen: Lesen tenantbezogen, Mutationen Admin-only.
   - Klartextwerte werden nie zurueckgegeben.
2. CLI `deploy-bpmn` korrigieren.
   - Help-Text: `deploy-bpmn <bpmn-file> [tenant]`.
   - Implementierung und Tests darauf ausrichten.
3. Contract-Tests ergaenzen.
   - Credential API.
   - Studio `HttpCredentialService` gegen echte API.
   - CLI Smoke fuer `deploy-bpmn`.
   - SDK-Trigger-Lifecycle bleibt erhalten.

Akzeptanzkriterien:

- Studio Credentials-Seite funktioniert gegen die echte API.
- `dotnet test` deckt Credential API und CLI-Help/Deploy ab.
- Keine Secrets in Responses, Logs oder Audit-Details.

## Phase 1: bpmn.io Modeler Shell modernisieren

Prioritaet: P0/P1  
Status: Baseline implementiert; npm-basierte, gepinnte bpmn.io Asset-Pipeline umgesetzt.  
Ziel: Eine stabile gemeinsame Shell fuer BPMN, DMN, Formulare und CMMN.

Implementierungsnotiz: siehe `../reference/bpmn-io-studio-shell.md`.

Aufgaben:

1. Frontend-Bundling fuer bpmn.io Toolkits standardisieren.
   - Lokale vendored Assets oder npm-build Pipeline eindeutig festlegen.
   - Versionen dokumentieren und updatebar machen.
2. BPMN-Modeler auf `bpmn-js/lib/Modeler` mit Properties Panel umbauen.
3. BPMN-Viewer auf `bpmn-js/lib/NavigatedViewer` fuer read-only Szenarien trennen.
4. `dmn-js` fuer Decision Tables sauber anbinden.
   - DMN laden, bearbeiten, speichern/deployen.
   - DRD und Decision Table Tabs unterstuetzen.
5. `form-js` fuer Formulare anbinden.
   - Form Builder fuer User-Task-Formulare.
   - Form Viewer fuer Runtime-Task-Formulare.
6. `cmmn-js` fuer Case-Modelle anbinden.
   - CMMN Modeler und Viewer.
   - Deploy/Run gegen vorhandene CMMN-Services.

Akzeptanzkriterien:

- Studio hat getrennte Modeler/Viewer-Komponenten fuer BPMN, DMN, Forms und CMMN.
- Alle Editoren koennen laden, speichern/exportieren und vorhandene Artefakte deployen.
- Playwright-Smoke-Tests pruefen Rendering und Save/Deploy-Buttons.

## Phase 2: Vertex BPMN Moddle Extension

Prioritaet: P1  
Ziel: Vertex-spezifische Runtime-Metadaten standardkonform im BPMN-XML speichern.

Namespace:

```xml
xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0"
```

Beispiel:

```xml
<bpmn:serviceTask id="Task_CallApi" name="Call API">
  <bpmn:extensionElements>
    <vertex:connector
      type="http"
      operationId="http.request"
      credentialRef="cred-orders-api"
      timeoutMs="30000" />
    <vertex:retryPolicy
      maxAttempts="5"
      strategy="exponential"
      baseDelayMs="1000"
      retryOn="429,5xx" />
    <vertex:ioMapping>
      <vertex:input name="url" expression="${orderApiUrl}" />
      <vertex:output name="response" target="httpResponse" />
    </vertex:ioMapping>
  </bpmn:extensionElements>
</bpmn:serviceTask>
```

Aufgaben:

1. `vertex-bpmn-moddle` JSON Descriptor fuer `bpmn-js`.
2. Parser-/Serializer-Roundtrip fuer `vertex:*`.
3. Properties-Panel Provider fuer Vertex-Felder.
4. Validation Rules fuer erforderliche Connector- und Trigger-Felder.
5. Backward Compatibility: unbekannte Extension Elements duerfen nicht verloren gehen.

Akzeptanzkriterien:

- BPMN Import -> Edit -> Export erhaelt `vertex:*`.
- Strict Roundtrip Tests enthalten Connector, Webhook, Retry, IO-Mapping und Credentials.
- Properties Panel kann Vertex-Felder bearbeiten.

## Phase 3: Connector Templates und Node-Palette

Prioritaet: P1  
Ziel: n8n-aehnliche Node-Auswahl ueber manifestbasierte Templates.

Connector-Template-Beispiel:

```json
{
  "id": "http.request",
  "name": "HTTP Request",
  "category": "Core",
  "appliesTo": ["bpmn:ServiceTask"],
  "runtime": "http",
  "icon": "http",
  "properties": [
    { "key": "method", "type": "select", "options": ["GET", "POST", "PUT", "PATCH", "DELETE"], "default": "GET" },
    { "key": "url", "type": "expression", "required": true },
    { "key": "credentialRef", "type": "credential" },
    { "key": "timeoutMs", "type": "number", "default": 30000 }
  ]
}
```

Aufgaben:

1. API fuer Connector Templates:
   - `GET /api/connector-templates`
   - `POST /api/connector-templates`
   - `PUT /api/connector-templates/{id}`
   - `DELETE /api/connector-templates/{id}`
2. Studio Node-Palette:
   - Trigger
   - HTTP
   - Database
   - Messaging
   - AI
   - Utility
   - Control Flow
   - Forms
   - Decisions
   - Cases
3. Quick-Insert:
   - Nach Task direkt passenden naechsten Node einfuegen.
4. Template -> BPMN Mapping:
   - Trigger -> Start Event.
   - HTTP/DB/SaaS -> Service Task.
   - Decision -> Business Rule Task + DMN Link.
   - User Form -> User Task + Form Ref.
   - Case -> Call Activity oder CMMN Case Task Mapping.

Akzeptanzkriterien:

- Templates koennen ohne Codeaenderung neue Palette-Eintraege erzeugen.
- Properties Panel rendert Felder aus Template-Manifesten.
- BPMN-XML enthaelt die passenden `vertex:*` Extension Elements.

## Phase 4: Credential- und Connector-Integration

Prioritaet: P1  
Ziel: Connectoren koennen sicher Credentials referenzieren und testen.

Aufgaben:

1. Credential Picker im Properties Panel.
2. Connector-Metadaten mit Template-ID verbinden.
3. `POST /api/connectors/{id}/test` fuer Testaufrufe.
4. Secret-Aufloesung nur serverseitig.
5. Audit fuer Secret-Zugriffe, Connector-Test und Runtime-Ausfuehrung.
6. Redaction-Regeln fuer Logs, UI, Exceptions und History.

Akzeptanzkriterien:

- Kein Secret verlaesst die API als Klartext.
- Connector-Test zeigt maskierte Inputs/Outputs.
- Tenant-Isolation wird durch API- und Service-Tests belegt.

## Phase 5: Runtime Connector Execution

Prioritaet: P1/P2  
Ziel: `vertex:connector` Service Tasks werden wirklich ausgefuehrt.

Neue Abstraktionen:

- `IConnectorRuntime`
- `IConnectorExecutor`
- `IConnectorRegistry`
- `ConnectorExecutionContext`
- `ConnectorExecutionResult`
- `ConnectorRetryPolicy`
- `ConnectorRedactionPolicy`

Built-in Connectoren fuer MVP:

1. HTTP Request
2. Webhook Inbound
3. Delay/Timer Helper
4. Email/SMTP oder SendGrid
5. Database: PostgreSQL/SQL Server/SQLite
6. Generic Webhook/Slack
7. AI Connector Wrapper

Spaeter:

- Code Node mit Sandbox.
- S3/Object Storage.
- Kafka/RabbitMQ.
- n8n-kompatible Import-Adapter.

Akzeptanzkriterien:

- BPMN-ServiceTask mit `vertex:connector type="http"` wird durch Runtime ausgefuehrt.
- Retry, Timeout, Rate Limit und Fehler-Mapping funktionieren.
- Connector-Ausfuehrung schreibt History/Audit ohne Secrets.

## Phase 6: Webhook Trigger als BPMN-Element

Prioritaet: P1  
Ziel: Vorhandene Workflow-Trigger werden im BPMN-Editor sichtbar und konfigurierbar.

Aufgaben:

1. Webhook Trigger Template fuer Message Start Event.
2. Properties:
   - Path
   - Method
   - Auth Mode
   - Secret/HMAC Credential Ref
   - Payload Schema
   - Correlation Key
3. Inbound Gateway erweitert:
   - Secret/HMAC pruefen.
   - Payload validieren.
   - Prozess starten oder Message korrelieren.
4. Studio zeigt Test-URL und curl-Beispiel.

Akzeptanzkriterien:

- Webhook-Start kann im BPMN-Editor angelegt werden.
- Deploy registriert oder aktualisiert passende Trigger-Konfiguration.
- Externer Invoke startet Prozess mit Variablen.

## Phase 7: DMN Decision Tables mit `dmn-js`

Prioritaet: P2  
Ziel: Entscheidungen visuell modellieren und in BPMN-Prozesse einbinden.

Aufgaben:

1. DMN Modeler als eigenstaendige Studio-Seite stabilisieren.
2. Decision Table Editor mit Deploy/Evaluate Workflow.
3. BPMN Business Rule Task Properties:
   - `decisionRef`
   - Binding/Version
   - Input Mapping
   - Output Mapping
4. Decision Picker im BPMN Properties Panel.
5. Runtime-Verknuepfung zwischen Business Rule Task und DMN Engine.

Akzeptanzkriterien:

- Eine DMN Decision Table kann erstellt, deployed und aus BPMN heraus ausgefuehrt werden.
- Studio kann Decision-Auswertung mit Testdaten anzeigen.
- SDK/API dokumentieren Decision Deploy/Evaluate.

## Phase 8: Formulare mit `form-js`

Prioritaet: P2  
Ziel: User Tasks und Start Forms visuell erstellen und zur Runtime nutzen.

Aufgaben:

1. Form Builder fuer JSON-basierte Formulare.
2. Form Registry:
   - `GET /api/forms`
   - `POST /api/forms`
   - `PUT /api/forms/{id}`
   - `DELETE /api/forms/{id}`
3. BPMN User Task Properties:
   - `formRef`
   - `formVersion`
   - Assignee/Candidate Groups
4. Runtime Form Viewer fuer Task Completion.
5. Form Submit -> Task Complete mit Variablen-Mapping.

Akzeptanzkriterien:

- User Task kann ein Formular referenzieren.
- Studio rendert Formular beim Bearbeiten einer Task.
- Formularwerte werden als Prozessvariablen uebergeben.

## Phase 9: Case-Modelle mit `cmmn-js`

Prioritaet: P2/P3  
Ziel: CMMN-Modelle visuell bearbeiten und mit BPMN/DMN verbinden.

Aufgaben:

1. CMMN Modeler/View in Studio konsolidieren.
2. CMMN Deploy/Start/User Event Flow.
3. BPMN Call Activity oder Case Task Mapping fuer CMMN Cases.
4. Case Runtime Viewer:
   - Stages
   - Milestones
   - Plan Items
   - User Events
5. Case History Overlay.

Akzeptanzkriterien:

- CMMN-Modell kann in Studio bearbeitet, deployed und gestartet werden.
- Case-Status ist visuell nachvollziehbar.
- BPMN kann optional CMMN Case-Ausfuehrung referenzieren.

## Phase 10: n8n-aehnliche UX

Prioritaet: P2  
Ziel: Die UI fuehlt sich fuer Low-Code-Nutzer wie ein Node-Workflow-Editor an, bleibt aber BPMN-korrekt.

Features:

- Suchdialog "Add Node".
- Kategorien und Icons.
- Quick-Insert zwischen Sequence Flows.
- Vorgefertigte Patterns:
  - HTTP mit Retry
  - Webhook -> IF -> HTTP
  - Cron -> Batch -> DB
  - User Approval mit Form
  - Decision Table Routing
  - Case Start aus BPMN
- Validation Sidebar.
- XML Preview.
- Diff/Version Compare.
- Import/Export.

Mapping:

| n8n-Konzept | VertexBPMN/BPMN Mapping |
|---|---|
| Trigger Node | Start Event, Message Start, Timer Start |
| HTTP Node | Service Task + `vertex:connector` |
| IF Node | Exclusive Gateway |
| Wait Node | Timer Catch Event |
| User Form | User Task + `formRef` |
| Decision | Business Rule Task + `decisionRef` |
| Subworkflow | Call Activity |
| Case | CMMN Case Model oder Case Task Mapping |
| Error Workflow | Boundary Error Event oder Event Subprocess |
| Batch | Multi-Instance Task |

Akzeptanzkriterien:

- Low-Code-Nutzer koennen einen HTTP/Webhook/Form/Decision-Prozess ohne XML-Wissen erstellen.
- Ergebnis bleibt valides BPMN/DMN/Form/CMMN-Artefakt.
- Import/Export funktioniert roundtrip-stabil.

## Phase 11: Test Runner, Simulation und Visual Debug

Prioritaet: P2  
Ziel: Modell- und Runtime-Verhalten im Studio nachvollziehbar machen.

Aufgaben:

1. Lokale BPMN-Simulation mit `bpmn-js-token-simulation`.
2. Engine-Test-Run:
   - Deploy Test Version.
   - Start mit Testvariablen.
   - Mock Connector Responses optional.
3. Runtime Overlay:
   - aktiv
   - abgeschlossen
   - fehlgeschlagen
   - wartend
   - retry
4. Timeline:
   - Tokens
   - Tasks
   - Connector Calls
   - Decisions
   - Forms
   - Case Events
5. Replay aus History.

Akzeptanzkriterien:

- Ein Prozess kann lokal simuliert und danach echt gegen VertexBPMN getestet werden.
- Runtime Overlay nutzt persistente Execution Tokens und History Events.
- Fehler sind direkt am Diagrammelement sichtbar.

Umsetzungsstand (2026-08-19): abgeschlossen.

- Der BPMN-Modeler bindet das gepinnte Paket `bpmn-js-token-simulation` lokal ein; Start, Pause und Reset sind direkt im Modeler verfügbar.
- Ein Engine-Testlauf deployt die aktuelle BPMN-Version, akzeptiert JSON-Testvariablen und startet eine isolierte Prozessinstanz.
- Die Debug-Ansicht legt aktive, abgeschlossene, fehlgeschlagene, wartende und Retry-Zustände als Diagramm-Markierungen über das persistierte Laufzeitbild. Ereignisse aus dem Execution Trace ergänzen Fehler-, Warte- und Retry-Zustände.
- Die Timeline zeigt die persistierten Ablaufereignisse; ein Klick hebt das zugehörige BPMN-Element als Replay-Schritt hervor. Damit werden Token-, Task-, Connector-, Decision-, Form- und Case-Ereignisse einheitlich dargestellt, sofern sie im Trace vorliegen.

## Phase 12: API, SDK und CLI komplettieren

Prioritaet: P2  
Ziel: Jede Studio-Funktion ist auch automatisierbar.

API ergaenzen:

- Credentials
- Connector Templates
- Connector Test Invoke
- Form Registry
- DMN Decision Deploy/Evaluate Review
- CMMN Case Model Deploy/Start
- Model Validation
- Test Run
- Runtime Trace

SDK ergaenzen:

- `CreateCredentialAsync`
- `RotateCredentialSecretAsync`
- `CreateConnectorAsync`
- `ListConnectorTemplatesAsync`
- `ValidateBpmnAsync`
- `DeployDmnAsync`
- `EvaluateDecisionAsync`
- `CreateFormAsync`
- `StartTestRunAsync`

CLI ergaenzen:

```bash
vertexbpmn credential create ...
vertexbpmn connector list
vertexbpmn connector create ...
vertexbpmn template list
vertexbpmn validate model.bpmn
vertexbpmn deploy-bpmn model.bpmn tenant-a
vertexbpmn deploy-dmn decision.dmn tenant-a
vertexbpmn deploy-form form.json tenant-a
vertexbpmn trigger create ...
vertexbpmn test-run model.bpmn variables.json
```

Akzeptanzkriterien:

- API, CLI, SDK und Studio sind funktional gleichwertig.
- SDK NuGet enthaelt die neuen Clients und Models.
- CLI-Help ist durch Tests gegen Implementierung abgesichert.

Umsetzungsstand (2026-08-20): abgeschlossen.

- API: Credentials, Connectoren/Templates, Forms, DMN, BPMN-Validierung und Runtime-Trace sind als Verträge verfügbar. `POST /api/test-runs` führt Deployment und Prozessstart als einen Testlauf aus.
- CMMN: `api/case-definitions` speichert validierte Case-Modelle tenant-spezifisch in der BPMN-Datenbank und bietet Deploy, Read und Start. Migration `20260820003000_AddCaseDefinitions` gehört zum Rollout.
- SDK: `VertexBpmnClient` enthält die Phase-12-Clients und typisierten Modelle, einschließlich `DeployCmmnAsync`, `StartCaseAsync` und `StartTestRunAsync`.
- CLI: Credentials, Connectors, Templates, Validierung, DMN-/Form-Deployment und Test-Runs sind verfügbar; der optionale Tenant fällt lokal auf `default` zurück.

## Phase 13: n8n Importer

Prioritaet: P3  
Ziel: n8n-Workflows teilweise automatisiert nach BPMN/VertexBPMN migrieren.

Aufgaben:

1. n8n JSON Parser.
2. Mapping:
   - Nodes -> BPMN Elements.
   - Connections -> Sequence Flows.
   - Credentials -> Credential Ref Platzhalter.
   - Expressions -> Vertex Expression Syntax.
3. Import Report:
   - migrated
   - needs review
   - unsupported
4. Studio Import Wizard.

Akzeptanzkriterien:

- Einfache n8n HTTP/Webhook/IF Workflows werden importiert.
- Nicht unterstuetzte Nodes werden sichtbar markiert.
- Import erzeugt gueltiges BPMN plus Vertex Extensions.

Umsetzungsstand (2026-08-20): abgeschlossen.

- Der MVP importiert n8n-JSON über `POST /api/import/n8n`, das SDK und `vertexbpmn import-n8n`.
- Webhook, HTTP Request und IF werden nach BPMN abgebildet. Credentials und IF-Ausdrücke erhalten sichere Review-Platzhalter, statt Secrets oder unübersetzte Ausdrücke zu übernehmen.
- Nicht unterstützte Nodes bleiben als markierte BPMN-Service-Tasks erhalten und erscheinen im strukturierten Importbericht.
- Der BPMN-Modeler bietet einen n8n-Dateiimport und zeigt den Bericht direkt nach dem Laden an.
- Die Implementierung ist für Application, API, SDK, CLI und Studio erfolgreich kompiliert. Der vollständige lokale Test-Host benötigt eine Bereinigung vorhandener, ungetrackter `bin\\Debug`-Artefakte; diese werden nicht durch die Produktimplementierung verändert oder entfernt.

## Teststrategie

Pflichttests:

- Parser Roundtrip fuer `vertex:*`.
- bpmn.io Import/Export Smoke Tests.
- Playwright UI Tests fuer BPMN, DMN, Forms, CMMN.
- API Contract Tests fuer Credentials, Templates, Connectors, Forms.
- SDK Integration Tests.
- CLI Smoke Tests.
- Connector Runtime Tests.
- Secret Redaction Tests.
- Webhook HMAC Tests.
- Runtime Overlay Tests.

CI-Gates:

- `dotnet test VertexBPMN.sln`
- Studio UI Tests
- npm build/test fuer gebuendelte bpmn.io Assets, falls npm Pipeline eingefuehrt wird
- OpenAPI Snapshot/Diff fuer neue APIs

## Empfohlene Implementierungsreihenfolge

1. Credential API fertigstellen.
2. CLI `deploy-bpmn` korrigieren.
3. bpmn.io Modeler Shell modernisieren.
4. Properties Panel integrieren.
5. Vertex Moddle Extension einfuehren.
6. Connector Template Registry bauen.
7. Node Palette und Template-Forms generieren.
8. HTTP Connector Runtime implementieren.
9. Webhook Trigger ins Diagramm integrieren.
10. `dmn-js` Decision Tables mit Business Rule Tasks verbinden.
11. `form-js` Formulare mit User Tasks verbinden.
12. `cmmn-js` Case-Modelle konsolidieren.
13. Test Run und Runtime Overlay bauen.
14. SDK/CLI/API vervollstaendigen.
15. n8n Importer als spaetere Phase.

## Grobe Aufwandsschaetzung

| Block | Aufwand |
|---|---:|
| Basis stabilisieren | 2-4 Tage |
| bpmn.io Modeler Shell | 1-2 Wochen |
| Vertex Moddle + Roundtrip | 1 Woche |
| Connector Templates + Palette | 1-2 Wochen |
| Runtime Connector HTTP/Webhook/Credentials | 2-3 Wochen |
| DMN mit `dmn-js` | 1-2 Wochen |
| Forms mit `form-js` | 1-2 Wochen |
| CMMN mit `cmmn-js` | 1-2 Wochen |
| Test Runner + Runtime Overlay | 2-3 Wochen |
| SDK/CLI/API Vervollstaendigung | 1-2 Wochen |
| n8n Importer MVP | 2-4 Wochen |

MVP realistisch: 6-8 Wochen.  
Produktionsreifer Ausbau: 3-4 Monate.

## Naechster konkreter Block

Empfohlen als erster Implementierungsblock:

1. `CredentialController` implementieren.
2. CLI `deploy-bpmn` korrigieren.
3. Tests fuer API, Studio-Client und CLI ergaenzen.
4. Dokumentation fuer Credentials/Connector-Grundlage aktualisieren.

Warum zuerst: Diese Basis ist klein, risikobegrenzt und macht die bereits vorhandenen Studio-Seiten fuer Credentials und Connectoren tatsaechlich nutzbar.
