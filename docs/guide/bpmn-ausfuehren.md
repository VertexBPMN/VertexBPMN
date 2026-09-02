# BPMN ausführen

## Prozessdefinitionen suchen

```http
GET /api/repository?key=Process_HelloWorld&tenantId=tenant-a
```

Die Antwort enthält unter anderem `id`, `key`, `name`, `version` und `tenantId`.

## Prozessinstanzen abfragen

```http
GET /api/runtime/{instanceId}
GET /api/runtime?processDefinitionId={definitionId}&tenantId=tenant-a
GET /api/vertex/process-instance/{instanceId}
```

## Variablen

```json
{
  "ProcessDefinitionKey": "OrderProcess",
  "Variables": {
    "orderId": "ORD-1007",
    "amount": 125.50,
    "requiresApproval": true
  },
  "BusinessKey": "ORD-1007",
  "TenantId": "sales"
}
```

Verwende stabile primitive JSON-Werte. Komplexe Objekte sollten eine explizite Versionierung besitzen, damit spätere Prozessversionen alte Instanzen lesen können.

## User-Tasks

```http
GET  /api/task?assignee=alice
POST /api/task/{taskId}/claim
POST /api/task/{taskId}/complete
POST /api/task/{taskId}/delegate
```

Task abschließen:

```json
{
  "Variables": {
    "approved": true,
    "reviewedBy": "alice"
  }
}
```

Formulare:

```http
GET /api/vertex/task/{taskId}/form-schema
PUT /api/vertex/task/{taskId}/form-schema
```

Die Vertex-Pfade sind geschützt. Verwende bei aktivierter Authentifizierung einen gültigen Bearer-Token oder API-Key.
