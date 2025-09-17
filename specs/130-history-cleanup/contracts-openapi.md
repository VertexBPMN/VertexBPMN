```yaml
openapi: 3.0.3
info: { title: VertexBPMN History Cleanup, version: 0.1.0 }
paths:
  /history/policies:
    post: { summary: Create policy, responses: { '201': { description: Created } } }
    get:  { summary: List policies,  responses: { '200': { description: OK } } }
  /history/executions/run-now:
    post: { summary: Trigger cleanup, responses: { '202': { description: Accepted } } }
  /history/executions/{id}:
    get: { summary: Get execution, responses: { '200': { description: OK } } }
components: {}

```