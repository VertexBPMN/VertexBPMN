# Monitoring & Observability für VertexBPMN

VertexBPMN bietet Health-Checks, Metriken und Logging out-of-the-box. Für produktive Umgebungen empfiehlt sich die Integration mit Prometheus und Grafana.

## Health-Checks
- Endpoint: `/api/health` (HTTP 200 bei Erfolg)

## Metriken
- Endpoint: `/api/metrics` (JSON)
- Prometheus-kompatibel: `/api/metrics/prometheus`

## Logging
- Standardmäßig Console-Logging, konfigurierbar über `appsettings.json`

## Prometheus Integration
1. Füge den Prometheus-Server zu deinem Kubernetes-Cluster hinzu.
2. Konfiguriere einen ServiceMonitor für VertexBPMN:

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: vertexbpmn-monitor
spec:
  selector:
    matchLabels:
      app: vertexbpmn
  endpoints:
  - port: 80
    path: /api/metrics/prometheus
```

## Grafana Dashboards
- Importiere Prometheus als Datenquelle
- Erstelle Dashboards für BPMN-Events, Prozessmetriken und System-Health

## OpenTelemetry
- Optional: Exportiere Traces und Metriken via OTLP für verteiltes Monitoring

## Beispiel: Alerts
- Alert bei hoher Fehlerquote, langer Prozessdauer oder Ausfall des Health-Checks

---
Weitere Details und Beispiel-Dashboards findest du in `docs/monitoring-examples.md`.
