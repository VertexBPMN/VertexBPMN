# Phase 3/4 Security Boundary Audit — Script-, Connector-, Webhook-, OAuth2- & Redaction-Grenzen

- **Repo**: VertexBPMN, branch `master` (HEAD `375a456`)
- **Date**: 2026-09-08
- **Scope**: Phase 4 (Script-/Connector-Grenzen) + Phase 3 (OAuth2/Webhook/Redaction)
- **Method**: manual source review of `src/` + test-coverage scan of `tests/`; all findings cite real `file:line` evidence.

Severity legend: **INFO** / **LOW** / **MEDIUM** / **HIGH**.

---

## (A) SCRIPT SANDBOX

### A1. JavaScript/Jint pfad ist ressourcenbegrenzt (2 s / 8 MB) — VERIFIZIERT
`src/VertexBPMN.Infrastructure/Scripting/ScriptTaskExecution.cs:83`:
```csharp
var engine = new Jint.Engine(cfg => cfg.Strict().LimitMemory(8_000_000).TimeoutInterval(TimeSpan.FromSeconds(2)));
```
Der JavaScript-Pfad ist der **Default** (`scriptFormat` defaultet auf `"JavaScript"`, Zeile 37–39; Default-Branch Zeile 48–52) und ist durch `LimitMemory(8_000_000)` (8 MB) und `TimeoutInterval(2 s)` begrenzt. **Severity: INFO** (Begrenzung vorhanden, verifiziert).

### A2. Roslyn C#-Pfad ist NICHT sandboxed — BESTÄTIGT (HIGH)
`src/VertexBPMN.Infrastructure/Scripting/ScriptTaskExecution.cs:69-77`:
```csharp
private static readonly ScriptOptions RoslynOptions = ScriptOptions.Default
    .AddReferences(typeof(object).Assembly)
    .AddImports("System", "System.Collections.Generic");

private static Task<object?> ExecuteCSharpAsync(string code, IDictionary<string, object> variables, CancellationToken ct)
{
    // Hinweis: Roslyn ist keine Sicherheits-Sandbox. Untrusted Code nur in isolierten Umgebungen ausführen.
    return CSharpScript.EvaluateAsync<object?>(code, RoslynOptions, globals: new Globals(variables), cancellationToken: ct);
}
```
`CSharpScript.EvaluateAsync` führt fulles .NET im Prozess aus: **keine** Memory-/Timeout-/AppDomain-/Reflection-/IO-/Netzwerk-Beschränkung. Die einzige Barriere ist funktional, nicht sicherheitsrelevant: Roslyn wird nur ausgeführt, wenn der BPMN-Autor explizit `scriptFormat="C#"` setzt (`ScriptTaskExecution.cs:42-47`). Ein Tenant-uploaded BPMN mit `scriptFormat=C#` erhält **beliebige Code-Ausführung** im Engine-Prozess (RCE): Dateisystem, Prozesse, Credentials/Secrets im Prozess-Speicher.
**Severity: HIGH.**

### A3. `Runtime:Scripts:Enabled` default `true` ist ein Risiko für untrusted Tenant-BPMN — (MEDIUM/HIGH)
`src/VertexBPMN.Application/RepositoryService.cs:25`:
```csharp
_scriptsEnabled = configuration.GetValue("Runtime:Scripts:Enabled", true);
```
Das Deployment-Gate (Zeile 45–46) wirft nur, wenn Scripts **deaktiviert** sind:
```csharp
if (!_scriptsEnabled && model.Tasks.Any(task => task.Type.Equals("scriptTask", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("BPMN script tasks are disabled for the in-process production runtime.");
```
- **Default ist `true`** → scriptTask-Deployments sind ohne explizite Konfiguration erlaubt. `k8s-prerequisites.yaml:18` setzt `Runtime__Scripts__Enabled: "false"`, aber nur in der Beispiel-K8s-Umgebung.
- Bei `Enabled=true` und untrusted Tenant-Deployments skaliert das Risiko: JS ist begrenzt (2 s/8 MB), aber **C# ist voll unsandboxed (A2)**. Zusätzlich rufen `DistributedProcessEngine.cs:1484` und `PersistentProcessExecutionRuntime.cs:876` `ScriptTaskExecution.TryHandleScriptTaskAsync(...)` **ohne** lokale `Scripts:Enabled`-Prüfung auf — die einzige Gate ist das Deployment (RepositoryService). Jede andere Deploy-Route, die RepositoryService umgeht, führt Skripte unbedingt aus.
**Severity: MEDIUM–HIGH** (stark abhängig von der Mandanten-Haltung; bei untrusted Tenant = HIGH, da mit A2 kombinierbar).

### A4. Testabdeckung (A)
- `tests/VertexBPMN.Tests/Integration/Handlers/ScriptTaskTests.cs`: `CSharp_ScriptTask_AddsNumbers_AndStoresResult`, `JavaScript_ScriptTask_Works_With_Jint`, `ScriptTask_Should_Update_ProcessVariable`, `ScriptTask_Should_Support_JavaScript`.
- `tests/.../Parsing/Runtime/RuntimeProjectionUserAndScriptEnhancementsTests.cs` + `RuntimeProjectionScriptAndUserTaskTests.cs` (Projektion).
- **LÜCKEN**: kein Test für die 2-s/8-MB-Grenzen, kein Test für das `Scripts:Enabled` Deployment-Gate, kein Test der Roslyn-Unsandboxed-Eigenschaft, kein Negativtest für unbounded C#.

---

## (B) CONNECTOR / SSRF

`ConnectorDestinationPolicy` wird als Singleton registriert und in `ConnectorRuntime` injiziert:
- `src/VertexBPMN.Application/Extensions/ServiceTaskRegistryExtensions.cs:154` → `services.AddSingleton<ConnectorDestinationPolicy>();`
- `src/VertexBPMN.Application/Connectors/ConnectorRuntime.cs:245,249-250` → `ConnectorDestinationPolicy? destinationPolicy = null` … `if (destinationPolicy is not null) await destinationPolicy.ValidateAsync(...)` (per DI non-null).

### B1. Private-/Loopback-/Link-Local-Blacking — VORHANDEN (stark)
`src/VertexBPMN.Application/Connectors/ConnectorRuntime.cs:213-229` `IsForbiddenAddress`:
```csharp
if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
    || address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None)
    || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
    return true;
if (address.AddressFamily != AddressFamily.InterNetwork)
    return false;
var bytes = address.GetAddressBytes();
return bytes[0] == 10            // 10/8
       || bytes[0] == 127        // 127/8
       || bytes[0] == 0          // 0/8
       || bytes[0] == 169 && bytes[1] == 254   // 169.254/16 link-local
       || bytes[0] == 172 && bytes[1] is >= 16 and <= 31   // 172.16/12
       || bytes[0] == 192 && bytes[1] == 168   // 192.168/16
       || bytes[0] >= 224;       // multicast/reserved
```
Abgedeckt: 10/8, 172.16/12, 192.168/16, 127.0.0.0/8, Link-Local, Loopback, IPv6-Link/Site-Local, Multicast, Any/None. **Severity: INFO/positiv** (Kern-SSRF-Ranges geblockt).

### B2. Host-Allowlist / konfigurierbare Base-URL-Allowlist — VORHANDEN
`src/VertexBPMN.Application/Connectors/ConnectorRuntime.cs:202-211` `EnsureAllowed` liest `ConnectorRuntime:AllowedHttpHosts` (http/webhook/slack/ai, Zeile 115), `AllowedSmtpHosts` (email/smtp, Zeile 121), `AllowedDatabaseHosts` / `AllowedDatabaseProviders` / `AllowedDatabaseFileRoots` (Zeile 148, 159); `AllowedCredentialHosts` separat in `VertexConnectorServiceTaskHandler.IsAllowedCredentialHost` (Zeile 358–362). Wildcard `*.example.com` unterstützt. **Severity: INFO/positiv.**

### B3. DNS-Rebinding / TOCTOU — NICHT vollständig mitigiert (LOW)
`ValidateNetworkHostAsync` löst Host einmal via `Dns.GetHostAddressesAsync` auf (Zeile 185–190) und blockt verbotene Adressen, aber die eigentliche Verbindung (`HttpConnectorExecutor` → `client.SendAsync`, `BuiltInConnectorExecutors.cs:30`) re-resolved den Hostnamen erneut → klassisches DNS-Rebinding-Fenster (Validate-IP ≠ Connect-IP). Kein Pin der aufgelösten IP auf die Verbindung.
**Severity: LOW–MEDIUM.**

### B4. Redirect- (Location-) Policy — FEHLT (MEDIUM/HIGH)
Das `HttpClient` wird als Default und ohne Handler registriert: `ServiceTaskRegistryExtensions.cs:139` → `services.AddSingleton<HttpClient>();`. Der Default `HttpClientHandler` hat `AllowAutoRedirect=true`. `HttpConnectorExecutor.ExecuteAsync` (`BuiltInConnectorExecutors.cs:30`) folgt Redirects automatisch. `ValidateNetworkHostAsync` prüft nur das **initiale** Endpoint-Host, nicht Redirect-Ziele → ein 30x-Redirect auf eine private IP umgeht B1/B2.
**Es existiert KEINE Redirect-Policy.** **Severity: MEDIUM–HIGH.**

### B5. OAuth2-Token-/Authorization-Endpoints unterliegen NICHT der Connector-SSRF-Policy (MEDIUM)
`OAuth2CredentialFlowService` postet an `record.TokenUrl` (`OAuth2CredentialFlowService.cs:118`, `:206`) und `config.AuthorizationUrl` (Zeile 64) ohne `ConnectorDestinationPolicy`-Prüfung, ohne Allowlist und ohne Private-IP-Blocking. `ConnectorDestinationPolicy.ValidateAsync` matcht nur `context.Type` http/webhook/slack/ai/email/database (`ConnectorRuntime.cs:107-131`) und wird auf dem OAuth2-Pfad nie aufgerufen. **Severity: MEDIUM.**

### B6. Testabdeckung (B)
- `tests/VertexBPMN.Tests/Unit/Application/ConnectorRuntimeTests.cs`: `Handler_ResolvesCredentialOnlyForAllowedHost_AndAuditsWithoutSecret`, `Handler_RejectsCredentialForHostOutsideAllowlistBeforeResolution`, `Handler_RejectsSmtpCredentialForHostOutsideAllowlistBeforeResolution`, `Runtime_RetriesAndRedactsSensitiveOutputs`, `Runtime_MapsTimeoutWithoutLeakingExceptionDetails`, `RateLimit_SpacesExecutionsForSameTenantTypeAndHost`, `HttpExecutor_ResolvesPathPlaceholdersFromVariables`.
- `tests/.../Unit/Infrastructure/Phase2ProductionConfigurationTests.cs`: testet `ConnectorDestinationPolicy` private/leer.
- `tests/.../Integration/Api/PollingTriggerApiTests.cs`: dokumentiert, dass der SSRF-Guard Loopback hart ablehnt.
- **LÜCKEN**: kein Test für Redirect-Follow-SSRF (B4), kein DNS-Rebinding-/TOCTOU-Test (B3), kein Test, dass Redirect-Ziele geprüft werden, kein OAuth2-token_url-SSRF-Test (B5).

---

## (C) WEBHOOK-AUTH (Trigger-Secret / HMAC)

### C1. Trigger-Secret (Klartext-Secret in Header) — Rejected bei fehlendem Secret (VERIFIZIERT)
`src/VertexBPMN.Application/WorkflowTriggerService.cs:185-188`:
```csharp
else if (string.IsNullOrWhiteSpace(triggerSecret) || !CryptographicEquals(trigger.SecretHash, HashSecret(triggerSecret)))
{
    return new(WorkflowTriggerInvocationStatus.InvalidSecret);
}
```
- **Fehlendes Secret wird abgelehnt** (`IsNullOrWhiteSpace` → `InvalidSecret`). Der Header `X-VertexBPMN-Trigger-Secret` wird aus dem Header gelesen (`WebhookIngressController.cs:17`), nie aus dem Body.
- Secret wird nur gehasht gespeichert: `SecretHash = HashSecret(secret)` (`WorkflowTriggerService.cs:47,226-227`, SHA-256-Hex).
- **Konstantzeit-Vergleich**: `CryptographicOperations.FixedTimeEquals` (`WorkflowTriggerService.cs:229-231`).
**Severity: INFO/positiv.**

### C2. HMAC-SHA256-Webhooks — VERIFIZIERT
`WorkflowTriggerService.cs:177-184,233-242`:
```csharp
secret = await credentialService.ResolveSecretAsync(trigger.TenantId, trigger.CredentialId, ...);
if (string.IsNullOrWhiteSpace(secret) || !IsValidHmac(secret, payload.Span, signature))
    return new(WorkflowTriggerInvocationStatus.InvalidSecret);
```
`IsValidHmac` nutzt `HMACSHA256.HashData` (Zeile 240) + `CryptographicOperations.FixedTimeEquals` (Zeile 241); malformed/non-hex oder fehlende Signatur → `false` (Zeile 235–239). **Missing secret rejected.**
**Severity: INFO/positiv.**

### C3. Einmal-Verwendung / Replay-Schutz / Anti-Replay-Nonce — NICHT VORHANDEN (LOW–MEDIUM)
`InvokeWebhookAsync` ist das einzige Gate; es gibt weder Nonce, Zeitstempel noch Einmal-Verwendung auf dem Invoke-Pfad. Das "one-time" Konzept (`WorkflowTriggerService.cs:158-161`) bedeutet nur, das Secret wird beim Deploy **einmalig** im Header offengelegt und nur als Hash gespeichert — nicht, dass ein aufgefangenes Secret nur einmal nutzbar ist. Ein abgefangenes Secret/Signatur kann **beliebig oft replay-bar** werden (keine Replay-Erkennung). Der Ingress ist `[AllowAnonymous]` und nicht ratelimited (`WebhookIngressController.cs:13`).
**Severity: LOW–MEDIUM.**

### C4. Testabdeckung (C)
- `tests/VertexBPMN.Tests/Integration/Api/WorkflowTriggerApiTests.cs`: `BpmnWebhook_IsSynchronized_AndStartsProcessWithValidHmac`, `BpmnTriggerSecretWebhook_ExposesOneTimeSecretInDeployHeader_AndCanBeInvoked`, `Trigger_CanBeRegistered_Invoked_Disabled_AndListedWithoutSecret`.
- `tests/.../Integration/Sdk/VertexBpmnClientTests.cs`: `WorkflowTriggerLifecycle_IsAvailableThroughSdk`.
- **LÜCKEN**: kein Test für Invalid-/Missing-Secret-Ablehnung via Webhook, kein HMAC-Mismatch-Test, kein Replay-/Anti-Replay-Test.

---

## (D) OAUTH2 + REDACTION

### D1. OAuth2 `state` ist kryptographisch zufälliger Nonce (CSRF-Schutz) — VERIFIZIERT, aber NICHT an den Ursprungs-User gebunden (LOW)
`OAuth2CredentialFlowService.cs:268-276`:
```csharp
Span<byte> bytes = stackalloc byte[32];
RandomNumberGenerator.Fill(bytes);
return Convert.ToBase64String(bytes).Replace("+", "-", ...).Replace("/", "_", ...).TrimEnd('=');
```
32 Bytes via `RandomNumberGenerator` → kryptographisch zufällig. Der Status ist **Einmal-Verwendung** (nach Callback entfernt, Zeile 153) und tenant-/credential-gebunden (`OAuth2FlowStateRecord.cs:10-11`). Der Callback ist `[AllowAnonymous]` (`OAuth2Controller.cs:35`) und validiert nur Existenz + Ablauf (`CompleteAuthorizationAsync` Zeile 84–95).

**Aber**: `OAuth2FlowStateRecord.cs` enthält **kein User-/Principal-Feld** (nur State, TenantId, CredentialId, URLs, Timestamps). Der `state` ist nicht an den **ursprünglich autorisierenden User/Session** gebunden — CSRF-Schutz beruht allein auf Zufälligkeit + Einmalverwendung, nicht auf einer User-Bindung. **Severity: LOW** (Defense-in-Depth-Gap).

### D2. Flow-State Expiry/Cleanup — VORHANDEN (VERIFIZIERT, GOOD)
- TTL 10 min: `StateTtl = TimeSpan.FromMinutes(10)` (`OAuth2CredentialFlowService.cs:24`, `ExpiresAt = now.Add(StateTtl)` Zeile 60).
- Expired werden entfernt in `CompleteAuthorizationAsync` (Zeile 85–91), `PruneExpiredAsync` beim Start (Zeile 38, 256–266) und vom Background-Service `OAuth2FlowStateCleanupService` (stündlich `ExecuteDeleteAsync`, `OAuth2FlowStateCleanupService.cs:26-28`).
**Severity: INFO/positiv.**

### D3. Client-Secret-Rotation — UNTERSTÜTZT (VERIFIZIERT)
- `ICredentialService.RotateSecretAsync` (`ICredentialService.cs:9`), implementiert als API-Endpoint `CredentialController.RotateSecret` (`CredentialController.cs:95-106`) und CLI (`CliApplication.cs:415`).
- `OAuth2CredentialFlowService.RotateAsync` legt Tokens/`client_secret`-bezogene Secrets über `RotateSecretAsync` ab (`OAuth2CredentialFlowService.cs:242-254`), `client_secret` wird für Token-Request aus dem Credential-Store gelesen (Zeile 97–98).
**Severity: INFO/positiv.** Hinweis: kein dedizierter OAuth2-"Rotate Client Secret"-Regenerations-Flow; Rotation erfolgt über den generischen Credential-Store.

### D4. BpmnRedactionProcessor redigiert Secrets — ABER default AUS und NICHT auf dem Export-/Raw-XML-Pfad (MEDIUM)
- Processor vorhanden und funktional: `BpmnRedactionProcessor.ApplyRedaction` (`BpmnRedactionProcessor.cs:30-59`), Policy `RedactedAttributes`/`RedactedNamespaces`/`RedactedElements`/`PreserveAttributes` (`BpmnRedactionPolicies.cs:16-31`).
- **Aufruf**: `BpmnParser.ApplyPostProcessing` → `BpmnParser.cs:2926-2931` (`if (_options.RedactionPolicies != null) { new BpmnRedactionProcessor(...).ApplyRedaction(...) }`). Das ist der **Parse-/Projektions-Pfad**, nicht der Export-/Raw-XML-Pfad.
- **Default AUS**: `BpmnParserOptions.RedactionPolicies { get; init; }` ist `null` (`BpmnParserOptions.cs:224`) und wird nirgends aus Konfiguration/DI befüllt (kein `RedactionPolicies`-Binding in `Program.cs`/Modulen gefunden); zusätzlich ist `StripConfidentialData` default `false` (`BpmnRedactionPolicies.cs:11`). Redaction ist **opt-in und per default deaktiviert**.
- **Gilt nur für das geparste Model**, nicht für den gespeicherten/exportierten Roh-XML-String: Export-Endpoints liefern das unredigierte `def.BpmnXml` (z. B. `VertexProcessDefinitionController.cs:52`, `InspectorController.cs:53`); gespeicherte `ProcessDefinition.BpmnXml` (`RepositoryService.cs:57`) und Re-Deploy behalten Secrets.
- **Konjunktive**: Auf dem Log-/Snapshot-Pfad greift die **Connector**-Redaction (`ConnectorRedactionPolicy`, `ConnectorRuntime.cs:91-97`, angewandt Zeile 264 `redaction.Redact(raw.Outputs)`; von `TaskIoSnapshotRecorder` verwendet) — diese ist aktiv, aber nur für Connector-Output-Keys (`secret/token/password/authorization/apikey/api-key/connectionstring`).
**Severity: MEDIUM** (BPMN-Secret-Redaction praktisch inaktiv auf Export/Roh-XML und default aus).

### D5. Testabdeckung (D)
- OAuth2: `tests/VertexBPMN.Tests/Unit/Infrastructure/OAuth2CredentialFlowServiceTests.cs` (State-Expiry/Completion), `tests/.../Integration/Api/OAuth2FlowApiTests.cs` (Authorization/Callback). Kein expliziter CSRF/User-Binding-Test, kein State-Rotation-Cleanup-Test.
- Redaction: `tests/VertexBPMN.Tests/Parsing/Ecosystem/Phase12EcosystemTests.cs` → `PolicyBasedRedaction_StripsConfidentialExtensions`, `RedactionPolicies_PreserveNonSensitiveData`. `tests/.../Integration/Api/CredentialApiTests.cs` → `CredentialLifecycle_ReturnsMetadataOnly_AndWritesRedactedAudit`; `TaskIoSnapshotApiTests` → `IoSnapshots_StoredRedacted_AndListedNewestFirst`.
- **LÜCKEN**: kein Test, dass Redaction auf den Export-/Raw-XML-Pfad angewandt wird; kein Test für die "default aus"-Konfiguration; kein Test, dass OAuth2-token_url unter SSRF-Guard steht (B5).

---

## Übersicht: Findings nach Severity

| # | Section | Finding | Severity | Evidence |
|---|---------|---------|----------|----------|
| A2 | A | Roslyn C#-Skript unsandboxed (RCE) | **HIGH** | ScriptTaskExecution.cs:69-77 |
| A3 | A | `Runtime:Scripts:Enabled` default `true` + Engine ruft Skripte ohne lokale Gate auf | **MEDIUM–HIGH** | RepositoryService.cs:25,45-46; DistributedProcessEngine.cs:1484; PersistentProcessExecutionRuntime.cs:876 |
| B4 | B | Redirect-(Location-)-Policy fehlt; Default-HttpClient folgt Redirects → SSRF-Bypass auf private IPs | **MEDIUM–HIGH** | ServiceTaskRegistryExtensions.cs:139; BuiltInConnectorExecutors.cs:30; ConnectorRuntime.cs:202-211 |
| B5 | B | OAuth2 Authorization-/Token-URL ohne SSRF-Guard/Allowlist | **MEDIUM** | OAuth2CredentialFlowService.cs:118,206; ConnectorRuntime.cs:107 |
| D4 | D | BPMN-Redaction default aus + nur auf Parse-, nicht Export-/Raw-XML-Pfad | **MEDIUM** | BpmnParserOptions.cs:224; BpmnParser.cs:2926-2931; VertexProcessDefinitionController.cs:52 |
| B3 | B | DNS-Rebinding/TOCTOU nicht vollständig mitigiert | **LOW–MEDIUM** | ConnectorRuntime.cs:185-190 + BuiltInConnectorExecutors.cs:30 |
| C3 | C | Webhook: kein Anti-Replay-Nonce/Timestamp, kein Replay-Schutz, Ingress unratelimited | **LOW–MEDIUM** | WorkflowTriggerService.cs:171-195; WebhookIngressController.cs:13 |
| D1 | D | OAuth2 `state` nicht an Ursprungs-User-Gebunden | **LOW** | OAuth2FlowStateRecord.cs; OAuth2CredentialFlowService.cs:268-276 |
| A1 | A | Jint begrenzt 2 s/8 MB | INFO | ScriptTaskExecution.cs:83 |
| B1/B2 | B | Private-IP-Blocking + Host-Allowlist vorhanden | INFO | ConnectorRuntime.cs:213-229,202-211 |
| C1/C2 | C | Missing-Secret rejected, constant-time compare, HMAC | INFO | WorkflowTriggerService.cs:185,229-241 |
| D2/D3 | D | State-TTL/cleanup, Secret-Rotation | INFO | OAuth2CredentialFlowService.cs:24,256-266; ICredentialService.cs:9 |

**Kurzfazit**: Ein robuster, verifizierter Kern (Jint 2 s/8 MB, Private-IP-/Allowlist-SSRF-Guard, constant-time HMAC/Secret-Vergleich, Missing-Secret-Rejected, OAuth2-Random-State + TTL-Cleanup + Rotation). Offene Grenzen: Roslyn-C#-Skripte voll unsandboxed (HIGH), fehlende Redirect-Policy im SSRF-Schutz (MEDIUM–HIGH), OAuth2-Token-URL außerhalb des SSRF-Guards (MEDIUM), BPMN-Redaction default inaktiv und nicht auf dem Export-/Roh-XML-Pfad (MEDIUM). Testlücken: Redirect-/DNS-Rebinding-SSRF, Scripts-Enabled-Gate, Webhook-Missing/Invalid-Secret, Redaction-auf-Export, OAuth2-token_url-SSRF.
