# Workflow-Triggers

Ein registrierter Trigger startet eine bereits deployte BPMN-Prozessdefinition. Trigger sind tenantbezogen, werden dauerhaft gespeichert und können deaktiviert oder gelöscht werden.

## REST API

Die Verwaltung erfordert Authentifizierung und die Rolle `ProcessManager` (oder `Admin`):

- `GET /api/triggers?tenantId=<tenant>` – Trigger auflisten
- `POST /api/triggers` – Trigger registrieren
- `PUT /api/triggers/{id}?tenantId=<tenant>` – Name oder Aktivierungsstatus ändern
- `DELETE /api/triggers/{id}?tenantId=<tenant>` – Trigger löschen

Beispiel für die Registrierung:

```http
POST /api/triggers
Content-Type: application/json

{
  "name": "Order webhook",
  "processDefinitionKey": "order-process",
  "tenantId": "acme"
}
```

Die Antwort enthält das Secret genau einmal. Es wird nicht gespeichert, sondern nur als Hash abgelegt.

Der externe Aufruf ist anonym erreichbar, benötigt aber das Secret im Header:

```http
POST /api/triggers/{id}/invoke
X-VertexBPMN-Trigger-Secret: <secret>
Content-Type: application/json

{
  "businessKey": "ORDER-123",
  "variables": {
    "customerId": "C-42"
  }
}
```

Bei Erfolg wird `201 Created` mit der gestarteten Prozessinstanz zurückgegeben. Ein falsches Secret liefert `401`; ein deaktivierter oder unbekannter Trigger liefert `404`.

## Persistentes BPMN-Deployment

Ein BPMN-Workflow kann vor der Trigger-Registrierung dauerhaft bereitgestellt werden:

    deploy-bpmn <bpmn-file> [tenant]

Im SDK steht dafür DeployProcessAsync(bpmnXml, name, tenantId) zur Verfügung. Die API verwendet POST /api/repository; Studio nutzt die vorhandene Upload-Seite Deployments.


## Webhooks aus dem BPMN-Modell

Ein Start-Event mit `vertex:webhook` (Path, Method, Auth Mode, ggf. HMAC-Credential-Ref,
Payload-Schema, Correlation Key) wird bei jedem Deploy
(`POST /api/repository` -> `SynchronizeBpmnWebhooksAsync`) als Trigger registriert bzw. aktualisiert.
Der externe Aufruf laeuft ueber den eingebauten Ingress statt ueber `/api/triggers/{id}/invoke`
und erlaubt GET, POST, PUT, PATCH und DELETE:

```http
POST /api/webhooks{path}
# hmac-sha256: Body mit dem Secret der referenzierten Credential signieren
X-VertexBPMN-Signature: <hmac>
# trigger-secret: Secret direkt im Header
X-VertexBPMN-Trigger-Secret: <secret>
Content-Type: application/json

{ ...payload... }
```

Auth-Modi:

- `hmac-sha256` – Secret kommt aus einer referenzierten Credential; kein Einzelsecret noetig.
- `trigger-secret` – das Secret wird beim Deploy serverseitig erzeugt und nur als Hash abgelegt.
  Fuer **neu** erzeugte Trigger liefert die Deploy-Antwort das Klartext-Secret **einmalig** im
  Response-Header `X-VertexBPMN-Created-Webhooks` (JSON-Array mit `Path`, `Method`, `Secret`,
  `InvokePath`). Der Studio-BPMN-Modeler zeigt es nach dem Deploy in einer ausblendbaren Warnbox
  samt curl-Beispiel; danach ist nur noch der Hash verfuegbar.

Antworten (bei Start-Event, kein Correlation Key): `201 Created` mit der gestarteten
Prozessinstanz; falsches Secret bzw. ungueltige Signatur `401`; unbekannter Pfad oder deaktivierter
Trigger `404`; Payload verletzt das deklarierte Schema `400`; Prozessdefinition fehlt `422`.
## CLI

```text
trigger create <name> <process-key> [tenant]
trigger list [tenant]
trigger invoke <id> <secret> [variables-json] [business-key]
trigger enable|disable <id> [tenant]
trigger delete <id> [tenant]
```

Das Secret wird beim Erstellen einmal in der CLI ausgegeben und muss sicher abgelegt werden.

## .NET SDK

`VertexBpmnClient` stellt folgende Methoden bereit:

```csharp
var created = await client.CreateWorkflowTriggerAsync("Order webhook", "order-process", "acme");
var triggers = await client.ListWorkflowTriggersAsync("acme");
var instance = await client.InvokeWorkflowTriggerAsync(
    created!.Trigger.Id,
    created.Secret,
    new Dictionary<string, object?> { ["customerId"] = "C-42" },
    "ORDER-123");
```

## Studio

Im Studio steht der Bereich `Workflow Triggers` zur Verfügung. Dort können Trigger tenantbezogen registriert, aktiviert/deaktiviert, getestet und gelöscht werden. Das Secret wird nach der Registrierung einmal angezeigt.
