```yaml
openapi: 3.0.3
info: { title: VertexBPMN Tenancy API Aspects, version: 0.1.0 }
paths:
  /_tenant/ping:
    get:
      summary: Echoes resolved tenant
      parameters:
        - in: header
          name: X-Tenant-Id
          schema: { type: string }
      responses: { '200': { description: OK } }
components: {}

---

```