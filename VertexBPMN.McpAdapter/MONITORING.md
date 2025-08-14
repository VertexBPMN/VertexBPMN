# VertexBPMN MCP-Adapter: Erweiterte Telemetry & Monitoring

## Features
- **OpenTelemetry Tracing:** Automatische Traces für HTTP, WebSocket, Engine-Calls
- **Custom Metrics:** Prozessstarts, Fehler, Event-Streams
- **Prometheus-Ready:** Export via OpenTelemetry Collector möglich
- **Alerts:** Integration mit Prometheus Alertmanager (optional)

## Aktivierung
Telemetry ist per Default aktiviert. Für eigene Metriken/Traces:

```csharp
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
// ...
var tracer = TracerProvider.Default.GetTracer("VertexBPMN.McpAdapter");
tracer.StartSpan("custom-operation");
```

## Beispiel: Custom Metric
```csharp
using OpenTelemetry.Metrics;
var meter = MeterProvider.Default.GetMeter("VertexBPMN.McpAdapter");
var counter = meter.CreateCounter<long>("bpmn_process_starts");
counter.Add(1);
```

## Alerts
Prometheus Alertmanager kann über OpenTelemetry Collector angebunden werden.

## Hinweise
- Traces und Metrics werden in der Konsole ausgegeben (Demo)
- Für produktive Nutzung: OpenTelemetry Collector/Prometheus konfigurieren
