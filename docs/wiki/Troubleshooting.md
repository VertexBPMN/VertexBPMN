# Troubleshooting

## `401 Unauthorized` oder `403 Forbidden`

Prüfe Token, Authorization-Header, erforderliche Rolle und Tenant-ID.

## `404 Not Found` beim Starten

Prüfe den exakten `ProcessDefinitionKey`. Eine Definition muss zuerst erfolgreich über `POST /api/repository` deployed worden sein. Liste sie danach über `GET /api/repository?key=...`.

## `429 Too Many Requests`

Die API verwendet Rate Limiting. Implementiere begrenztes Exponential Backoff für wiederholbare GET-Aufrufe. POST-Aufrufe dürfen nur wiederholt werden, wenn sie idempotent abgesichert sind.

## Deployment akzeptiert das XML nicht

Prüfe Namespace, `process id`, `isExecutable="true"`, Start Event und erreichbare Sequence Flows. Teste das Modell außerdem im bpmn.io Modeler und prüfe die Serverlogs.

## SDK liefert `null`

Der SDK-Client interpretiert `404 Not Found` bei einzelnen Get-Methoden als nicht gefunden und liefert `null`. Andere Fehler lösen `HttpRequestException` aus.

## Falscher Port

Verlasse dich nicht auf den alten Beispielport `5000`. Verwende die URL aus Launch-Profil, CLI-Ausgabe oder Swagger. Der CLI-Workflow verwendet standardmäßig `http://localhost:51870`.
