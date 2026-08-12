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