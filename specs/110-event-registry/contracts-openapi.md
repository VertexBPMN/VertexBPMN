```yaml
openapi: 3.0.3
info: { title: VertexBPMN Event Registry, version: 0.1.0 }
paths:
  /events/channels:
    post: { summary: Create channel, responses: { '201': { description: Created } } }
    get:  { summary: List channels,  responses: { '200': { description: OK } } }
  /events/channels/{id}/pause:
    post: { summary: Pause channel, responses: { '204': { description: Paused } } }
  /events/definitions:
    post: { summary: Create event definition, responses: { '201': { description: Created } } }
    get:  { summary: List event definitions,  responses: { '200': { description: OK } } }
  /events/subscriptions:
    post: { summary: Create subscription, responses: { '201': { description: Created } } }
    get:  { summary: List subscriptions,  responses: { '200': { description: OK } } }
components: { schemas: {} }

---
`
```