# Codex-Auftrag: VertexBPMN Low-Code Studio (bpmn.io Toolkits) – vollständige, inkrementelle, zero-break Implementierung

## Rolle und Arbeitsweise

Du bist ein Senior C#/.NET-Architekt und arbeitest nach State-of-the-Art-Standards für professionelle, produktionsreife .NET-Software (aktuelles .NET LTS/STS, C# neueste Sprachversion, nullable reference types aktiviert, async/await durchgängig, Dependency Injection, klare Schichtenarchitektur, SOLID-Prinzipien, Clean Code).

Du implementierst den unten stehenden Plan **strikt in der angegebenen Reihenfolge, Phase für Phase, vollständig**. Du überspringst keine Phase, keine Teilaufgabe und kein Akzeptanzkriterium. Du markierst nichts als "TODO" oder "später" – jede Phase muss beim Abschluss lauffähig, getestet und geprüft sein, bevor die nächste Phase beginnt.

### Nicht verhandelbare Arbeitsprinzipien

1. **Zero Break / Zero Regression**
   - Alle bestehenden Unit-, Integration-, Contract- und Smoke-Tests müssen nach jeder Phase weiterhin grün sein.
   - `dotnet test VertexBPMN.sln` muss nach jedem abgeschlossenen Schritt fehlerfrei durchlaufen.
   - Bestehende öffentliche APIs (REST-Endpunkte, SDK-Signaturen, CLI-Befehle) dürfen nicht ohne Backward-Compatibility-Strategie geändert werden. Breaking Changes sind nur zulässig, wenn im Plan explizit vorgesehen, und müssen dokumentiert und in Tests abgesichert werden.
   - Bestehende BPMN/DMN/CMMN/Form-Artefakte müssen nach Änderungen an Parsern/Serializern weiterhin korrekt geladen werden (Roundtrip-Garantie).

2. **Inkrementelles Vorgehen**
   - Jede Phase wird in kleine, in sich abgeschlossene Commits/Schritte zerlegt (z. B. „API-Endpoint hinzufügen", „Service implementieren", „Tests ergänzen", „Doku aktualisieren").
   - Nach jedem Schritt: kompilieren, Tests laufen lassen, Ergebnis kurz zusammenfassen.
   - Kein „Big Bang"-Commit, der mehrere Phasen gleichzeitig verändert.
   - Wenn eine Phase Vorarbeiten aus einer späteren Phase voraussetzt, wird das explizit benannt und die Reihenfolge nur in diesem begründeten Fall minimal angepasst.

3. **Vollständigkeit statt Prototyp**
   - Kein Platzhalter-Code, keine „throw new NotImplementedException()"-Stubs in Produktionscode.
   - Fehlerbehandlung, Validierung, Logging und Security (insbesondere Secret-Handling) sind Teil der Implementierung, nicht optional.
   - Jede Phase liefert genau die in ihren Akzeptanzkriterien beschriebenen Ergebnisse – vollständig, nicht nur „im Prinzip funktionierend".

4. **Tests sind Pflicht, nicht optional**
   - Für jede neue Komponente: Unit-Tests.
   - Für jede neue API: Contract-/Integrationstests.
   - Für jede CLI-Änderung: Smoke-Tests inkl. Help-Text-Prüfung.
   - Für jede UI-Komponente (Studio): Playwright-Smoke-Tests für Rendering, Save/Deploy.
   - Für Secret-/Credential-Handling: explizite Redaction-Tests (keine Klartext-Secrets in Response, Log, Exception, Audit).
   - Für BPMN-Extension-Parsing: strikte Roundtrip-Tests (Import → Edit → Export erhält alle `vertex:*`-Elemente sowie unbekannte Extension Elements).

5. **Strenge Selbstprüfung vor Abschluss jeder Phase**
   Prüfe und bestätige explizit für jede Phase, bevor du zur nächsten übergehst:
   - [ ] Alle Teilaufgaben der Phase sind vollständig umgesetzt.
   - [ ] Alle Akzeptanzkriterien der Phase sind erfüllt und nachweisbar (Test/Log/Screenshot-Beschreibung).
   - [ ] `dotnet test VertexBPMN.sln` ist grün.
   - [ ] Ggf. Studio-UI-Tests und npm build/test sind grün.
   - [ ] Keine Secrets im Klartext in Code, Logs, Responses oder Tests.
   - [ ] Bestehende Funktionalität aus vorherigen Phasen ist unverändert nutzbar (manuell nachvollzogen anhand der jeweiligen Akzeptanzkriterien).
   - [ ] Dokumentation (README/Docs/OpenAPI) ist aktualisiert.

6. **Kommunikation während der Umsetzung**
   - Nach jeder abgeschlossenen Phase: kurze, präzise Zusammenfassung (was wurde gebaut, welche Tests wurden ergänzt, welches Ergebnis hatten sie).
   - Bei Unklarheiten im Plan: eine begründete, konservative Annahme treffen, dokumentieren und fortfahren – keine Verzögerung durch offene Rückfragen, wenn eine sinnvolle Default-Entscheidung möglich ist.
   - Bei echten Blockern (z. B. widersprüchliche Anforderungen, fehlende Abhängigkeiten): explizit benennen, bevor weitergemacht wird.

---

## Kontext: Ausgangslage im Repository

Bereits vorhanden:
- BPMN-Deployment über `POST /api/repository`.
- Persistente Workflow-Trigger über `api/triggers`.
- SDK-Unterstützung für Deployment, Start und Workflow-Trigger.
- Studio-Seiten für BPMN-Upload, BPMN-Modeler, Workflow-Trigger, Connectoren und Credentials.
- Connector-Metadaten mit tenantbezogener API unter `api/connectors`.
- Persistenter Credential-Service mit verschlüsselten Secret-Werten.
- DMN-, CMMN- und form-js-Bibliotheken sind im Studio bereits als statische Assets angelegt.

Bekannte Lücken:
- API-Controller für `api/credentials` fehlt, obwohl Studio bereits einen HTTP-Client dafür nutzt.
- CLI-Befehl `deploy-bpmn` muss gegen Help-Text und Tests geprüft werden.
- BPMN-Modeler ist noch minimal, kein vollständiges Properties Panel.
- Keine Vertex-spezifische BPMN-Moddle-Extension.
- Keine manifestbasierte Connector-Template-Registry.
- Keine generische Connector-Runtime für `vertex:connector`.
- Keine n8n-ähnliche Node-Palette.
- Kein einheitlicher Test-Run mit Mockdaten, Connector-Preview, Runtime-Overlay.
- Kein n8n-Importer.
- SDK/CLI decken Connectoren, Credentials, Templates, Test-Runs noch nicht vollständig ab.

## Eingesetzte bpmn.io Toolkits

| Toolkit | Zweck | Einsatz |
|---|---|---|
| `bpmn-js` | BPMN 2.0 Viewer/Editor | Canvas, Modeler, Viewer, Runtime-Overlay, Custom Palette |
| `bpmn-js-properties-panel` | Technisches Properties Panel | Vertex-Properties, Connector-/Retry-Felder, Multi-Instance |
| `bpmn-js-token-simulation` | Lokale Token-Simulation | Modelltests ohne Engine-Deployment |
| `dmn-js` | DMN Viewer/Editor | Decision Tables, DRD-Viewer, Deploy/Evaluate |
| `form-js` | Formular-Viewer/Builder | User-Task-Formulare, Start-Formulare, Connector-Konfig |
| `cmmn-js` | CMMN Viewer/Editor | Case-Modelle, Stages, Milestones, Case-Plan |

---

## Implementierungsreihenfolge (strikt einzuhalten)

### Phase 0 – Basis stabilisieren (P0)
Ziel: Vorhandene, vorbereitete Funktionen ehrlich nutzbar machen.

1. `CredentialController` für `api/credentials` implementieren:
   - `GET /api/credentials?tenantId=...`
   - `POST /api/credentials`
   - `PUT /api/credentials/{id}`
   - `PUT /api/credentials/{id}/secret`
   - `DELETE /api/credentials/{id}`
   - Rollen: Lesen tenantbezogen, Mutationen Admin-only.
   - Klartextwerte werden **nie** zurückgegeben.
2. CLI `deploy-bpmn` korrigieren:
   - Help-Text: `deploy-bpmn <bpmn-file> [tenant]`.
   - Implementierung und Tests exakt darauf ausrichten.
3. Contract-Tests ergänzen:
   - Credential API.
   - Studio `HttpCredentialService` gegen echte API.
   - CLI-Smoke für `deploy-bpmn`.
   - SDK-Trigger-Lifecycle bleibt unverändert funktionsfähig.

Akzeptanzkriterien:
- Studio-Credentials-Seite funktioniert gegen die echte API.
- `dotnet test` deckt Credential-API und CLI-Help/Deploy ab.
- Keine Secrets in Responses, Logs oder Audit-Details.

### Phase 1 – bpmn.io Modeler Shell modernisieren (P0/P1)
Ziel: Stabile gemeinsame Shell für BPMN, DMN, Formulare, CMMN.

1. Frontend-Bundling für bpmn.io Toolkits standardisieren (vendored Assets oder npm-Build-Pipeline eindeutig festlegen, Versionen dokumentieren und updatebar machen).
2. BPMN-Modeler auf `bpmn-js/lib/Modeler` mit Properties Panel umbauen.
3. BPMN-Viewer auf `bpmn-js/lib/NavigatedViewer` für read-only Szenarien trennen.
4. `dmn-js` sauber anbinden (laden, bearbeiten, speichern/deployen; DRD- und Decision-Table-Tabs).
5. `form-js` anbinden (Form Builder für User-Task-Formulare, Form Viewer für Runtime-Task-Formulare).
6. `cmmn-js` anbinden (Modeler/Viewer, Deploy/Run gegen vorhandene CMMN-Services).

Akzeptanzkriterien:
- Getrennte Modeler-/Viewer-Komponenten für BPMN, DMN, Forms, CMMN.
- Alle Editoren können laden, speichern/exportieren und vorhandene Artefakte deployen.
- Playwright-Smoke-Tests prüfen Rendering und Save/Deploy-Buttons.

### Phase 2 – Vertex BPMN Moddle Extension (P1)
Ziel: Vertex-spezifische Runtime-Metadaten standardkonform im BPMN-XML.

Namespace: `xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0"`

Referenzbeispiel:
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

1. `vertex-bpmn-moddle` JSON Descriptor für `bpmn-js`.
2. Parser-/Serializer-Roundtrip für `vertex:*`.
3. Properties-Panel-Provider für Vertex-Felder.
4. Validation Rules für erforderliche Connector- und Trigger-Felder.
5. Backward Compatibility: unbekannte Extension Elements dürfen nicht verloren gehen.

Akzeptanzkriterien:
- BPMN Import → Edit → Export erhält `vertex:*` vollständig.
- Strict-Roundtrip-Tests für Connector, Webhook, Retry, IO-Mapping, Credentials.
- Properties Panel kann Vertex-Felder bearbeiten.

### Phase 3 – Connector Templates und Node-Palette (P1)
Ziel: n8n-ähnliche Node-Auswahl über manifestbasierte Templates.

Template-Beispiel:
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

1. API für Connector Templates: `GET/POST /api/connector-templates`, `PUT/DELETE /api/connector-templates/{id}`.
2. Studio Node-Palette mit Kategorien: Trigger, HTTP, Database, Messaging, AI, Utility, Control Flow, Forms, Decisions, Cases.
3. Quick-Insert: nach Task direkt passenden nächsten Node einfügen.
4. Template → BPMN Mapping (Trigger→Start Event, HTTP/DB/SaaS→Service Task, Decision→Business Rule Task+DMN Link, User Form→User Task+Form Ref, Case→Call Activity/CMMN Case Task Mapping).

Akzeptanzkriterien:
- Templates erzeugen ohne Codeänderung neue Palette-Einträge.
- Properties Panel rendert Felder aus Template-Manifesten.
- BPMN-XML enthält passende `vertex:*` Extension Elements.

### Phase 4 – Credential- und Connector-Integration (P1)
Ziel: Connectoren referenzieren und testen Credentials sicher.

1. Credential Picker im Properties Panel.
2. Connector-Metadaten mit Template-ID verbinden.
3. `POST /api/connectors/{id}/test` für Testaufrufe.
4. Secret-Auflösung nur serverseitig.
5. Audit für Secret-Zugriffe, Connector-Test und Runtime-Ausführung.
6. Redaction-Regeln für Logs, UI, Exceptions und History.

Akzeptanzkriterien:
- Kein Secret verlässt die API als Klartext.
- Connector-Test zeigt maskierte Inputs/Outputs.
- Tenant-Isolation ist durch API- und Service-Tests belegt.

### Phase 5 – Runtime Connector Execution (P1/P2)
Ziel: `vertex:connector` Service Tasks werden wirklich ausgeführt.

Neue Abstraktionen: `IConnectorRuntime`, `IConnectorExecutor`, `IConnectorRegistry`, `ConnectorExecutionContext`, `ConnectorExecutionResult`, `ConnectorRetryPolicy`, `ConnectorRedactionPolicy`.

Built-in Connectoren für MVP: HTTP Request, Webhook Inbound, Delay/Timer Helper, Email/SMTP oder SendGrid, Database (PostgreSQL/SQL Server/SQLite), Generic Webhook/Slack, AI Connector Wrapper.

Später (nur benennen, nicht in dieser Phase umsetzen): Code Node mit Sandbox, S3/Object Storage, Kafka/RabbitMQ, n8n-kompatible Import-Adapter.

Akzeptanzkriterien:
- BPMN-ServiceTask mit `vertex:connector type="http"` wird durch Runtime ausgeführt.
- Retry, Timeout, Rate Limit, Fehler-Mapping funktionieren.
- Connector-Ausführung schreibt History/Audit ohne Secrets.

### Phase 6 – Webhook Trigger als BPMN-Element (P1)
Ziel: Vorhandene Workflow-Trigger im BPMN-Editor sichtbar und konfigurierbar.

1. Webhook-Trigger-Template für Message Start Event.
2. Properties: Path, Method, Auth Mode, Secret/HMAC Credential Ref, Payload Schema, Correlation Key.
3. Inbound Gateway erweitert: Secret/HMAC prüfen, Payload validieren, Prozess starten oder Message korrelieren.
4. Studio zeigt Test-URL und curl-Beispiel.

Akzeptanzkriterien:
- Webhook-Start kann im BPMN-Editor angelegt werden.
- Deploy registriert/aktualisiert passende Trigger-Konfiguration.
- Externer Invoke startet Prozess mit Variablen.

### Phase 7 – DMN Decision Tables mit `dmn-js` (P2)
1. DMN-Modeler als eigenständige Studio-Seite stabilisieren.
2. Decision-Table-Editor mit Deploy/Evaluate-Workflow.
3. BPMN Business Rule Task Properties: `decisionRef`, Binding/Version, Input Mapping, Output Mapping.
4. Decision Picker im BPMN Properties Panel.
5. Runtime-Verknüpfung zwischen Business Rule Task und DMN Engine.

Akzeptanzkriterien:
- Decision Table erstellbar, deploybar, aus BPMN heraus ausführbar.
- Studio zeigt Decision-Auswertung mit Testdaten.
- SDK/API dokumentieren Deploy/Evaluate.

### Phase 8 – Formulare mit `form-js` (P2)
1. Form Builder für JSON-basierte Formulare.
2. Form Registry: `GET/POST /api/forms`, `PUT/DELETE /api/forms/{id}`.
3. BPMN User Task Properties: `formRef`, `formVersion`, Assignee/Candidate Groups.
4. Runtime Form Viewer für Task Completion.
5. Form Submit → Task Complete mit Variablen-Mapping.

Akzeptanzkriterien:
- User Task kann Formular referenzieren.
- Studio rendert Formular beim Bearbeiten einer Task.
- Formularwerte werden als Prozessvariablen übergeben.

### Phase 9 – Case-Modelle mit `cmmn-js` (P2/P3)
1. CMMN Modeler/Viewer in Studio konsolidieren.
2. CMMN Deploy/Start/User Event Flow.
3. BPMN Call Activity oder Case Task Mapping für CMMN Cases.
4. Case Runtime Viewer: Stages, Milestones, Plan Items, User Events.
5. Case History Overlay.

Akzeptanzkriterien:
- CMMN-Modell editierbar, deploybar, startbar im Studio.
- Case-Status visuell nachvollziehbar.
- BPMN kann optional CMMN Case-Ausführung referenzieren.

### Phase 10 – n8n-ähnliche UX (P2)
Features: Suchdialog „Add Node", Kategorien/Icons, Quick-Insert zwischen Sequence Flows, vorgefertigte Patterns (HTTP mit Retry; Webhook→IF→HTTP; Cron→Batch→DB; User Approval mit Form; Decision Table Routing; Case Start aus BPMN), Validation Sidebar, XML Preview, Diff/Version Compare, Import/Export.

Mapping-Tabelle n8n→BPMN ist einzuhalten:
Trigger Node→Start/Message/Timer Start; HTTP Node→Service Task+`vertex:connector`; IF Node→Exclusive Gateway; Wait Node→Timer Catch Event; User Form→User Task+`formRef`; Decision→Business Rule Task+`decisionRef`; Subworkflow→Call Activity; Case→CMMN Case Model/Case Task Mapping; Error Workflow→Boundary Error Event/Event Subprocess; Batch→Multi-Instance Task.

Akzeptanzkriterien:
- Low-Code-Nutzer können HTTP/Webhook/Form/Decision-Prozess ohne XML-Wissen erstellen.
- Ergebnis bleibt valides BPMN/DMN/Form/CMMN-Artefakt.
- Import/Export funktioniert roundtrip-stabil.

### Phase 11 – Test Runner, Simulation, Visual Debug (P2)
1. Lokale BPMN-Simulation mit `bpmn-js-token-simulation`.
2. Engine-Test-Run: Deploy Test Version, Start mit Testvariablen, Mock Connector Responses optional.
3. Runtime Overlay: aktiv, abgeschlossen, fehlgeschlagen, wartend, retry.
4. Timeline: Tokens, Tasks, Connector Calls, Decisions, Forms, Case Events.
5. Replay aus History.

Akzeptanzkriterien:
- Prozess lokal simulierbar und danach real gegen VertexBPMN testbar.
- Runtime Overlay nutzt persistente Execution Tokens und History Events.
- Fehler sind direkt am Diagrammelement sichtbar.

### Phase 12 – API, SDK, CLI komplettieren (P2)
API ergänzen um: Credentials, Connector Templates, Connector Test Invoke, Form Registry, DMN Decision Deploy/Evaluate Review, CMMN Case Model Deploy/Start, Model Validation, Test Run, Runtime Trace.

SDK ergänzen um: `CreateCredentialAsync`, `RotateCredentialSecretAsync`, `CreateConnectorAsync`, `ListConnectorTemplatesAsync`, `ValidateBpmnAsync`, `DeployDmnAsync`, `EvaluateDecisionAsync`, `CreateFormAsync`, `StartTestRunAsync`.

CLI ergänzen um:
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
- SDK-NuGet enthält neue Clients und Models.
- CLI-Help ist durch Tests gegen Implementierung abgesichert.

### Phase 13 – n8n Importer (P3)
1. n8n-JSON-Parser.
2. Mapping: Nodes→BPMN Elements, Connections→Sequence Flows, Credentials→Credential Ref Platzhalter, Expressions→Vertex Expression Syntax.
3. Import Report: migrated / needs review / unsupported.
4. Studio Import Wizard.

Akzeptanzkriterien:
- Einfache n8n HTTP/Webhook/IF-Workflows werden importiert.
- Nicht unterstützte Nodes werden sichtbar markiert.
- Import erzeugt gültiges BPMN plus Vertex Extensions.

---

## Verbindliche Teststrategie (über alle Phasen hinweg)

Pflichttests:
- Parser-Roundtrip für `vertex:*`.
- bpmn.io Import/Export Smoke Tests.
- Playwright-UI-Tests für BPMN, DMN, Forms, CMMN.
- API-Contract-Tests für Credentials, Templates, Connectors, Forms.
- SDK-Integrationstests.
- CLI-Smoke-Tests.
- Connector-Runtime-Tests.
- Secret-Redaction-Tests.
- Webhook-HMAC-Tests.
- Runtime-Overlay-Tests.

CI-Gates, die nach jeder Phase grün sein müssen:
- `dotnet test VertexBPMN.sln`
- Studio-UI-Tests
- npm build/test für gebündelte bpmn.io Assets, falls npm-Pipeline eingeführt wird
- OpenAPI Snapshot/Diff für neue APIs

---

## Ablaufsteuerung für Codex

Arbeite die Phasen 0 bis 13 **exakt in dieser Reihenfolge** ab. Für **jede** Phase:

1. Kurzer Plan: Welche Dateien/Komponenten werden angefasst, welche neuen Komponenten entstehen.
2. Implementierung in kleinen, nachvollziehbaren Schritten.
3. Tests schreiben/erweitern (siehe Teststrategie) – neue Tests müssen die neuen Akzeptanzkriterien tatsächlich prüfen, keine Alibi-Tests.
4. `dotnet test VertexBPMN.sln` (und ggf. UI-/npm-Tests) ausführen und Ergebnis zeigen.
5. Selbstprüfung anhand der Checkliste unter „Strenge Selbstprüfung vor Abschluss jeder Phase" durchführen und explizit bestätigen.
6. Kurze Zusammenfassung der Phase inkl. offener Punkte (falls vorhanden) und expliziter Bestätigung „Zero Break: bestehende Tests weiterhin grün".
7. Erst danach zur nächsten Phase übergehen.

Falls im Rahmen einer Phase eine Entscheidung zwischen mehreren technisch sinnvollen Optionen zu treffen ist (z. B. Bundling-Strategie in Phase 1, konkrete Datenbank-Provider in Phase 5), wähle die Option, die am wenigsten Risiko für bestehenden Code birgt, dokumentiere die Entscheidung kurz und fahre fort, statt nachzufragen.

Beginne jetzt mit **Phase 0** gemäß den obigen Vorgaben.