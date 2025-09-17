# Quickstart – Event Registry

1) POST /events/channels  (Kafka topic, group, bootstrap)
2) POST /events/definitions (order.created + schema)
3) POST /events/subscriptions (correlation: orderId → BPMN Message Start)
4) Sende Nachricht → Prozess startet; Logs/Traces prüfen
