```yaml
openapi: 3.0.3
info: { title: VertexBPMN Standard Tasks, version: 0.1.0 }
paths:
  /connectors/templates:
    post: { summary: Create connector template, responses: { '201': { description: Created } } }
    get:  { summary: List templates, responses: { '200': { description: OK } } }
  /connectors/invoke:
    post:
      summary: Invoke connector by template
      responses: { '202': { description: Accepted } }
components: {}
```