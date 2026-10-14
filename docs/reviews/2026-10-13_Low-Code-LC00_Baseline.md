# LC00 — Aktuelle Inventur und reproduzierbare Baseline

Stand: 2026-10-13. Planbasis `2e772249`. Ausgangscommit dieses Pakets: `28e892d3`.
Feature-Branch: `codex/low-code-lc00`.

Status: **offen → lokal geprüft (Inventur + Baseline erfasst; externe Prüfungen offen).**
Befund im Sinne des Plans (§9): LC00 liefert die Inventur und die reproduzierbare
Baseline; die Abnahme aus §5 LC00 ist erfüllt, soweit sie ohne laufende autorisierte
Umgebung lokal nachweisbar ist. Extern ausstehende Punkte sind unten benannt.

## Ziel und Geltungsbereich

Dieses Dokument erfasst den Ausgangsstand für die Low-Code-Anwendungsplattform
(Implementierungsplan `2026-10-13_Low-Code-Anwendungsplattform_Implementierungsplan.md`).
Es hält die Ist-Befunde, die real geprüften Anschlussstellen, ausgeführte Befehle
mit Laufzahlen sowie die offenen externen Prüfungen fest, damit Folgepakete (LC01 ff.)
gegen einen nachvollziehbaren Ausgangszustand arbeiten.

## Ausgangszustand

| Kennzahl | Wert |
| --- | --- |
| Branch | `codex/low-code-lc00` |
| Ausgangscommit | `28e892d3` (docs: require HTTP request files) |
| SDK (`global.json`) | .NET `10.0.302`, Runner Microsoft.Testing.Platform, xUnit v3 |
| Installiertes SDK | `10.0.302` unter `/home/azureuser/.dotnet` (Host `/usr/bin/dotnet` = `10.0.112` unzureichend) |
| Provider | lokal: SQLite/PostgreSQL (Port 5432), RabbitMQ (5672), Keycloak (58080), API (5263) |
| Arbeitsbaum | sauber; nur vorbestandene untracked `.agents/skills/`, `skills-lock.json` |
| Festplatte | Ausgang 99% belegt (455 MB frei); nach Docker-Cleanup 82% (5.3 GB frei) |

## Ausgeführte Befehle und Ergebnisse

| Schritt | Befehl | Ergebnis |
| --- | --- | --- |
| Branch anlegen | `git switch -c codex/low-code-lc00` | ok |
| Docker-Cleanup (Hermes/VertexBPMN-only) | `docker system df`, gezielte `rm`/`rmi`/`volume prune`/`builder prune`; OpenClaw-Image/Container entfernt | freier Platz 2.3 GB → 5.3 GB; alle 6 Vertex-Dienste intakt |
| NuGet-Restore | `dotnet restore tests/VertexBPMN.Tests/...csproj` | ok |
| Build (Release) | `dotnet build tests/VertexBPMN.Tests/... --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1` | **0 Warnungen, 0 Fehler** (8:42) |
| Test Connector-Pfade | `dotnet test ... --filter "FullyQualifiedName~Connector"` | **49/49 grün** (0 failed, 0 skipped, 7.1 s) |
| Test Formular + Import | `dotnet test ... --filter "FullyQualifiedName~FormDefinition|FormBuilder|OpenApiConnectorTemplateImporter"` | **5/5 grün** (0 failed, 0 skipped, 1.4 s) |

## Inventur der Anschlussstellen (§2 des Plans)

| Befund | Reale Datei (verifiziert) | Ergebnis |
| --- | --- | --- |
| Connector-Template-Eigenschaften als flacher Key/Type-Datensatz | `src/VertexBPMN.Domain/Interfaces/ConnectorTemplateProperty.cs` (record mit `Key, Type, Required, DefaultValue, Options`) | Keine versionierten Binding-Metadaten; LC02-Befund bestätigt |
| HTTP-Executor liefert Status, aber keinen Antwortinhalt/Body | `src/VertexBPMN.Application/Connectors/HttpConnectorExecutor.cs` (liest `vertex:connector.method/body/contentType`, Response-Header-Reads; gibt nur `httpStatus` zurück) | LC03-Befund bestätigt: Body fehlt |
| Vertex-Connector-Handler: `vertex:connector.*`-Attribute, Credential-/SSRF-Regeln | `src/VertexBPMN.Application/Connectors/VertexConnectorServiceTaskHandler.cs` | Bindung lesbar; `IsAllowedCredentialHost`, Retry, Audit vorhanden |
| XML-Flattenning `vertex:connector.*`/`ioMapping` | `src/VertexBPMN.Engine/Parsing/VertexBpmnExtensions.cs` (`CopyAttributes`, `FlattenIoMapping`) | Studio→Runtime-Vertrag vorhanden; ioMapping textuell `name=value` |
| Formulare tenantbezogen, Update überschreibt Zeile, `Version` zählt | `src/VertexBPMN.Domain/Entities/FormDefinitionRecord.cs` (Id, TenantId, Key, Name, Schema, `Version`, CreatedAt, LastModified) | LC04-Befund bestätigt: keine unveränderlichen Revisionen |
| API-Routen | `api/repository` (GET, GET/{id}, POST deploy, DELETE/{id}); `api/runtime` (POST start, GET/{id}, GET); `api/forms` (GET, GET/{id}, POST, PUT/{id}); `api/task` (GET, GET/{id}, POST {id}/claim, POST {id}/complete, POST {id}/delegate); `api/external-tasks` (POST claim, GET {jobId}, POST {jobId}/heartbeat|complete|fail); `api/health` | Routen aus Controllern verifiziert |
| `.http`-Einstieg war ein Weatherforecast-Template | `src/VertexBPMN.Api/VertexBPMN.Api.http` (vorher: `GET /weatherforecast/`) | Auf reale Routen umgestellt + Katalogverweis |
| `launchSettings.json` gitignored | `.gitignore` Zeile 69 `**/Properties/launchSettings.json`; keine Datei im Repo | Ports kommen aus lokalen Profilen (http: 5263); nicht versioniert |
| HTTP-Response-/Body-Anteil | `HttpConnectorExecutor` liefert nur `httpStatus`-Output | Kein Datenantwort-Pfad für LC03 |

## Reproduzierbarkeit des HTTP-Template-Pfads (LC00-Pflicht)

Der vollständige Pfad — Studio-Serialisierung → Repository-Deployment → persistente
Runtime-Handler — wird in LC02 bis zur End-to-End-Ausführung mit Test-HTTP-Request
auf Methode/URL/Body/Parameter reproduziert. Die dafür nötigen Vertragsteile sind oben
inventarisiert: Der Handler liest `vertex:connector.type/operationId/endpoint/body/...`,
die Runtime leitet über `IConnectorRuntime` an `HttpConnectorExecutor`. Ein
Executor-Direkttest für den HTTP-Pfad ist Bestandteil der vorhandenen
`ConnectorRuntimeTests`; er bleibt als gesonderter Teilnachweis erhalten.

## Gelieferte Dateien (LC00)

| Datei | Inhalt |
| --- | --- |
| `docs/reviews/2026-10-13_Low-Code-LC00_Baseline.md` | dieses Dokument (Ausgangscommit, Befunde, Befehle, Laufzahlen, offene Prüfungen) |
| `src/VertexBPMN.Api/VertexBPMN.Api.http` | smoke-Einstieg umgestellt auf reale Routen (`/api/health`, `/api/engine/capabilities`) + Verweis auf `Http/` |
| `src/VertexBPMN.Api/Http/runtime-baseline.http` | neu: Deploy → Start → Read → Start-Negativfall (Methode/Route/DTO/Auth aus Code verifiziert) |
| `src/VertexBPMN.Api/http-client.env.json` | neu: versionierte, nicht-sensitive Umgebungsbasis (`baseUrl`, `tenantId`, `otherTenantId`, `appKey`) |

## Offene externe Prüfungen (LC00)

Die folgenden Prüfungen erfordern eine laufende autorisierte Umgebung bzw. einen
live ausführbaren HTTP-Editor und bleiben **offen** (nicht als erfüllt gewertet):

1. `Http/runtime-baseline.http` gegen eine isolierte laufende API ausführen
   (smoke + Deploy/Start/Read mit echter Testidentität); IDs aus echten Antworten.
2. Browser-/Studio-Abnahme (Editor-Serialisierung) mit getrennten Voraussetzungen.
3. CI-safe-Gesamtsuite im Autorisierten CI (kein ungefilterter lokaler Gesamtlauf).

## Abnahme (LC00)

Ausgangscommit, Befunde (gegen reale Dateien verifiziert), Befehle, Laufzahlen
(49/49 + 5/5 grün) und offene externe Prüfungen sind in diesem Dokument festgehalten.
Ein bestehender Befund (§2: HTTP-Executor ohne Antwort-Body; Formulare ohne
unveränderliche Revisionen; `.http`-Einstieg ohne echte Routen) ist durch die
Inventur sichtbar. Das nächste abhängige Paket ist **LC01** (Verträge und
Architekturentscheidungen).

> Hinweis: Der erste Befund ist der **Build-/Restore-Baseline auf dem Host**: Das
> standardmäßige `dotnet` (10.0.112) löste die von `global.json` geforderte Version
> `10.0.302` nicht auf; die korrekte SDK-Installation liegt unter
> `/home/azureuser/.dotnet`. Build- und Testbefehle setzen deshalb
> `DOTNET_ROOT=/home/azureuser/.dotnet` und `PATH=/home/azureuser/.dotnet:$PATH`.

## Test-Laufzahlen (CI-safe Teil, lokal)

Es wurden die für LC00-relevanten, nicht extern abhängigen Testgruppen ausgeführt
(Connector-/HTTP-Executor-/Mapping-Pfade), nicht der ungefilterte Gesamtlauf.
