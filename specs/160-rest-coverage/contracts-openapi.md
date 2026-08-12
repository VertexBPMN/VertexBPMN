```yaml
openapi: 3.0.3
info: { title: VertexBPMN REST Coverage Aspects, version: 0.1.0 }
paths:
  /_system/error-example:
    get:
      summary: Standard error envelope
      responses:
        '500':
          description: Error example
          content:
            application/json:
              schema:
                type: object
                properties:
                  code: { type: string }
                  message: { type: string }
                  details: { type: object, additionalProperties: true }
                  traceId: { type: string }
components: {}

```