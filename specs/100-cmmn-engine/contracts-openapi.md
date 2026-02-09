```yaml
openapi: 3.0.3
info:
  title: VertexBPMN CMMN API
  version: 0.1.0
paths:
  /cmmn/case-definition:
    get:
      summary: List case definitions
      parameters:
        - in: header
          name: X-Tenant-Id
          schema: { type: string }
      responses: { '200': { description: OK } }
  /cmmn/case-definition/key/{key}/start:
    post:
      summary: Start case by key
      parameters:
        - in: path
          name: key
          required: true
          schema: { type: string }
        - in: header
          name: X-Tenant-Id
          schema: { type: string }
      requestBody:
        content:
          application/json:
            schema:
              type: object
              properties:
                businessKey: { type: string }
                variables: { type: object, additionalProperties: true }
      responses: { '201': { description: Created } }
  /cmmn/case-instance/{id}:
    get:
      summary: Get case instance
      parameters:
        - in: path
          name: id
          required: true
          schema: { type: string }
      responses: { '200': { description: OK } }
  /cmmn/plan-item-instance/{id}/manual-activate:
    post:
      summary: Manually activate plan item
      parameters:
        - in: path
          name: id
          required: true
          schema: { type: string }
      responses: { '204': { description: Activated } }
  /cmmn/history/case-instance/{id}:
    get:
      summary: History for case instance (UTC)
      responses: { '200': { description: OK } }
components: { schemas: {} }

---

## `specs/110-event-registry/plan.md`
```md
# Plan – Event Registry (HTTP/Kafka/RabbitMQ/JMS)
Date: 2025-09-17

Phasen
1. Research: Korrelation (keys, headers), DLQ/Retries, Exactly-once vs At-least-once
2. Contracts: Channel-, Event-, Subscription-APIs
3. Runtime: Inbound/Outbound, Dispatcher, Correlator
4. Adapter: HTTP (in/out), Kafka, RabbitMQ, JMS
5. Tenancy: Isolierung + Quotas
6. Tests: Contract, Integration (E2E mit Kafka/Rabbit), Chaos/Retries
7. Hardening: Backpressure, Health/Control (pause/resume)
