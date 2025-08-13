# VertexBPMN Observability & Performance

## Logging
- Alle HTTP-Requests und Responses werden mit Methode, Pfad und Statuscode im Konsolen-Log ausgegeben.
- Fehler und Ausnahmen werden automatisch geloggt.

## Health-Check
- `/api/health` liefert Status und Timestamp.

## Metriken
- Management-API: `/api/management/metrics` liefert Engine-Metriken (z.B. Instanzzahlen, Taskzahlen, etc.).
- Erweiterbar für Prometheus/StatsD via Community-Packages.

## Performance-Benchmarks
- Benchmarks für einfache und komplexe BPMN-Modelle im Ordner `VertexBPMN.Benchmarks`:
  - `TokenEngineBenchmarks`: 10.000 einfache Prozesse < 2s
  - `AdvancedTokenEngineBenchmarks`: 5.000 komplexe Prozesse < 3s
- Ergebnisse werden in der Konsole ausgegeben und als Test validiert.

## Erweiterung
- Für produktive Nutzung empfiehlt sich die Integration von Application Insights, OpenTelemetry oder Prometheus für Metriken und Tracing.

---
*Letztes Update: August 2025*
