# REST API

## Authentifizierung

Produktive APIs sollten über HTTPS und mit aktivierter Authentifizierung betrieben werden:

```http
Authorization: Bearer <JWT>
X-API-Key: <API_KEY>
```

Secrets gehören nicht in BPMN-Dateien, Wiki-Beispiele oder Quellcode. Verwende Umgebungsvariablen oder einen Secret Store.

## Kernpfade

| Bereich | Endpunkte |
| --- | --- |
| Health | `GET /api/Health` |
| Repository | `GET/POST /api/repository`, `GET/DELETE /api/repository/{id}` |
| Runtime | `GET /api/runtime`, `GET /api/runtime/{id}`, `POST /api/runtime/start` |
| Tasks | `GET /api/task`, `GET /api/task/{id}`, `POST /api/task/{id}/claim` |
| Task-Abschluss | `POST /api/task/{id}/complete`, `POST /api/task/{id}/delegate` |
| Formulare | `GET/PUT /api/vertex/task/{id}/form-schema` |
| OpenAPI | `/swagger` und `src/VertexBPMN.Api/Contracts/openapi.json` |

## Fehlercodes

| Status | Bedeutung |
| --- | --- |
| `200` | Erfolgreiche Abfrage |
| `201` | Definition oder Instanz erstellt |
| `204` | Änderung erfolgreich ohne Inhalt |
| `401` | Token oder API-Key fehlt/ungültig |
| `403` | Rolle fehlt |
| `404` | Ressource nicht gefunden |
| `429` | Rate Limit überschritten |

Bei Integrationen sollten Fehlerstatus und Correlation-/Request-IDs protokolliert werden.

## OpenAPI

Importiere `src/VertexBPMN.Api/Contracts/openapi.json` in Swagger UI, Postman oder einen Client-Generator. Generierte Clients sollten gegen dieselbe API-Version wie der Server gebaut werden.
