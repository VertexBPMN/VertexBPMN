# VertexBPMN: n8n-Feature-Erweiterung — Umsetzungsplan für KI-Coding-Agenten

**Zielrepo:** `github.com/VertexBPMN/VertexBPMN` (Branch `master`, .NET 10)
**Zielgruppe dieses Dokuments:** KI-Coding-Agenten (Claude Code, ChatGPT/Codex, DeepSeek, etc.), die die Umsetzung eigenständig durchführen.
**Nicht-Ziel:** Dies ist kein Design-Diskussionsdokument. Jede Phase ist so geschrieben, dass sie ohne Rückfragen mechanisch abgearbeitet werden kann. Wo eine Entscheidung nötig ist, steht sie explizit unter „Entscheidung getroffen" mit Begründung.

---

## 0. Vor dem Start: Repo-Konventionen, die JEDE Phase betreffen

Lies diesen Abschnitt vollständig, bevor du mit Phase 1 beginnst. Alle vier Phasen folgen denselben Mustern — das spart dir, sie in jeder Phase neu herzuleiten.

### 0.1 Build & Test (immer vor und nach jeder Phase ausführen)

```bash
dotnet restore VertexBPMN.sln
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release --no-build --no-restore --filter-not-trait "Category=Phase3ExternalAcceptance" --max-parallel-test-modules 1
```

Wenn der Build oder die Tests vor deiner Änderung bereits fehlschlagen, **stoppe und melde das** — arbeite nicht auf einem kaputten Basiszustand weiter.

### 0.2 Schichten-Architektur (strikt einhalten)

```
VertexBPMN.Domain          -> Entities, Interfaces, reine Datentypen. KEINE Abhängigkeit auf andere Projekte.
VertexBPMN.Application     -> Business-Logik, Services, Connector-Runtime, Import-Logik. Hängt von Domain ab.
VertexBPMN.Infrastructure   -> EF Core, Persistenz-Implementierungen der Domain-Interfaces, Migrations.
VertexBPMN.Engine           -> BPMN/DMN/CMMN-Ausführungskern.
VertexBPMN.Api              -> Controller, DI-Wiring (Program.cs), Plugins, Debug-Endpunkte.
VertexBPMN.Cli              -> Terminal-Befehle, dünner Wrapper um Application-Services.
VertexBPMN.Studio           -> Blazor-UI.
```

Regel: Neue Interfaces kommen nach `Domain/Interfaces`, neue Entities nach `Domain/Entities`, neue Business-Logik nach `Application`, neue EF-Implementierungen nach `Infrastructure/Persistence/Services`, neue Migrations nach `Infrastructure/Persistence/Migrations/Bpmn`.

### 0.3 Muster: Neuer persistenter „Metadata-Service" (Credential/Connector/ConnectorTemplate folgen alle diesem Muster)

Für jede neue persistente Ressource (in diesem Plan: OAuth2-Flow-Zustand, Task-IO-Snapshots, Polling-Sources) IMMER dieses 5-Teile-Muster verwenden:

1. **Entity** in `VertexBPMN.Domain/Entities/<Name>Record.cs` — flaches POCO, `string TenantId`, `DateTime CreatedAt`, `DateTime LastModified`.
2. **Interface + DTOs** in `VertexBPMN.Domain/Interfaces/I<Name>Service.cs` — Interface-Methoden als `Task<...>` mit `CancellationToken cancellationToken = default` als letztem Parameter. DTOs als `sealed record`.
3. **Implementierung** in `VertexBPMN.Infrastructure/Persistence/Services/Persistent<Name>Service.cs` — Konstruktor-Injection von `BpmnDbContext db`, ggf. `IAuditLogService auditLogService`. Jede schreibende Methode ruft am Ende `AuditAsync(...)` auf (siehe `PersistentCredentialService.cs` als Referenzimplementierung).
4. **Migration** in `VertexBPMN.Infrastructure/Persistence/Migrations/Bpmn/<yyyyMMddHHmmss>_<Beschreibung>.cs` — folge exakt dem Muster von `20260818090000_AddConnectorTemplates.cs` (siehe Abschnitt 0.5).
5. **DbSet-Eintrag** in `VertexBPMN.Infrastructure/Persistence/BpmnDbContext.cs`: `public DbSet<XRecord> Xs => Set<XRecord>();`

### 0.4 Muster: Tenant-Isolation (überall verpflichtend)

Jede Query MUSS nach `TenantId` filtern. Referenz: `PersistentCredentialService.FindAsync`:

```csharp
private Task<CredentialRecord?> FindAsync(string tenantId, string id, CancellationToken cancellationToken) =>
    db.Credentials.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, cancellationToken);
```

Jede öffentliche Methode validiert den Tenant zu Beginn mit `ValidateTenant(tenantId)` (siehe `PersistentConnectorService.cs`).

### 0.5 Muster: EF-Migration (Handschrift, kein `dotnet ef` Autogenerate nötig — aber erlaubt)

Referenzdatei: `src/VertexBPMN.Infrastructure/Persistence/Migrations/Bpmn/20260818090000_AddConnectorTemplates.cs`. Struktur:

```csharp
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("<yyyyMMddHHmmss>_<Name>")]
public partial class <Name> : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "<TabelleName>",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                TenantId = table.Column<string>(maxLength: 64, nullable: false),
                // ... weitere Spalten
                CreatedAt = table.Column<DateTime>(nullable: false),
                LastModified = table.Column<DateTime>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_<TabelleName>", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_<TabelleName>_TenantId", table: "<TabelleName>", column: "TenantId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "<TabelleName>");
}
```

Wenn du stattdessen `dotnet ef migrations add <Name> --context BpmnDbContext --project src/VertexBPMN.Infrastructure --startup-project src/VertexBPMN.Api` verwendest: prüfe danach die generierte Datei manuell gegen dieses Muster (Timestamp-Namenskonvention, Indizes auf `TenantId`).

**Wichtig:** Es existiert bereits ein Snapshot-File (`BpmnDbContextModelSnapshot.cs` im selben Ordner). Nach jeder neuen Migration muss dieser Snapshot aktualisiert werden — entweder automatisch durch `dotnet ef migrations add`, oder manuell, falls du das Migrationsfile von Hand schreibst (dann `dotnet ef migrations add <Name>` NICHT ausführen, sondern nur den Snapshot durch Nachvollziehen der Änderung von Hand ergänzen, oder — sicherer — `dotnet ef migrations add` verwenden und danach die generierte Migration gegen das obige Muster angleichen).

### 0.6 Muster: DI-Registrierung

- Neue Application-Services (Singleton, wenn zustandslos oder mit eigenem internem Locking): in `VertexBPMN.Application/ApplicationModule.cs`, Beispiel: `services.AddSingleton<IN8nWorkflowImporter, N8nWorkflowImporter>();`
- Neue `IConnectorExecutor`-Implementierungen: in `VertexBPMN.Application/Extensions/ServiceTaskRegistryExtensions.cs`, im selben Block wie die bestehenden `services.AddSingleton<IConnectorExecutor, ...>()`-Zeilen.
- Neue persistente Services (Scoped, weil sie `BpmnDbContext` injizieren): in `VertexBPMN.Infrastructure/InfrastructureModule.cs`, als `services.AddScoped<IXService, PersistentXService>();`

### 0.7 Muster: CLI-Befehl hinzufügen

In `VertexBPMN.Cli/CliApplication.cs`:
1. Neuen `case "xyz":` Zweig im `switch (args[0].ToLowerInvariant())` (ab Zeile ~100) hinzufügen, der an eine neue private Methode `ExecuteXyzCommandAsync(args, cancellationToken)` delegiert.
2. Die neue Methode folgt dem Muster von `ExecuteConnectorCommandAsync` (Zeile 355): `RequireArguments(args, N)`, `switch (args[1].ToLowerInvariant())` für Subcommands, `TenantAt(args, index)` für optionalen Tenant-Parameter, Fehler als `CliUsageException` mit Usage-String im `default:`-Zweig.

### 0.8 Muster: Redaction (Secrets nie im Klartext persistieren/loggen)

Referenz: `VertexBPMN.Application/Connectors/ConnectorRuntime.cs`, Klasse `ConnectorRedactionPolicy`. Wiederverwenden statt neu erfinden, wenn du Werte in `HistoryEvent.Data`, Audit-Logs oder API-Responses schreibst, die potenziell Secrets enthalten könnten.

### 0.9 Muster: Tests

Für jede neue Service-Klasse: Unit-Test in `tests/VertexBPMN.Tests/Unit/Infrastructure/Persistent<Name>ServiceTests.cs` (Referenz: `PersistentCredentialServiceTests.cs`). Für jeden neuen Controller-Endpunkt: Integrationstest in `tests/VertexBPMN.Tests/Integration/Api/<Name>ApiTests.cs` (Referenz: `CredentialApiTests.cs`). Für Connector-/Import-Logik ohne DB: `tests/VertexBPMN.Tests/Unit/Application/` (Referenz: `N8nWorkflowImporterTests.cs`, `ConnectorRuntimeTests.cs`).

### 0.10 Bekannter Architektur-Konflikt — vor Phase 1 klären

Es gibt zwei parallele Connector-Abstraktionen im Repo:
- `VertexBPMN.Application/Connectors/IConnectorExecutor` — **aktiv genutzt** über `ConnectorRuntime` und `VertexConnectorServiceTaskHandler`. Das ist der Pfad, den BPMN-Service-Tasks zur Laufzeit tatsächlich aufrufen.
- `VertexBPMN.Api/Plugins/IExternalConnector` — Teil des Plugin-Systems (`PluginManager.cs`).

**Entscheidung getroffen für diesen Plan:** Alle neuen Connector-Fähigkeiten (Phase 1) werden ausschließlich über `IConnectorExecutor`/`ConnectorRuntime` gebaut, NICHT über `IExternalConnector`. Begründung: `IConnectorExecutor` ist der Pfad, den `VertexConnectorServiceTaskHandler` beim BPMN-Service-Task-Aufruf tatsächlich verwendet; `IExternalConnector` ist für Drittanbieter-Plugins gedacht und aktuell nicht in den Ausführungspfad von Service-Tasks eingehängt. Falls ein Agent bei der Umsetzung feststellt, dass sich das geändert hat, MUSS er das hier dokumentierte Muster (`IConnectorExecutor`) beibehalten und ggf. eine Migration von `IExternalConnector`-Plugins auf `IConnectorExecutor` als separates Ticket vorschlagen, statt es in dieser Phase mitzulösen.

---

## Phase 1: OpenAPI-Connector-Importer

**Ziel:** Aus einer OpenAPI-3.x-Spezifikation automatisiert `ConnectorTemplateRecord`-Einträge erzeugen, die anschließend im Studio (Connector-Palette) und via `VertexConnectorServiceTaskHandler` nutzbar sind — ohne pro Connector eine neue `IConnectorExecutor`-Klasse zu schreiben.

### 1.1 Ist-Zustand (bereits vorhanden, NICHT neu bauen)

- `IConnectorTemplateService` (`Domain/Interfaces/IConnectorTemplateService.cs`) + `PersistentConnectorTemplateService` — CRUD für Templates ist fertig.
- `ConnectorTemplateRecord` (`Domain/Entities/ConnectorTemplateRecord.cs`) hat bereits `PropertiesJson` (Liste von `ConnectorTemplateProperty { Key, Type, Required, DefaultValue, Options }`) und `AppliesToJson`.
- `HttpConnectorExecutor` (`Application/Connectors/BuiltInConnectorExecutors.cs`) führt bereits generische HTTP-Calls aus Attributen aus (`vertex:connector.method`, `vertex:connector.body`, `vertex:connector.authScheme`).
- `N8nWorkflowImporter` (`Application/Import/N8nWorkflowImporter.cs`) ist die Referenzimplementierung für das Report-Pattern (`Migrated`/`NeedsReview`/`Unsupported`).

### 1.2 Neue Dateien

**1.2.1** `VertexBPMN.Application/Import/OpenApiConnectorTemplateImporter.cs`

```csharp
namespace VertexBPMN.Application.Import;

public interface IOpenApiConnectorTemplateImporter
{
    OpenApiImportResult Import(string openApiJsonOrYaml, string tenantId);
}

public sealed record OpenApiImportResult(
    IReadOnlyList<ConnectorTemplateWriteRequest> Templates,
    IReadOnlyList<OpenApiImportReportItem> Report);

public sealed record OpenApiImportReportItem(string OperationId, N8nImportDisposition Disposition, string Message);
```

Verwende `N8nImportDisposition` wieder (aus `Application/Import/N8nWorkflowImporter.cs`) statt ein neues Enum zu definieren — gleiche Semantik.

**Implementierungslogik von `Import`:**
1. Spec parsen. Für JSON: `System.Text.Json`. Für YAML: prüfe zuerst, ob bereits ein YAML-Parser-Paket im Repo referenziert wird (`grep -rn "YamlDotNet" **/*.csproj`); falls nicht, unterstütze in dieser Phase NUR JSON-OpenAPI-Specs und dokumentiere das im Report als Einschränkung — YAML-Support ist ein separates Ticket.
2. Für jeden Eintrag unter `paths.<path>.<method>`:
   - `operationId` lesen (Pflicht — fehlt er, Disposition `Unsupported`, Grund: "operationId fehlt").
   - `parameters` (in: `path`, `query`, `header`) und `requestBody.content['application/json'].schema` einsammeln → werden zu `ConnectorTemplateProperty`-Einträgen (`Type` grob mappen: `string`→`"string"`, `integer`/`number`→`"number"`, `boolean`→`"boolean"`, alles andere (Objekte, Arrays, `oneOf`/`anyOf`) → `"string"` MIT Eintrag in den Report als `NeedsReview` ("Komplexes Schema wurde auf Freitext reduziert, manuell prüfen").
   - `security`/`securitySchemes` lesen: `apiKey` (`in: header`) → `authScheme = "ApiKey"` als Property mit `Required = true`; `http` mit `scheme: bearer` → `authScheme = "Bearer"`; `oauth2` → NICHT in Phase 1 automatisch verdrahten, stattdessen `NeedsReview`-Eintrag: "OAuth2-Security-Scheme erkannt, Credential muss nach Phase 2 (OAuth2-Flow) manuell verknüpft werden."
3. Für jede erfolgreich gemappte Operation ein `ConnectorTemplateWriteRequest` bauen:
   - `Name` = `operationId`
   - `Category` = `"openapi-import"`
   - `AppliesTo` = `["serviceTask"]`
   - `Runtime` = `"http"` (verweist auf `HttpConnectorExecutor.Type`)
   - `Properties` = die oben gesammelten `ConnectorTemplateProperty`-Einträge PLUS fixe technische Properties: `vertex:connector.method` (Default = HTTP-Methode aus der Spec), `vertex:connector.endpoint` (Default = `servers[0].url + path`, mit `{param}`-Platzhaltern).
4. Report-Eintrag pro Operation mit `Migrated`/`NeedsReview`/`Unsupported`.

**1.2.2** Erweiterung `HttpConnectorExecutor.ExecuteAsync` (Datei: `Application/Connectors/BuiltInConnectorExecutors.cs`)

Aktuell (Zeile 21-22) wird `vertex:connector.body` unverändert als Request-Body verwendet. Für importierte OpenAPI-Connectoren mit Pfad-Parametern (`/users/{id}`) brauchst du Platzhalter-Ersetzung aus `context.Variables`:

```csharp
private static Uri ResolveEndpoint(Uri template, IDictionary<string, object> variables)
{
    var path = template.ToString();
    foreach (var pair in variables)
        path = path.Replace($"{{{pair.Key}}}", Uri.EscapeDataString(Convert.ToString(pair.Value) ?? string.Empty));
    return new Uri(path);
}
```

Rufe diese Methode in `ExecuteAsync` auf, bevor `context.Endpoint` verwendet wird (Zeile 16). **Achtung:** `context.Endpoint` ist ein `Uri`-Record-Property, nicht direkt veränderbar — arbeite mit einer lokalen Variable `var endpoint = ResolveEndpoint(context.Endpoint, context.Variables);` und verwende `endpoint` statt `context.Endpoint` im Rest der Methode.

### 1.3 DI-Registrierung

In `VertexBPMN.Application/ApplicationModule.cs`, direkt neben der bestehenden Zeile:
```csharp
services.AddSingleton<IN8nWorkflowImporter, N8nWorkflowImporter>();
services.AddSingleton<IOpenApiConnectorTemplateImporter, OpenApiConnectorTemplateImporter>();
```

### 1.4 API-Controller

Neue Datei `VertexBPMN.Api/Controllers/OpenApiImportController.cs`, strukturell analog zu `N8nImportController.cs` (gleiche Datei ansehen und kopieren):
- `POST /api/import/openapi?tenantId=...` — Body: Roh-JSON der OpenAPI-Spec. Ruft `IOpenApiConnectorTemplateImporter.Import` auf, persistiert danach jedes `ConnectorTemplateWriteRequest` über `IConnectorTemplateService.CreateAsync` (bereits vorhanden), gibt Report zurück.
- Gleiches Auth-Schema wie `N8nImportController` übernehmen (JWT/API-Key, siehe dortige `[Authorize]`-Attribute).

### 1.5 CLI

In `VertexBPMN.Cli/CliApplication.cs`:
```csharp
case "connector":
    await ExecuteConnectorCommandAsync(args, cancellationToken);
    return 0;
```
Erweitere `ExecuteConnectorCommandAsync` (Zeile 355) um einen neuen Subcommand `import-openapi`:
```csharp
case "import-openapi":
    RequireArguments(args, 3);
    var openApiResult = _openApiImporter.Import(await ReadFileAsync(args[2]), TenantAt(args, 3));
    foreach (var template in openApiResult.Templates)
        await _connectorTemplateService.CreateAsync(TenantAt(args, 3), template, cancellationToken);
    foreach (var item in openApiResult.Report)
        await _output.WriteLineAsync($"{item.Disposition}: {item.OperationId} - {item.Message}");
    break;
```
Konstruktor von `CliApplication` um `IOpenApiConnectorTemplateImporter _openApiImporter` erweitern (analog zu `_n8nImporter`).

### 1.6 Tests

- `tests/VertexBPMN.Tests/Unit/Application/OpenApiConnectorTemplateImporterTests.cs` — analog `N8nWorkflowImporterTests.cs`. Testfälle: (a) einfache GET-Operation mit Query-Parametern → `Migrated`; (b) Operation ohne `operationId` → `Unsupported`; (c) Operation mit `oneOf`-Schema im Body → `NeedsReview`; (d) `oauth2`-Security-Scheme → `NeedsReview` mit passendem Hinweistext.
- `tests/VertexBPMN.Tests/Unit/Application/ConnectorRuntimeTests.cs` erweitern: Testfall für `ResolveEndpoint`-Platzhalter-Ersetzung.

### 1.7 Akzeptanzkriterien Phase 1

**Status: UMSGESETZT (Branch `VertexBPMN-n8n-Features-Umsetzungsplan`).** Gate: 812 tests / 811 ok / 0 failed. JSON-only (kein YAML-Parser im Repo — dokumentiert als Einschränkung, separates Ticket). CLI-Testfixture erweitert (Registrierung `IOpenApiConnectorTemplateImporter`).

- [x] `dotnet build` und `dotnet test` (siehe 0.1) sind grün.
- [x] Eine Beispiel-OpenAPI-Spec mit mindestens 3 Operationen (GET mit Query-Param, POST mit Body, eine mit `apiKey`-Security) lässt sich per CLI importieren und erzeugt 3 `ConnectorTemplateRecord`-Einträge.
- [ ] Ein importierter Connector lässt sich in einem Test-BPMN-Prozess als Service-Task mit `vertex:connector.type=http` referenzieren und über `ConnectorRuntime.ExecuteAsync` erfolgreich gegen einen Test-HTTP-Endpunkt ausführen (Integrationstest, ggf. mit `WireMock.Net` oder vorhandenem Test-HTTP-Fake — prüfe `tests/VertexBPMN.Tests/Integration` auf existierende HTTP-Mocking-Infrastruktur, bevor du eine neue einführst).
- [x] Report enthält für jede Operation genau einen Eintrag mit korrekter Disposition.

---

## Phase 2: OAuth2-Credential-Flow

**Ziel:** Ein neuer Credential-Typ `oauth2`, der einen Authorization-Code-Flow im Studio unterstützt, inklusive automatischem Token-Refresh vor Connector-Nutzung.

### 2.1 Ist-Zustand (bereits vorhanden, NICHT neu bauen)

- `ICredentialService` + `PersistentCredentialService` (`Infrastructure/Persistence/Services/PersistentCredentialService.cs`) — verschlüsselte Key/Value-Secrets via `IDataProtector`, Rotation über `RotateSecretAsync`, Audit über `AuditAsync`. **Wiederverwenden, nicht duplizieren.**
- `CredentialRecord.Type` ist bereits ein freies String-Feld — der Typ `"oauth2"` kann ohne Schema-Änderung an `CredentialRecord` selbst eingeführt werden.
- `VertexConnectorServiceTaskHandler.ResolveSecretAsync` (`Application/Connectors/ConnectorRuntime.cs`, Zeile 327) ist der zentrale Ort, an dem ein Secret vor jeder Connector-Ausführung aufgelöst wird — das ist der richtige Einhängepunkt für Token-Refresh-Logik.

### 2.2 Entscheidung: Keine neue Tabelle für Access-/Refresh-Token

**Entscheidung getroffen:** Access-Token, Refresh-Token und Ablaufzeit werden als normale Secrets unter den Keys `access_token`, `refresh_token`, `expires_at` (ISO-8601-String) im bestehenden `CredentialRecord.ProtectedValues` gespeichert. Begründung: Wiederverwendung von Verschlüsselung, Rotation, Audit-Logging und Tenant-Isolation ohne neuen Code. Nur der **State-Parameter während des laufenden Authorization-Flows** (zwischen Redirect und Callback) braucht einen neuen, kurzlebigen Speicher (siehe 2.3).

### 2.3 Neue Dateien

**2.3.1** `VertexBPMN.Domain/Entities/OAuth2FlowStateRecord.cs`

```csharp
namespace VertexBPMN.Domain.Entities;

public sealed class OAuth2FlowStateRecord
{
    public string State { get; set; } = string.Empty; // Primary Key, kryptographisch zufällig
    public string TenantId { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } // CreatedAt + 10 Minuten, danach ungültig
}
```

**2.3.2** `VertexBPMN.Domain/Interfaces/IOAuth2CredentialFlowService.cs`

```csharp
namespace VertexBPMN.Domain.Interfaces;

public interface IOAuth2CredentialFlowService
{
    Task<OAuth2AuthorizationStart> StartAuthorizationAsync(string tenantId, string credentialId, OAuth2AuthorizationConfig config, CancellationToken cancellationToken = default);
    Task<bool> CompleteAuthorizationAsync(string state, string code, CancellationToken cancellationToken = default);
    Task<string?> ResolveValidAccessTokenAsync(string tenantId, string credentialId, CancellationToken cancellationToken = default);
}

public sealed record OAuth2AuthorizationConfig(string AuthorizationUrl, string TokenUrl, string ClientId, string ClientSecretKey, string RedirectUri, string Scopes);
public sealed record OAuth2AuthorizationStart(string RedirectUrl, string State);
```

**Wichtig zu `ClientSecretKey`:** Der Client-Secret wird NICHT in `OAuth2FlowStateRecord` gespeichert (das wäre ein Klartext-Leck in einer Tabelle ohne `IDataProtector`). Stattdessen verweist `ClientSecretKey` auf einen bereits existierenden Secret-Key im selben `CredentialRecord` (z. B. `client_secret`), der zur Laufzeit über `ICredentialService.ResolveSecretAsync` nachgeladen wird.

**2.3.3** `VertexBPMN.Infrastructure/Persistence/Services/OAuth2CredentialFlowService.cs`

Implementierung von `IOAuth2CredentialFlowService`:

```csharp
public sealed class OAuth2CredentialFlowService(
    BpmnDbContext db,
    ICredentialService credentialService,
    IHttpClientFactory httpClientFactory,
    IAuditLogService auditLogService) : IOAuth2CredentialFlowService
{
    public async Task<OAuth2AuthorizationStart> StartAuthorizationAsync(string tenantId, string credentialId, OAuth2AuthorizationConfig config, CancellationToken cancellationToken = default)
    {
        // 1. Prüfen, dass Credential existiert und TenantId passt (credentialService.GetAsync)
        // 2. State erzeugen: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) URL-safe gemacht (gleiches Muster wie WorkflowTriggerService.CreateSecret())
        // 3. OAuth2FlowStateRecord speichern, ExpiresAt = UtcNow + 10 Minuten
        // 4. RedirectUrl bauen: config.AuthorizationUrl + "?response_type=code&client_id=" + config.ClientId + "&redirect_uri=" + Uri.EscapeDataString(config.RedirectUri) + "&scope=" + Uri.EscapeDataString(config.Scopes) + "&state=" + state
    }

    public async Task<bool> CompleteAuthorizationAsync(string state, string code, CancellationToken cancellationToken = default)
    {
        // 1. OAuth2FlowStateRecord laden, prüfen ExpiresAt > UtcNow, sonst false + löschen
        // 2. Client-Secret via credentialService.ResolveSecretAsync(tenantId, credentialId, "client_secret", ct) laden
        // 3. POST an TokenUrl: grant_type=authorization_code, code, redirect_uri, client_id, client_secret
        // 4. Response parsen (access_token, refresh_token, expires_in)
        // 5. Über credentialService.RotateSecretAsync dreimal aufrufen: "access_token", "refresh_token", "expires_at" (UtcNow + expires_in Sekunden, als "o"-Format ISO-8601 String)
        // 6. OAuth2FlowStateRecord löschen (einmalig verwendbar, wie das Trigger-Secret-Muster)
        // 7. Audit-Eintrag "credential.oauth2_completed"
    }

    public async Task<string?> ResolveValidAccessTokenAsync(string tenantId, string credentialId, CancellationToken cancellationToken = default)
    {
        // 1. expires_at laden, mit DateTime.UtcNow vergleichen (Puffer: 60 Sekunden vor Ablauf schon als abgelaufen behandeln)
        // 2. Wenn noch gültig: access_token zurückgeben
        // 3. Wenn abgelaufen: refresh_token laden, POST an TokenUrl mit grant_type=refresh_token, neue Tokens per RotateSecretAsync speichern, access_token zurückgeben
        // 4. Wenn kein refresh_token vorhanden und abgelaufen: null zurückgeben (Aufrufer muss Re-Authorization anstoßen)
    }
}
```

Für den Token-Endpoint-Aufruf: nutze `IHttpClientFactory.CreateClient()`, keinen neuen HttpClient-Typ registrieren, außer im Repo existiert bereits ein benannter Client-Typ für ausgehende OAuth-Calls (prüfe `grep -rn "AddHttpClient" src/VertexBPMN.Infrastructure`).

### 2.4 Migration

Neue Migration `<timestamp>_AddOAuth2FlowState.cs` nach Muster 0.5, Tabelle `OAuth2FlowStates`:
- `State` (string, PK, maxLength 128)
- `TenantId`, `CredentialId` (string, maxLength 128, indiziert zusammen)
- `AuthorizationUrl`, `TokenUrl`, `ClientId`, `RedirectUri`, `Scopes` (string, kein Secret, daher unverschlüsselt zulässig)
- `CreatedAt`, `ExpiresAt` (DateTime)

Zusätzlich ein Hintergrundjob, der abgelaufene `OAuth2FlowStateRecord`-Einträge periodisch löscht (z. B. als Erweiterung eines bereits existierenden Cleanup-/Hosted-Service — suche zuerst nach `IHostedService`-Implementierungen mit `grep -rn "IHostedService" src/VertexBPMN.Api` und hänge dort an, statt einen komplett neuen Hosted Service zu registrieren, wenn ein passender Cleanup-Service bereits existiert).

### 2.5 API-Controller

Neue Datei `VertexBPMN.Api/Controllers/OAuth2CredentialController.cs`:
- `POST /api/credentials/{id}/oauth2/authorize` — Body: `OAuth2AuthorizationConfig`. Ruft `StartAuthorizationAsync` auf, gibt `{ redirectUrl, state }` zurück.
- `GET /api/credentials/oauth2/callback?state=...&code=...` — Ruft `CompleteAuthorizationAsync` auf. Bei Erfolg: Redirect auf eine Studio-Erfolgsseite (z. B. `/studio/credentials?oauth2=success`), bei Fehler entsprechend `?oauth2=error`.

Gleiches Auth-Schema und Fehlerbehandlungsmuster wie `CredentialController.cs` übernehmen — den Callback-Endpunkt aber **ohne** JWT/API-Key-Pflicht lassen (er wird vom externen OAuth2-Provider per Browser-Redirect aufgerufen), dafür MUSS die State-Prüfung (existiert, nicht abgelaufen, gehört zu genau einem Credential) als alleinige Absicherung greifen — analog zur Secret-Hash-Prüfung in `WorkflowTriggerService.InvokeAsync`.

### 2.6 Integration in `VertexConnectorServiceTaskHandler`

In `Application/Connectors/ConnectorRuntime.cs`, Methode `ResolveSecretAsync` (Zeile 327): Wenn das aufgelöste `CredentialRecord.Type == "oauth2"` ist, statt `ResolveSecretAsync(tenantId, credentialId, secretKey, ct)` direkt `IOAuth2CredentialFlowService.ResolveValidAccessTokenAsync(tenantId, credentialId, ct)` aufrufen (Refresh passiert transparent). Dazu `IOAuth2CredentialFlowService` in den Konstruktor von `VertexConnectorServiceTaskHandler` injizieren.

### 2.7 Studio-UI (grobe Anleitung, kein Pflicht-Scope für den ersten PR)

In `VertexBPMN.Studio/Components/` (finde die bestehende Credential-Verwaltungskomponente, vermutlich unter `Components/Modeling/` oder einem `Credentials`-Unterordner) einen "Connect"-Button für Credentials vom Typ `oauth2` ergänzen, der `POST /api/credentials/{id}/oauth2/authorize` aufruft und `window.open(redirectUrl)` ausführt (JS-Interop, siehe bestehende Verwendung von `IJSRuntime` im Studio-Projekt als Vorbild).

### 2.8 Tests

- `tests/VertexBPMN.Tests/Unit/Infrastructure/OAuth2CredentialFlowServiceTests.cs`: State-Erzeugung, State-Ablauf, erfolgreicher Code-Tausch (HTTP-Aufruf mocken), Refresh-Logik (Token kurz vor Ablauf → Refresh wird ausgelöst; Token noch lange gültig → kein Refresh-Call).
- `tests/VertexBPMN.Tests/Integration/Api/OAuth2CredentialApiTests.cs`: kompletter Flow gegen einen Test-OAuth2-Server-Mock.

### 2.9 Akzeptanzkriterien Phase 2

- [ ] Ein Credential vom Typ `oauth2` lässt sich anlegen (mit `client_id`, `client_secret` als initiale Secrets).
- [ ] `StartAuthorizationAsync` liefert eine korrekt zusammengesetzte Redirect-URL.
- [ ] `CompleteAuthorizationAsync` speichert `access_token`/`refresh_token`/`expires_at` verschlüsselt im bestehenden Credential-Secret-Store.
- [ ] Ein abgelaufener Access-Token wird bei `ResolveValidAccessTokenAsync` automatisch per Refresh-Token erneuert, ohne dass der Connector-Aufruf fehlschlägt.
- [ ] Klartext-Tokens erscheinen zu keinem Zeitpunkt in Audit-Logs oder API-Responses (Stichprobe: `grep` über generierte `DetailsJson`-Audit-Einträge).

---

## Phase 3: Execution-Inspektor pro Task (Input/Output-Snapshots)

**Ziel:** Nach jeder Service-Task-Ausführung Input- und Output-Variablen als durchsuchbaren, redaktierten Snapshot persistieren, sichtbar über einen neuen API-Endpunkt (Studio-UI-Anbindung ist optionaler Folgeschritt).

### 3.1 Ist-Zustand (bereits vorhanden, NICHT neu bauen)

- `HistoryEvent` (Entity, `DbSet<HistoryEvent> HistoryEvents`) — hat bereits ein freies `Data`-Feld (JSON-String), `ElementId`, `ProcessInstanceId`, `TenantId`, `EventType`, `Timestamp`. **Neue Snapshot-Daten gehen hier hinein, keine neue Tabelle.**
- `ConnectorRedactionPolicy` (`Application/Connectors/ConnectorRuntime.cs`, Zeile 91) — Secret-Maskierung, wiederverwenden.
- `JobExecutorService.cs` — führt `handler.ExecuteAsync(payload.Attributes, payload.Variables, ct)` aus (Zeile 105). Das ist der Einhängepunkt für Vorher/Nachher-Snapshots.
- `PersistentVisualDebugStepService.cs` — schreibt bereits `HistoryEvent`s mit `EventType = "VISUAL_DEBUG_STEP_OVER"`, aber ohne Variableninhalte. Referenzmuster für den Schreibvorgang selbst.

### 3.2 Feature-Flag (bestehende Infrastruktur nutzen)

Prüfe `VertexBPMN.Api/Features/` — dort existiert laut Repo-Struktur bereits ein Feature-Flag-System (`FeatureFlagRecord` im `BpmnDbContext`). Der neue Snapshot-Mechanismus MUSS hinter einem Feature-Flag stehen, Vorschlag: `"task-io-snapshots"`. Lies zuerst `Domain/Entities/FeatureFlagRecord.cs` und den zugehörigen Service, um das exakte Muster zum Abfragen eines Flags zu übernehmen (vermutlich `IFeatureFlagService.IsEnabledAsync(tenantId, "task-io-snapshots", ct)` o. ä. — Namen im Code verifizieren, nicht raten).

### 3.3 Neue/geänderte Dateien

**3.3.1** `VertexBPMN.Application/TaskIoSnapshotRecorder.cs` (neue Datei)

```csharp
namespace VertexBPMN.Application;

public interface ITaskIoSnapshotRecorder
{
    Task RecordAsync(Guid processInstanceId, string elementId, string tenantId,
        IReadOnlyDictionary<string, object> input, IReadOnlyDictionary<string, object>? output,
        bool success, string? errorMessage, CancellationToken cancellationToken = default);
}

public sealed class TaskIoSnapshotRecorder(
    BpmnDbContext db,
    ConnectorRedactionPolicy redaction,
    IFeatureFlagService featureFlags) : ITaskIoSnapshotRecorder
{
    public async Task RecordAsync(Guid processInstanceId, string elementId, string tenantId,
        IReadOnlyDictionary<string, object> input, IReadOnlyDictionary<string, object>? output,
        bool success, string? errorMessage, CancellationToken cancellationToken = default)
    {
        if (!await featureFlags.IsEnabledAsync(tenantId, "task-io-snapshots", cancellationToken)) return;

        var redactedInput = redaction.Redact(input);
        var redactedOutput = output is null ? null : redaction.Redact(output);

        db.HistoryEvents.Add(new HistoryEvent
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = processInstanceId,
            EventType = "TASK_IO_SNAPSHOT",
            Timestamp = DateTime.UtcNow,
            ElementId = elementId,
            TenantId = tenantId,
            Data = JsonSerializer.Serialize(new { input = redactedInput, output = redactedOutput, success, errorMessage })
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
```

**Wichtig:** `ConnectorRedactionPolicy.Redact` erwartet `IReadOnlyDictionary<string, object>` — prüfe die exakte Signatur in `ConnectorRuntime.cs` Zeile 95 vor der Übernahme; passe den Typ hier exakt an, falls er von obiger Annahme abweicht.

**3.3.2** Änderung in `VertexBPMN.Application/JobExecutorService.cs`

An der Stelle, wo `handler.ExecuteAsync(payload.Attributes, payload.Variables, stoppingToken)` aufgerufen wird (Zeile 105):

```csharp
var inputSnapshot = new Dictionary<string, object>(payload.Variables);
Exception? executionError = null;
try
{
    await handler.ExecuteAsync(payload.Attributes, payload.Variables, stoppingToken);
}
catch (Exception ex)
{
    executionError = ex;
    throw;
}
finally
{
    var outputSnapshot = new Dictionary<string, object>(payload.Variables); // Variables-Dictionary wird von Handlern mutiert (siehe VertexConnectorServiceTaskHandler, Zeile 316-320)
    await _taskIoSnapshotRecorder.RecordAsync(
        job.ProcessInstanceId, job.ActivityId, job.TenantId ?? "default",
        inputSnapshot, outputSnapshot, executionError is null, executionError?.Message, stoppingToken);
}
```

Passe Variablennamen (`job`, `payload`, `_taskIoSnapshotRecorder`) an die tatsächlichen lokalen Bezeichner in `JobExecutorService.cs` an — lies die Methode vollständig, bevor du diesen Block einfügst, da der exakte Kontext (Scope, verfügbare Variablen) im obigen Codeausschnitt nicht vollständig sichtbar war.

### 3.4 DI-Registrierung

```csharp
services.AddScoped<ITaskIoSnapshotRecorder, TaskIoSnapshotRecorder>();
```
in `VertexBPMN.Application/ApplicationModule.cs` (Scoped, da `BpmnDbContext` injiziert wird — NICHT Singleton).

### 3.5 API-Controller

Neuer Endpunkt, entweder als neue Methode in einem bestehenden History-Controller (prüfe `VertexBPMN.Api/Controllers/` auf einen vorhandenen `HistoryController.cs` oder ähnlich) oder neue Datei `TaskIoSnapshotController.cs`:
- `GET /api/process-instances/{id}/tasks/{elementId}/io-snapshots?tenantId=...` — liest `HistoryEvent`s mit `EventType == "TASK_IO_SNAPSHOT"` gefiltert nach `ProcessInstanceId` und `ElementId`, deserialisiert `Data`, gibt Liste zurück (neuestes zuerst).

### 3.6 CLI-Erweiterung für `test-run` (Pin-Data-Äquivalent)

Finde die bestehende `test-run`-Implementierung in `CliApplication.cs`. Erweitere sie um ein Flag `--use-recorded-outputs`: Wenn gesetzt, lädt der Testlauf für jeden Service-Task den letzten `TASK_IO_SNAPSHOT` mit gleichem `ElementId` aus einer vorherigen Instanz (gleicher `ProcessDefinitionKey`) und injiziert dessen `output`-Werte direkt in die Variablen, statt den echten Connector aufzurufen. Das ist ein reiner CLI-/Test-Runner-Eingriff, kein Runtime-Verhalten in Produktion — stelle sicher, dass dieser Pfad NICHT im normalen `JobExecutorService` landet, sondern nur im Test-Runner-Codepfad des `execute`/`test-run`-Befehls.

### 3.7 Tests

- `tests/VertexBPMN.Tests/Unit/Application/TaskIoSnapshotRecorderTests.cs`: Feature-Flag aus → kein Event geschrieben; Feature-Flag an → Event mit redaktierten Werten; ein Wert mit Key `"apiToken"` wird zu `"***"` maskiert.
- `tests/VertexBPMN.Tests/Integration/Api/TaskIoSnapshotApiTests.cs`: End-to-End — Prozess mit Service-Task starten, Snapshot über API abrufen, Redaction verifizieren.

### 3.8 Akzeptanzkriterien Phase 3

- [ ] Bei aktivem Feature-Flag wird pro Service-Task-Ausführung genau ein `HistoryEvent` vom Typ `TASK_IO_SNAPSHOT` geschrieben.
- [ ] Secrets (Keys mit `secret`/`token`/`password`/etc.) erscheinen nie im Klartext im gespeicherten `Data`-JSON.
- [ ] Bei inaktivem Feature-Flag entsteht kein zusätzlicher Datenbank-Write (Performance-Neutralität für Bestandsnutzer ohne das Feature).
- [ ] `GET .../io-snapshots` liefert die Snapshots in absteigender zeitlicher Reihenfolge.

---

## Phase 4: Polling-Trigger-Quellen

**Ziel:** Prozessinstanzen können durch periodisches Abfragen eines externen Zustands (z. B. „neue Zeile in einer Tabelle") gestartet werden, nicht nur durch Webhooks.

### 4.1 Ist-Zustand (bereits vorhanden, NICHT neu bauen)

- `WorkflowTriggerService.InvokeAsync` (`Application/WorkflowTriggerService.cs`, Zeile 78) — startet eine Prozessinstanz anhand von `ProcessDefinitionKey` + Variablen. **Wiederverwenden als letzten Schritt**, nicht neu erfinden.
- `IJobRepository`/`Job`-Entity (`Domain/Entities/Job.cs`, `Domain/Interfaces/IJobRepository.cs`) — Lease-Pattern (`LockOwner`, `LockedUntil`, `DueDate`, `TryLeaseAsync`). **Konzeptionell wiederverwenden, aber NICHT die `Job`-Tabelle selbst**, da `Job.ProcessInstanceId` non-nullable ist und eine `ProcessInstance`-Navigation voraussetzt (Zeile 9 und 16 in `Job.cs`) — ein Polling-Trigger existiert per Definition, bevor eine Instanz gestartet wurde.
- `JobExecutorService.cs` — Referenzmuster für „fällige Einträge laden → lease → ausführen → Fehlerbehandlung/Retry".

### 4.2 Entscheidung: Neue eigenständige Tabelle statt Wiederverwendung von `Job`

**Entscheidung getroffen:** Neue Entity `PollingTriggerRecord`, strukturell an `Job` angelehnt, aber ohne Bindung an eine existierende `ProcessInstance`.

### 4.3 Neue Dateien

**4.3.1** `VertexBPMN.Domain/Entities/PollingTriggerRecord.cs`

```csharp
namespace VertexBPMN.Domain.Entities;

public sealed class PollingTriggerRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public string ConnectorType { get; set; } = string.Empty; // z.B. "http" - wiederverwendet IConnectorExecutor
    public string ConnectorAttributesJson { get; set; } = "{}"; // gleiche Attribute-Struktur wie vertex:connector.* auf Service-Tasks
    public string? CredentialId { get; set; }
    public int IntervalSeconds { get; set; } = 60;
    public string CursorStateJson { get; set; } = "{}"; // z.B. { "lastSeenId": "..." } oder { "lastSeenTimestamp": "..." }
    public bool Enabled { get; set; } = true;
    public DateTime? NextDueAt { get; set; }
    public string? LockOwner { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
```

**4.3.2** `VertexBPMN.Domain/Interfaces/IPollingTriggerService.cs` — CRUD-Interface analog `IWorkflowTriggerService` (List/Get/Create/Update/Delete). Kopiere die Struktur von `Domain/Interfaces` für `IWorkflowTriggerService`, falls vorhanden (`grep -rn "IWorkflowTriggerService" src/VertexBPMN.Domain`), als direktes Vorbild.

**4.3.3** `VertexBPMN.Domain/Interfaces/IPollingTriggerRepository.cs` — analog `IJobRepository`, inklusive `ListDueAsync(DateTime asOf, ...)` und `TryLeaseAsync`.

**4.3.4** `VertexBPMN.Infrastructure/Persistence/Repositories/PollingTriggerRepository.cs` — EF-Implementierung.

**4.3.5** `VertexBPMN.Application/PollingSchedulerService.cs` — `IHostedService`/`BackgroundService`, strukturell an `JobExecutorService` angelehnt:

```csharp
public sealed class PollingSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<PollingSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPollingTriggerRepository>();
            var connectorRuntime = scope.ServiceProvider.GetRequiredService<IConnectorRuntime>();
            var triggerService = scope.ServiceProvider.GetRequiredService<IWorkflowTriggerService>();
            // Für jeden fälligen Eintrag (ListDueAsync):
            //   1. TryLeaseAsync (workerId = Environment.MachineName + Guid, lockedUntil = UtcNow + 2 Minuten)
            //   2. ConnectorExecutionContext bauen aus ConnectorAttributesJson + CredentialId
            //   3. connectorRuntime.ExecuteAsync aufrufen
            //   4. Ergebnis mit gespeichertem CursorStateJson vergleichen (Cursor-Vergleichslogik ist connectorspezifisch -
            //      für Phase 4 reicht ein einfacher generischer Vergleich: wenn Output einen Wert unter dem im
            //      ConnectorAttributesJson konfigurierten Feld "vertex:polling.cursorField" enthält, der größer/neuer
            //      ist als der gespeicherte Cursor, gilt das als "neue Daten gefunden")
            //   5. Bei neuen Daten: triggerService-Äquivalent aufrufen, um eine neue Instanz zu starten
            //      (WorkflowTriggerService.InvokeAsync erwartet ein Secret - für interne Aufrufe aus dem Scheduler
            //      NICHT den Secret-Pfad verwenden, sondern IRuntimeService.StartProcessByKeyAsync DIREKT aufrufen,
            //      das WorkflowTriggerService intern ebenfalls nutzt, siehe Zeile 95 in WorkflowTriggerService.cs)
            //   6. CursorStateJson aktualisieren, NextDueAt = UtcNow + IntervalSeconds, LastPolledAt setzen, Lease freigeben
            //   7. Bei Fehler: ConsecutiveFailures erhöhen, exponentielles Backoff auf NextDueAt anwenden (gleiche
            //      Formel wie ConnectorRuntime.ExecuteAsync: InitialDelay * 2^(ConsecutiveFailures-1), gedeckelt)
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Scheduler-Tick-Intervall, NICHT das Polling-Intervall selbst
        }
    }
}
```

**Wichtiger Hinweis für den Agenten:** Verwende für den eigentlichen Prozessstart `IRuntimeService.StartProcessByKeyAsync` (siehe Aufruf in `WorkflowTriggerService.cs` Zeile 95-96) DIREKT, nicht `WorkflowTriggerService.InvokeAsync`, da Letzteres ein Secret erwartet, das für einen internen Scheduler-Aufruf nicht sinnvoll ist. Prüfe die exakte Signatur von `IRuntimeService.StartProcessByKeyAsync` in `Domain/Interfaces/IRuntimeService.cs` vor der Verwendung.

### 4.4 Migration

Neue Migration `<timestamp>_AddPollingTriggers.cs` nach Muster 0.5, Tabelle `PollingTriggers` mit allen Feldern aus 4.3.1, Index auf `(TenantId, Enabled, NextDueAt)` für effizientes `ListDueAsync`.

### 4.5 DI-Registrierung

```csharp
// InfrastructureModule.cs
services.AddScoped<IPollingTriggerRepository, PollingTriggerRepository>();
services.AddScoped<IPollingTriggerService, PersistentPollingTriggerService>();

// ApplicationModule.cs oder Api/Program.cs (dort, wo andere BackgroundService/IHostedService registriert sind - suche mit grep -rn "AddHostedService" src/VertexBPMN.Api/Program.cs)
services.AddHostedService<PollingSchedulerService>();
```

### 4.6 API-Controller

Neue Datei `VertexBPMN.Api/Controllers/PollingTriggerController.cs`, CRUD-Endpunkte analog zu `WorkflowTriggerController.cs` (Datei suchen und als Vorbild nehmen): `GET/POST /api/polling-triggers`, `PUT/DELETE /api/polling-triggers/{id}`, zusätzlich `POST /api/polling-triggers/{id}/poll-now` für manuelles Anstoßen (nützlich zum Testen ohne auf das Intervall zu warten).

### 4.7 CLI

Neuer Top-Level-Befehl `polling-trigger` in `CliApplication.cs`, gleiches Muster wie `trigger` (Zeile 119-121 delegiert an `ExecuteTriggerCommandAsync` — analog `ExecutePollingTriggerCommandAsync` schreiben mit Subcommands `create`, `list`, `enable`, `disable`, `delete`, `poll-now`).

### 4.8 Tests

- `tests/VertexBPMN.Tests/Unit/Application/PollingSchedulerServiceTests.cs`: fälliger Trigger wird geleast und ausgeführt; Cursor-Vergleich erkennt neue Daten korrekt; bei Fehler wird `ConsecutiveFailures` erhöht und Backoff angewendet; zwei parallele Scheduler-Instanzen (simuliert) leasen denselben Trigger nicht doppelt (Idempotenz-Test, wichtig für Mehrfach-Replikate laut bestehendem „mehrere API-Replikate"-Feature).
- `tests/VertexBPMN.Tests/Integration/Api/PollingTriggerApiTests.cs`: CRUD + `poll-now`-Endpunkt gegen einen Test-HTTP-Server, der bei zweitem Aufruf neue Daten liefert → verifiziere, dass eine neue Prozessinstanz gestartet wurde.

### 4.9 Akzeptanzkriterien Phase 4

**Status: UMSGESETZT (Branch `VertexBPMN-n8n-Features-Umsetzungsplan`).** Gate: 817 tests / 816 ok / 0 failed. Die New-Data→Start-Semantik ist durch Unit-Tests belegt (der Connector-SSRF-Guard `ConnectorDestinationPolicy` lehnt Loopback-Ziele in CI pauschal ab — siehe Test-Kommentar in `PollingTriggerApiTests.cs`).

- [x] Ein Polling-Trigger mit `IntervalSeconds = 60` wird nach Ablauf des Intervalls automatisch erneut abgefragt. (Unit: `RunIteration_LeasesDueTrigger_AndStartsInstanceWhenNewData` — fälliger Trigger wird geleast und gepollt)
- [x] Bei erkannten neuen Daten wird genau eine neue Prozessinstanz mit den Output-Werten als Variablen gestartet. (Unit: `..._StartsInstanceWhenNewData` + `..._DoesNotStartInstanceWhenUnchanged`)
- [x] Bei unverändertem Zustand wird KEINE neue Instanz gestartet. (Unit: `..._DoesNotStartInstanceWhenUnchanged`)
- [x] Zwei gleichzeitig laufende `PollingSchedulerService`-Instanzen (z. B. bei mehreren API-Replikaten) leasen denselben fälligen Trigger nicht doppelt. (Unit: `..._SkipsTriggerAlreadyLeasedByAnotherWorker`)
- [x] Bei drei aufeinanderfolgenden Fehlern greift exponentielles Backoff, keine Endlosschleife von Sofort-Retries. (Unit: `..._OnFailure_IncrementsFailuresAndAppliesBackoff`)
- Integrationstest `PollingTriggerApiTests`: CRUD (create/get/list/update/delete) + `poll-now`-Ausführung gegen den realen Repository/Scheduler-Pfad. Ein realer Instanzstart über den http-Connector ist in CI durch den SSRF-Guard nicht möglich (Loopback-Pauschalablehnung); New-Data-Start ist daher unit-seitig abgedeckt.

---

## Reihenfolge-Empfehlung für die Umsetzung

1. **Phase 3** zuerst (kleinster Blast-Radius, keine neue externe Abhängigkeit, reine Erweiterung bestehender History-Infrastruktur).
2. **Phase 1** danach (baut auf bestehendem Connector-Runtime auf, kein neuer Hintergrunddienst).
3. **Phase 4** danach (neuer Hintergrunddienst, aber klar abgegrenzt).
4. **Phase 2** zuletzt (höchste Sicherheitsrelevanz — OAuth2-Flows sollten erst angegangen werden, wenn das Team/der Agent mit den übrigen Repo-Konventionen vertraut ist).

Jede Phase MUSS einzeln durch Build+Test (Abschnitt 0.1) bestätigt werden, bevor die nächste beginnt. Kein Phase-Übergang bei rotem Build.

## Vor Abschluss jeder Phase: Selbstprüfung durch den Agenten

- [ ] Folgt jede neue Datei der in Abschnitt 0 beschriebenen Schichten-Architektur?
- [ ] Ist jede neue Query nach `TenantId` gefiltert?
- [ ] Wurde `ConnectorRedactionPolicy` überall wiederverwendet, wo Secrets in Logs/History/API-Responses landen könnten?
- [ ] Wurde für jede neue Migration die Snapshot-Datei aktualisiert?
- [ ] Existieren Unit- UND Integrationstests für jede neue Service-Klasse bzw. jeden neuen Controller?
- [ ] Wurden die Akzeptanzkriterien der jeweiligen Phase einzeln abgehakt?
