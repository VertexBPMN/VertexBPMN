# Cloud Adapter Contract

VertexBPMN keeps cloud integrations behind configuration and provider-neutral application contracts. Cloud credentials must be supplied through the centralized `ISecretProvider`; they must not be embedded in BPMN definitions, source code, or committed settings.

## Provider configuration

Use one of the provider sections below when an adapter is enabled:

```json
{
  "Cloud": {
    "Provider": "Azure|Aws|Gcp",
    "Region": "",
    "Endpoint": ""
  },
  "AI": {
    "OpenAI": { "ApiKey": "" },
    "Anthropic": { "ApiKey": "" },
    "Google": { "ApiKey": "" }
  }
}
```

Production deployments should inject values through a secret manager or environment variables. The application must fail closed when a required provider credential is missing.

## Adapter requirements

Every cloud adapter must provide cancellation support, bounded retries for transient failures, structured logging without secret values, tenant-aware resource names, and an integration test using a local emulator or HTTP test server. Provider-specific packages belong in the integration project, not in the domain layer.

Until an adapter meets those requirements it must not be selected by default. The existing provider-neutral interfaces remain the supported extension point.
