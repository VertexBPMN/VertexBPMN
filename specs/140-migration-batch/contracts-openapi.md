```yaml
openapi: 3.0.3
info: { title: VertexBPMN Migration & Batch, version: 0.1.0 }
paths:
  /migrations/plans:
    post: { summary: Create migration plan, responses: { '201': { description: Created } } }
    get:  { summary: List plans, responses: { '200': { description: OK } } }
  /migrations/batches:
    post: { summary: Create batch, responses: { '201': { description: Created } } }
    get:  { summary: List batches, responses: { '200': { description: OK } } }
  /migrations/batches/{id}:
    get: { summary: Get batch, responses: { '200': { description: OK } } }
    post:
      summary: Control batch
      parameters:
        - in: query
          name: action
          schema: { type: string, enum: [pause,resume,cancel] }
      responses: { '202': { description: Accepted } }
components: {}
```