# Cloud-Native & .NET 9 Exzellenz in VertexBPMN

VertexBPMN™ ist von Grund auf für moderne Cloud- und Container-Umgebungen konzipiert. Die Engine nutzt die neuesten .NET-9-Features und Best Practices für Skalierbarkeit, Performance und Observability.

## Features für Cloud & DevOps

- **Health-, Liveness- & Readiness-Probes:**
  - `/api/health` (HealthCheck)
  - `/api/metrics` (JSON, Prometheus)
- **Prometheus/OpenTelemetry-Metriken:**
  - `/api/metrics/prometheus` für Monitoring & Alerting
- **BackgroundService/JobExecutor:**
  - Asynchrone Timer, Jobs, Retry-Logik
- **Graceful Shutdown:**
  - BackgroundServices und Engine reagieren auf SIGTERM/CancellationToken
- **Docker- & Kubernetes-Ready:**
  - Offizielles Dockerfile, keine lokalen Abhängigkeiten
- **OpenAPI/Swagger:**
  - Automatisch generiert, für API-Gateways und Contract-Tests
- **Live-Inspector-API:**
  - `/api/inspector/process-instance/{id}/state` für Visual Debugger, Cockpit, Analytics

## Beispiel: Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: vertexbpmn
spec:
  replicas: 2
  selector:
    matchLabels:
      app: vertexbpmn
  template:
    metadata:
      labels:
        app: vertexbpmn
    spec:
      containers:
      - name: vertexbpmn
        image: ghcr.io/vertexbpmn/vertexbpmn:latest
        ports:
        - containerPort: 5000
        livenessProbe:
          httpGet:
            path: /api/health
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /api/health
            port: 5000
          initialDelaySeconds: 5
          periodSeconds: 5
```

## Observability & Monitoring
- Prometheus kann direkt `/api/metrics/prometheus` scrapen
- OpenTelemetry-Integration vorbereitet (Hooks für Tracing/Metrics)

## Weitere Innovationen
- Feature Flags für experimentelle Features
- API für Predictive Analytics & Process Mining vorbereitet
- Visual Debugger/Live-Inspector als API und UI-Hook

---
*Letztes Update: 2025-08-12*
