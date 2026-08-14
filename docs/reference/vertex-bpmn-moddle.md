# Vertex BPMN Moddle Extension

Status: Phase 2  
Namespace: `xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0"`

Vertex-specific runtime metadata is stored as BPMN 2.0 extension elements. Studio loads the descriptor from `src/VertexBPMN.Studio/tools/bpmn-io/src/vertex.json` (copied to `wwwroot/lib/vertex-bpmn-moddle/vertex.json`). The Engine parser flattens known `vertex:*` fields when vendor normalization is enabled and always preserves the raw `extensionElements` clone in strict roundtrip.

## Canonical namespace

| Role | URI | Prefix |
| --- | --- | --- |
| Canonical (write `vertex:*` extension elements) | `https://vertexbpmn.io/schema/bpmn/1.0` | `vertex` |
| Legacy sequence-flow `priority` | `http://vertexbpmn.io/schema/1.0` | `vertex` |
| Legacy alias | `http://vertexbpmn.io/schema/1.0/bpmn` | `vertex` |

The Engine still **reads** the legacy URIs (including sequence-flow `priority`). New `vertex:connector` / `webhook` / `trigger` / `credential` elements are written with the HTTPS `/schema/bpmn/1.0` URI. If a document already binds `xmlns:vertex` to the old URI, strict serialization keeps the original declarations.

## Elements

| Element | Typical parent | Attributes | Required |
| --- | --- | --- | --- |
| `vertex:connector` | serviceTask, sendTask, receiveTask, callActivity | `type`, `operationId`, `credentialRef`, `timeoutMs` | `type`, `operationId` |
| `vertex:retryPolicy` | same | `maxAttempts`, `strategy`, `baseDelayMs`, `retryOn` | — |
| `vertex:ioMapping` | same | nested `vertex:input` / `vertex:output` | — |
| `vertex:input` | `vertex:ioMapping` | `name`, `expression` | — |
| `vertex:output` | `vertex:ioMapping` | `name`, `target` | — |
| `vertex:webhook` | startEvent | `path`, `method`, `secretRef` | `path` |
| `vertex:trigger` | startEvent | `type`, `name`, `processDefinitionKey` | `type`, `processDefinitionKey` |
| `vertex:credential` | any extension host | `id`, `kind` | `id`, `kind` |

Unknown extension elements next to `vertex:*` are not dropped in strict mode.

## Validation codes

Opt-in via `BpmnParserOptions.EnableAdvancedValidation`. Severity is Error, category is `Vertex`.

| Code | When |
| --- | --- |
| `VEN-VERTEX-CONNECTOR-TYPE` | `vertex:connector` without `type` |
| `VEN-VERTEX-CONNECTOR-OPERATION` | `vertex:connector` without `operationId` |
| `VEN-VERTEX-WEBHOOK-PATH` | `vertex:webhook` without `path` |
| `VEN-VERTEX-TRIGGER-TYPE` | `vertex:trigger` without `type` |
| `VEN-VERTEX-TRIGGER-PROCESS-KEY` | `vertex:trigger` without `processDefinitionKey` |
| `VEN-VERTEX-CREDENTIAL-ID` | `vertex:credential` without `id` |
| `VEN-VERTEX-CREDENTIAL-KIND` | `vertex:credential` without `kind` |

Studio also exposes `window.VertexValidateBpmn(xmlOrModel)` from the properties-panel bundle.
