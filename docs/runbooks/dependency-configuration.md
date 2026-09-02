# Dependency configuration

VertexBPMN binds runtime dependency settings from the `Dependencies` configuration section. The same section can be used by the API, CLI, workers, and other hosts that compose `AddApplicationServices`.

## AI models

```json
{
  "Dependencies": {
    "Ai": {
      "Enabled": true,
      "DefaultProvider": "openai",
      "DefaultModel": "gpt-4o-mini",
      "Models": {
        "openai": {
          "Enabled": true,
          "Provider": "openai",
          "Model": "gpt-4o-mini",
          "ApiKeyEnvironmentVariable": "OPENAI_API_KEY"
        }
      }
    }
  }
}
```

`DefaultProvider` and `DefaultModel` are used when a BPMN task does not specify `ai:provider` or `ai:model`. API keys are never read from JSON; the configured environment variable name points to the secret in the host environment.

## Service task mappings

Existing built-in mappings remain registered by default. Add aliases, or disable mappings, without changing code:

```json
{
  "Dependencies": {
    "ServiceTasks": {
      "Enabled": true,
      "Disabled": [ "ai:anthropic" ],
      "Mappings": {
        "company:score": "CalculateScoreServiceTaskHandler",
        "company:ai": "AIServiceTaskHandler"
      }
    }
  }
}
```

Mapping values are explicit built-in handler names. Unknown handler names fail host construction with a configuration error. This avoids silently activating arbitrary types from configuration.

Supported handler names include `AIServiceTaskHandler`, `OpenAiServiceTaskHandler`, `AnthropicServiceTaskHandler`, `GeminiServiceTaskHandler`, `GenericAiServiceTaskHandler`, `ContextEnrichmentServiceTaskHandler`, `McpServiceTaskHandler`, `CalculateScoreServiceTaskHandler`, `CancelApplicationServiceTaskHandler`, `IssuePolicyServiceTaskHandler`, `RejectPolicyServiceTaskHandler`, and `SendGridServiceTaskHandler`.

## Interfaces

The `Interfaces` subsection controls optional application integrations:

```json
{
  "Dependencies": {
    "Interfaces": {
      "AiDecisionService": true,
      "McpAgentService": true,
      "LoadBalancing": true
    }
  }
}
```

`IAiDecisionService` is required by `DistributedProcessEngine` and should remain enabled for engine hosts. `IMcpAgentService` and `ILoadBalancingService` can be disabled in hosts that do not expose those integrations.

## MCP agents and transport

MCP agent clients are configured in the existing `McpAgents` section. The dependency switch controls whether `IMcpAgentService` is registered:

```json
{
  "Dependencies": {
    "Mcp": {
      "Enabled": true
    },
    "Interfaces": {
      "McpAgentService": true
    }
  },
  "McpAgents": [
    {
      "name": "NLP",
      "type": "REST",
      "url": "http://localhost:5000/api/nlp"
    }
  ]
}
```

`Dependencies.Mcp.Enabled` and `Dependencies.Interfaces.McpAgentService` must both be enabled for the client service. The API's gRPC MCP endpoint is a transport module and is controlled separately with `Modules.Grpc`.

## Plugins

The API loads plugins during startup through `IPluginManager`. Plugin assemblies are validated by the existing plugin security manager before initialization:

```json
{
  "Dependencies": {
    "Plugins": {
      "Enabled": true,
      "Directory": "plugins",
      "Files": [ "VertexBPMN.McpAgentPlugin.dll" ]
    }
  },
  "Modules": {
    "Plugins": true
  }
}
```

Both switches must be enabled and the host must not run in Test mode. An empty `Files` list loads all top-level `.dll` files from the configured directory; a populated list acts as an allowlist. Relative directories and file names are resolved relative to the host's application directory. The CLI uses the application/engine composition and does not automatically load API plugins.

Configuration precedence follows the normal .NET host order. For environment variables, use `Dependencies__Ai__DefaultModel` or `Dependencies__ServiceTasks__Disabled__0` with the host-specific prefix where applicable.

## Local CLI persistence

The CLI stores runtime dependency overrides in the `DependencyRegistry` SQLite database. The default is:

```json
{
  "ConnectionStrings": {
    "DependencyRegistry": "Data Source=vertexbpmn-dependencies.db"
  }
}
```

Use the CLI to manage persisted values:

```text
config list
config get Dependencies__Ai__DefaultModel
config set Dependencies__Ai__DefaultModel gpt-4o-mini
config remove Dependencies__Ai__DefaultModel
```

The startup precedence is environment variables, then the SQLite registry, then JSON configuration, then code defaults. Only configuration values are stored in the registry. Plugin binaries remain on disk, and secrets must remain in environment variables or an external secret store.
