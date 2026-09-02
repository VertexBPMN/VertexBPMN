# Connector Runtime

`vertex:connector` on a BPMN service task is executed by the registered connector runtime. The parser assigns the service-task implementation `vertex:connector`, and the runtime selects an executor by the extension's `type`.

## Built-in types

| Type | Execution |
|---|---|
| `http` | HTTP/HTTPS request |
| `webhook` | Outbound generic webhook |
| `slack` | Slack-compatible HTTP endpoint |
| `ai` | Authenticated AI-provider HTTP wrapper |
| `delay` | Cancellation-aware delay/timer helper |
| `email`, `smtp` | SMTP delivery |
| `database`, `db`, `postgresql`, `sqlserver`, `sqlite` | Parameterized ADO.NET command |

HTTP-derived connectors accept `endpoint`, `method`, `contentType`, and `body`. SMTP uses `smtpHost`, `smtpPort`, `ssl`, `from`, `to`, `subject`, and `body`. Database connectors use `provider`, `commandText`, and optional `commandTimeoutSeconds`; variables prefixed with `db.` become command parameters. Connection strings and authentication values must come from credentials, never BPMN attributes.

## Reliability and error mapping

`vertex:retryPolicy.maxAttempts`, `timeoutMs`, and `initialDelayMs` control bounded exponential retries. `vertex:connector.requestsPerSecond` controls the tenant/type/host rate limit. Failures are mapped to stable codes such as `timeout`, `network_error`, `authentication_error`, `rate_limited`, `remote_server_error`, `database_error`, and `smtp_error`.

## Credentials and redaction

Set `credentialRef` and optionally `secretKey` (default: `token`). Credentials are resolved through `ICredentialService` inside a server-side scope. For outbound HTTP and SMTP connectors, the destination host must be explicitly configured:

```json
{
  "ConnectorRuntime": {
    "AllowedCredentialHosts": [
      "api.example.com",
      "hooks.slack.com"
    ]
  }
}
```

An empty allowlist denies HTTP/SMTP credential transmission. Database connection strings are resolved server-side and passed directly to the configured ADO.NET provider; they never come from BPMN attributes. Secrets are excluded from results, process history fields, logs, exceptions, and audit details. Every execution records `connector.executed`; credential access separately records `credential.secret_resolved`.
