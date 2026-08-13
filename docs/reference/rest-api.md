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
| Credentials | `GET/POST /api/credentials`, `GET/PUT/DELETE /api/credentials/{id}`, `PUT /api/credentials/{id}/secret` |
| OpenAPI | `/swagger` und `src/VertexBPMN.Api/Contracts/openapi.json` |


## Credentials

`api/credentials` verwaltet tenantbezogene Credential-Metadaten und verschlüsselte Secret-Werte für Studio-Connectoren. Lesezugriffe sind tenantbezogen erlaubt; Mutationen sind Admin-only. API-Antworten enthalten nur Metadaten und Secret-Key-Namen, niemals Klartext-Secrets oder geschützte Payloads.

| Aktion | Methode/Pfad | Hinweis |
| --- | --- | --- |
| Auflisten | `GET /api/credentials?tenantId=...` | Tenant-scope; Nicht-Admins nur eigener Tenant |
| Details | `GET /api/credentials/{id}?tenantId=...` | Gibt `404`, wenn Credential nicht im Tenant liegt |
| Erstellen | `POST /api/credentials` | Admin-only; Body enthält `tenantId`, `name`, `type`, `description`, `secrets` |
| Metadaten ändern | `PUT /api/credentials/{id}` | Admin-only; Secret-Werte bleiben unverändert |
| Secret rotieren | `PUT /api/credentials/{id}/secret` | Admin-only; rotiert einen einzelnen Secret-Key |
| Löschen | `DELETE /api/credentials/{id}?tenantId=...` | Admin-only |

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
