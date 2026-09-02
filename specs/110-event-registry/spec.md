# Spec – Event Registry

## Ziel
Ereignisse modellieren, empfangen, korrelieren und senden. Verwendung in BPMN/CMMN (Start/Boundary/Intermediate).

## Musskriterien
- Artefakte: ChannelDefinition, EventDefinition, Subscription
- Adapter: HTTP, Kafka, RabbitMQ, JMS (Inbound/Outbound)
- Korrelation: key/payload/header-Muster, Expressions
- Fehlerbehandlung: Retries, DLQ, Poison-Message-Erkennung
- Admin: pause/resume Channel, Offsets/ConsumerGroup Status
- Tenancy: channels/events/subscriptions mandantenfähig
- Observability: Durchsatz, Lag, Fehler, DLQ-Counts

## Abnahme
- E2E: Inbound Message → BPMN StartEvent (Message) → Token startet
- E2E: Boundary-Message cancelt Task und verzweigt
- DLQ: 3× Retry, dann DLQ; Admin-API zeigt DLQ-Einträge
